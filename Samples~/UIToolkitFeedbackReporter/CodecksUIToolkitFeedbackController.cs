using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Codecks.Runtime;
using UnityEngine;
using UnityEngine.UIElements;

namespace Codecks.Samples.UIToolkitFeedbackReporter
{
    /// <summary>
    /// Binds the editable UI Toolkit feedback template to a CodecksCardCreator backend.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class CodecksUIToolkitFeedbackController : MonoBehaviour
    {
        [SerializeField] private CodecksCardCreator cardCreator;
        [SerializeField] private string reportElementName = "codecks-feedback-overlay";

        private PanelRenderer panelRenderer;
        private VisualElement root;
        private VisualElement overlay;
        private Button launcherButton;
        private TextField reportField;
        private DropdownField severityField;
        private TextField emailField;
        private Button sendButton;
        private Button cancelButton;
        private Label statusLabel;
        private byte[] queuedScreenshot;
        private CodecksCardCreator.CodecksFileType queuedScreenshotType;
        private int boundVersion = -1;
        private int bindingGeneration;
        private int session;
        private bool submissionInFlight;
        private bool awaitingSubmission;
        private readonly CodecksFormLifecycle lifecycle = new CodecksFormLifecycle();
        public CodecksFormState State => lifecycle.State;
        public event Action<CodecksFormState> StateChanged
        {
            add => lifecycle.StateChanged += value;
            remove => lifecycle.StateChanged -= value;
        }
        /// <summary>Optional caller modal ownership. Assigned in code; null means no ownership.</summary>
        public Func<IDisposable> AcquireScope { get; set; }
        /// <summary>Current session only; requests dispatched by closed sessions can still complete.</summary>
        public bool IsSubmitting => awaitingSubmission;
        private Coroutine showCoroutine;
        private Coroutine dismissCoroutine;
        private Coroutine bindingRecoveryCoroutine;

        private void OnEnable()
        {
            panelRenderer = GetComponent<PanelRenderer>();
            panelRenderer.RegisterUIReloadCallback(OnUIReload);

            // A disabled Panel Renderer has no initialized root, so defer one normal
            // callback registration until it becomes active again.
            if (!IsPanelRendererActive())
                bindingRecoveryCoroutine = StartCoroutine(RebindWhenRendererIsActive());
        }

        private void OnDisable()
        {
            if (panelRenderer != null)
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);

            CloseSession(true);
        }

        private void OnDestroy() => CloseSession(true);

        private void Update()
        {
            if (State != CodecksFormState.Closed && !IsBound())
                CloseSession(true);
        }

