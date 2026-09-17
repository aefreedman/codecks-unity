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
        private int boundVersion = -1;
        private int session;
        private bool overlayOpen;
        private bool submissionInFlight;
        private Coroutine showCoroutine;
        private Coroutine dismissCoroutine;

        private void OnEnable()
        {
            panelRenderer = GetComponent<PanelRenderer>();
            panelRenderer.RegisterUIReloadCallback(OnUIReload);
        }

        private void OnDisable()
        {
            if (panelRenderer != null)
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);

            InvalidateSession();
            DetachBindings();
        }

        private void OnUIReload(PanelRenderer renderer, VisualElement currentRoot, int version)
        {
            if (root == currentRoot && boundVersion == version)
                return;

            InvalidateSession();
            DetachBindings();

            if (currentRoot == null)
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

            launcherButton.clicked += ShowCodecksForm;
            sendButton.clicked += SendReport;
            cancelButton.clicked += HideCodecksForm;
            overlay.style.display = DisplayStyle.None;
            statusLabel.text = string.Empty;
        }

        /// <summary>Captures a screenshot, then displays the report overlay.</summary>
        public void ShowCodecksForm()
        {
            if (!IsBound() || overlayOpen || showCoroutine != null)
                return;

            InvalidateSession();
            showCoroutine = StartCoroutine(ShowAfterScreenshotCoroutine(session));
        }

        /// <summary>Closes the report overlay without removing the Panel Renderer reload subscription.</summary>
        public void HideCodecksForm()
        {
            InvalidateSession();
            if (overlay != null)
                overlay.style.display = DisplayStyle.None;
        }

        private IEnumerator ShowAfterScreenshotCoroutine(int activeSession)
        {
            yield return new WaitForEndOfFrame();
            if (!IsCurrentSession(activeSession))
                yield break;

            queuedScreenshot = CaptureScreenshot();
            if (!IsCurrentSession(activeSession) || overlay == null)
                yield break;

            reportField.value = string.Empty;
            statusLabel.text = string.Empty;
            sendButton.SetEnabled(true);
            overlay.style.display = DisplayStyle.Flex;
            overlayOpen = true;
            reportField.Focus();
            showCoroutine = null;
        }

        private void SendReport()
        {
            if (!IsBound() || !overlayOpen || submissionInFlight)
                return;

            if (reportField.value == null || reportField.value.Trim().Length < 10)
            {
                statusLabel.text = "Please enter at least 10 characters.";
                reportField.Focus();
                return;
            }

            int activeSession = session;
            submissionInFlight = true;
            sendButton.SetEnabled(false);
            statusLabel.text = "Sending report...";
            string report = reportField.value + "\n\n" + GetMetadata();
            var files = new Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)>();
            if (queuedScreenshot != null)
            {
#if UNITY_STANDALONE
                files["screenshot.jpg"] = (queuedScreenshot, CodecksCardCreator.CodecksFileType.JPG);
#else
                files["screenshot.png"] = (queuedScreenshot, CodecksCardCreator.CodecksFileType.PNG);
#endif
            }

            cardCreator.CreateNewCard(report, files, MapSeverity(severityField.value), emailField.value,
                (success, result) => HandleSubmissionResult(activeSession, success, result));
        }

        private void HandleSubmissionResult(int activeSession, bool success, string result)
        {
            if (!IsCurrentSession(activeSession) || !IsBound() || overlay.style.display == DisplayStyle.None)
                return;

            if (!success)
            {
                Debug.LogWarning("Codecks UI Toolkit report submission failed: " + result, this);
                submissionInFlight = false;
                statusLabel.text = "Report could not be sent. Please try again.";
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

        /// <summary>Captures the frame before the feedback overlay becomes visible.</summary>
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
            if (overlay != null && launcherButton != null && reportField != null && severityField != null &&
                emailField != null && sendButton != null && cancelButton != null && statusLabel != null && cardCreator != null)
                return true;

            Debug.LogError("Codecks UI Toolkit feedback reporter is not configured. Assign CodecksCardCreator and retain the named launcher, overlay, report, severity, email, send, cancel, and status elements from the sample UXML.", this);
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

        private void InvalidateSession()
        {
            session++;
            overlayOpen = false;
            submissionInFlight = false;
            queuedScreenshot = null;
            StopActiveCoroutines();
        }

        private void StopActiveCoroutines()
        {
            if (showCoroutine != null)
                StopCoroutine(showCoroutine);
            if (dismissCoroutine != null)
                StopCoroutine(dismissCoroutine);
            showCoroutine = null;
            dismissCoroutine = null;
        }

        private bool IsBound()
        {
            return isActiveAndEnabled && root != null && overlay != null && cardCreator != null;
        }

        private bool IsCurrentSession(int activeSession)
        {
            return isActiveAndEnabled && activeSession == session;
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