        private IEnumerator RebindWhenRendererIsActive()
        {
            while (isActiveAndEnabled && !IsPanelRendererActive())
                yield return null;

            bindingRecoveryCoroutine = null;
            if (!isActiveAndEnabled || root != null || panelRenderer.visualTreeAsset == null)
                yield break;

            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private bool IsPanelRendererActive()
        {
            return panelRenderer != null && panelRenderer.enabled && panelRenderer.gameObject.activeInHierarchy;
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement currentRoot, int version)
        {
            if (root == currentRoot && boundVersion == version)
                return;

            int binding = ++bindingGeneration;
            CloseSession(true);
            if (!isActiveAndEnabled || binding != bindingGeneration) return;

            if (currentRoot == null || (renderer != null && renderer.visualTreeAsset == null))
            {
                Debug.LogError("Codecks UI Toolkit feedback reporter received an empty Panel Renderer root.", this);
                return;
            }
            root = currentRoot;
            boundVersion = version;

            overlay = root.Q<VisualElement>(reportElementName);
            launcherButton = root.Q<Button>("codecks-feedback-launcher");
            reportField = root.Q<TextField>("codecks-feedback-report");
            severityField = root.Q<DropdownField>("codecks-feedback-severity");
            emailField = root.Q<TextField>("codecks-feedback-email");
            sendButton = root.Q<Button>("codecks-feedback-send");
            cancelButton = root.Q<Button>("codecks-feedback-cancel");
            statusLabel = root.Q<Label>("codecks-feedback-status");

            if (!ValidateRequiredElements())
                return;

            severityField.formatSelectedValueCallback = FormatSeverity;
            severityField.formatListItemCallback = FormatSeverity;
            if (launcherButton != null) launcherButton.clicked += ShowCodecksForm;
            sendButton.clicked += SendReport;
            cancelButton.clicked += HideCodecksForm;
            overlay.style.display = DisplayStyle.None;
            statusLabel.text = string.Empty;
        }

        /// <summary>Captures a screenshot, then displays the report overlay.</summary>
        public void ShowCodecksForm()
        {
            if (!IsBound() || State != CodecksFormState.Closed)
                return;

            int opening = BeginSession();
            if (IsCurrentSession(opening)) showCoroutine = StartCoroutine(ShowAfterScreenshotCoroutine(opening));
        }

        /// <summary>Opens immediately with caller-owned encoded JPG or PNG bytes, without capture or an end-of-frame wait.</summary>
        public void ShowCodecksForm(byte[] screenshot, CodecksCardCreator.CodecksFileType fileType)
        {
            if (screenshot == null) throw new ArgumentNullException(nameof(screenshot));
            if (screenshot.Length == 0) throw new ArgumentException("Screenshot bytes must not be empty.", nameof(screenshot));
            if (fileType != CodecksCardCreator.CodecksFileType.JPG && fileType != CodecksCardCreator.CodecksFileType.PNG)
                throw new ArgumentOutOfRangeException(nameof(fileType), "Screenshots must be encoded as JPG or PNG.");
            if (!IsBound()) return;
            int opening = BeginSession();
            if (IsCurrentSession(opening) && IsBound()) OpenForm(opening, screenshot, fileType);
        }

        /// <summary>Opens immediately without a screenshot, capture, or an end-of-frame wait.</summary>
        public void ShowCodecksFormWithoutScreenshot()
        {
            if (!IsBound()) return;
            int opening = BeginSession();
            if (IsCurrentSession(opening) && IsBound()) OpenForm(opening, null, DefaultScreenshotFileType);
        }

        /// <summary>Closes the report overlay without removing the Panel Renderer reload subscription.</summary>
        public void HideCodecksForm()
        {
            CloseSession(false);
        }

        private IEnumerator ShowAfterScreenshotCoroutine(int activeSession)
        {
            yield return new WaitForEndOfFrame();
            if (!IsCurrentSession(activeSession))
                yield break;

            byte[] screenshot;
            try { screenshot = CaptureScreenshot(); }
            catch (Exception)
            {
                if (IsCurrentSession(activeSession)) HideCodecksForm();
                Debug.LogWarning("Codecks UI Toolkit capture override threw; opening was aborted.");
                yield break;
            }
            if (!IsCurrentSession(activeSession)) yield break;
            if (!IsBound()) { HideCodecksForm(); yield break; }
            showCoroutine = null;
            OpenForm(activeSession, screenshot, DefaultScreenshotFileType);
        }

        private static CodecksCardCreator.CodecksFileType DefaultScreenshotFileType =>
#if UNITY_STANDALONE
            CodecksCardCreator.CodecksFileType.JPG;
#else
            CodecksCardCreator.CodecksFileType.PNG;
#endif

        private void OpenForm(int opening, byte[] screenshot, CodecksCardCreator.CodecksFileType fileType)
        {
            queuedScreenshot = screenshot;
            queuedScreenshotType = fileType;
            reportField.SetValueWithoutNotify(string.Empty);
            statusLabel.text = string.Empty;
            sendButton.SetEnabled(true);
            if (!IsCurrentSession(opening) || !IsBound()) return;
            overlay.style.display = DisplayStyle.Flex;
            reportField.Focus();
            if (!IsCurrentSession(opening)) return;
            if (!IsBound()) { CloseSession(true); return; }
            lifecycle.Open(opening);
        }

        private void SendReport()
        {
            if (!IsBound() || State != CodecksFormState.Open || submissionInFlight)
                return;

            if (reportField.value == null || reportField.value.Trim().Length < 10)
            {
                statusLabel.text = "Please enter at least 10 characters.";
                reportField.Focus();
                return;
            }

            int activeSession = session;
            submissionInFlight = true;
            awaitingSubmission = true;
            sendButton.SetEnabled(false);
            statusLabel.text = "Sending report...";
            string report = reportField.value + "\n\n" + GetMetadata();
            if (!IsCurrentSession(activeSession) || !IsBound()) return;
            var files = new Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)>();
            if (queuedScreenshot != null)
            {
                string fileName = queuedScreenshotType == CodecksCardCreator.CodecksFileType.JPG ? "screenshot.jpg" : "screenshot.png";
                files[fileName] = (queuedScreenshot, queuedScreenshotType);
            }

            cardCreator.CreateNewCard(report, files, MapSeverity(severityField.value), emailField.value,
                (success, result) => HandleSubmissionResult(activeSession, success, result));
        }

        private void HandleSubmissionResult(int activeSession, bool success, string result)
        {
            if (!IsCurrentSession(activeSession) || !IsBound() || State != CodecksFormState.Open)
                return;

            awaitingSubmission = false;
            if (!success)
            {
                Debug.LogWarning("Codecks UI Toolkit report submission failed: " + result, this);
                submissionInFlight = false;
                // The backend only reaches attachment errors after creating the card.
                // Do not imply that retrying this partial failure cannot create a duplicate.
                bool attachmentFailed = result != null &&
                    (result.StartsWith("error uploading file ", StringComparison.Ordinal) ||
                     result.Contains("Codecks attachment upload"));
                statusLabel.text = attachmentFailed
                    ? "Report created, but the screenshot upload failed. Retrying sends a new report."
                    : "Report could not be sent. Please try again.";
                sendButton.SetEnabled(true);
                return;
            }

            statusLabel.text = "Thank you! Your report was sent.";
            dismissCoroutine = StartCoroutine(DismissAfterSuccessCoroutine(activeSession));
        }

        private IEnumerator DismissAfterSuccessCoroutine(int activeSession)
        {
            yield return new WaitForSecondsRealtime(1f);
            if (IsCurrentSession(activeSession))
                HideCodecksForm();
        }

        /// <summary>Captures before the overlay is visible. Overrides must return the default platform encoding (standalone JPG, otherwise PNG), or null.</summary>
        protected virtual byte[] CaptureScreenshot()
        {
            Texture2D screenshot = null;
            try
            {
                screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshot == null)
                {
                    Debug.LogWarning("Codecks UI Toolkit feedback reporter could not capture a screenshot; sending without one.", this);
                    return null;
                }

#if UNITY_STANDALONE
                return screenshot.EncodeToJPG();
#else
                return screenshot.EncodeToPNG();
#endif
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Codecks UI Toolkit feedback reporter could not capture a screenshot; sending without one.", this);
                return null;
            }
            finally
            {
                if (screenshot != null)
                    Destroy(screenshot);
            }
        }

        private bool ValidateRequiredElements()
        {
            if (overlay != null && reportField != null && severityField != null &&
                emailField != null && sendButton != null && cancelButton != null && statusLabel != null && cardCreator != null)
                return true;

            Debug.LogError("Codecks UI Toolkit feedback reporter is not configured. Assign CodecksCardCreator and retain the named overlay, report, severity, email, send, cancel, and status elements (launcher is optional) from the sample UXML.", this);
            DetachBindings();
            return false;
        }

        private void DetachBindings()
        {
            if (launcherButton != null)
                launcherButton.clicked -= ShowCodecksForm;
            if (sendButton != null)
                sendButton.clicked -= SendReport;
            if (cancelButton != null)
                cancelButton.clicked -= HideCodecksForm;

            root = null;
            overlay = null;
            launcherButton = null;
            reportField = null;
            severityField = null;
            emailField = null;
            sendButton = null;
            cancelButton = null;
            statusLabel = null;
            boundVersion = -1;
        }

        private int BeginSession()
        {
            int opening = lifecycle.Begin(ResetSession, () => AcquireScope?.Invoke(), HideCodecksForm);
            if (IsCurrentSession(opening) && !IsBound()) CloseSession(true);
            return opening;
        }

        private void ResetSession(int current)
        {
            session = current;
            submissionInFlight = false;
            awaitingSubmission = false;
            queuedScreenshot = null;
            StopActiveCoroutines();
        }

        private void CloseSession(bool detach)
        {
            if (State == CodecksFormState.Closed)
            {
                StopActiveCoroutines();
                if (overlay != null) overlay.style.display = DisplayStyle.None;
                if (detach) DetachBindings();
                return;
            }
            lifecycle.Close(current =>
            {
                ResetSession(current);
                if (overlay != null) overlay.style.display = DisplayStyle.None;
                if (detach) DetachBindings();
            });
        }

        private void StopActiveCoroutines()
        {
            if (showCoroutine != null)
                StopCoroutine(showCoroutine);
            if (dismissCoroutine != null)
                StopCoroutine(dismissCoroutine);
            if (bindingRecoveryCoroutine != null)
                StopCoroutine(bindingRecoveryCoroutine);
            showCoroutine = null;
            dismissCoroutine = null;
            bindingRecoveryCoroutine = null;
        }

        private bool IsBound()
        {
            return isActiveAndEnabled && IsPanelRendererActive() && root != null && overlay != null && cardCreator != null;
        }

        private bool IsCurrentSession(int activeSession)
        {
            return this != null && isActiveAndEnabled && lifecycle.IsCurrent(activeSession);
        }

        private static string FormatSeverity(string severity)
        {
            switch (severity)
            {
                case "Low": return "Minor - Typos, visual glitches, missing sound or animation";
                case "High": return "Major - Exploits, unreadable text, major balance issues, broken abilities";
                case "Critical": return "Critical - I am unable to continue playing!";
                default: return "Feedback - I just have opinions about something";
            }
        }

        private static CodecksCardCreator.CodecksSeverity MapSeverity(string severity)
        {
            switch (severity)
            {
                case "Low": return CodecksCardCreator.CodecksSeverity.Low;
                case "High": return CodecksCardCreator.CodecksSeverity.High;
                case "Critical": return CodecksCardCreator.CodecksSeverity.Critical;
                default: return CodecksCardCreator.CodecksSeverity.None;
            }
        }

        /// <summary>Override in a consumer-owned subclass to include project metadata.</summary>
        protected virtual string GetMetadata()
        {
            var metadata = new StringBuilder();
            metadata.AppendLine("```");
            metadata.AppendLine("Platform: " + Application.platform);
            metadata.AppendLine("App Version: " + Application.version);
            metadata.AppendLine("```");
            return metadata.ToString();
        }
    }
}
