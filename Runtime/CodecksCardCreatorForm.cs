using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Codecks.Runtime
{
    public class CodecksCardCreatorForm : MonoBehaviour
    {
        /// <summary>
        /// Reference to the CardCreator class inside the hierarchy.
        /// </summary>
        public CodecksCardCreator cardCreator;

        [Header("UI References")]
        public TMP_Dropdown categoryDropdown;
        public TMP_InputField textArea;
        public TMP_InputField emailInput;
        public TMP_Text statusText;
        public Button sendButton;

        [Header("Texts")]
        public string statusShortText;
        public string statusSending;
        public string statusSent;
        public string statusError;

        private byte[] queuedScreenshot;
        private CodecksCardCreator.CodecksFileType queuedScreenshotType;
        private int session;
        private bool submissionInFlight;
        private bool awaitingSubmission;
        private readonly CodecksFormLifecycle lifecycle = new CodecksFormLifecycle();
        private bool destroying;
        private bool abortingPendingOpening;
        private CodecksPendingOpening pendingOpening;

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

        private void OnDisable() => CloseSession(true);

        private void OnDestroy()
        {
            destroying = true;
            CloseSession(false);
        }

        /// <summary>
        /// Shows the Codecks Report Form.
        /// </summary>
        public void ShowCodecksForm()
        {
            // A second request while visible would capture the feedback UI into its own attachment.
            if (destroying || !enabled || abortingPendingOpening || gameObject.activeInHierarchy || State != CodecksFormState.Closed)
                return;

            int opening = BeginSession();
            if (!IsCurrentSession(opening)) return;
            if (cardCreator == null || !cardCreator.isActiveAndEnabled) { HideCodecksForm(); return; }
            var captureHost = cardCreator;
            pendingOpening = captureHost.gameObject.AddComponent<CodecksPendingOpening>();
            pendingOpening.Initialize(
                () => this != null && enabled && !destroying && captureHost != null && captureHost.isActiveAndEnabled,
                () =>
                {
                    if (!lifecycle.IsCurrent(opening)) return;
                    abortingPendingOpening = true;
                    try { CloseSession(true); }
                    finally { abortingPendingOpening = false; }
                });
            captureHost.StartCoroutine(ShowCodecksFormCoroutine(opening));
        }

        private IEnumerator ShowCodecksFormCoroutine(int activeSession)
        {
            yield return new WaitForEndOfFrame();
            if (!IsCurrentSession(activeSession))
            {
                if (lifecycle.IsCurrent(activeSession)) CloseSession(false);
                yield break;
            }

            byte[] screenshot;
            try { screenshot = CaptureScreenshot(); }
            catch (Exception)
            {
                if (IsCurrentSession(activeSession)) HideCodecksForm();
                Debug.LogWarning("Codecks report capture override threw; opening was aborted.");
                yield break;
            }
            if (IsCurrentSession(activeSession))
                OpenForm(activeSession, screenshot, DefaultScreenshotFileType);
        }

        /// <summary>Opens immediately with caller-owned encoded JPG or PNG bytes, without capture or an end-of-frame wait.</summary>
        public void ShowCodecksForm(byte[] screenshot, CodecksCardCreator.CodecksFileType fileType)
        {
            if (screenshot == null) throw new ArgumentNullException(nameof(screenshot));
            if (screenshot.Length == 0) throw new ArgumentException("Screenshot bytes must not be empty.", nameof(screenshot));
            if (fileType != CodecksCardCreator.CodecksFileType.JPG && fileType != CodecksCardCreator.CodecksFileType.PNG)
                throw new ArgumentOutOfRangeException(nameof(fileType), "Screenshots must be encoded as JPG or PNG.");
            if (destroying || !enabled) return;
            int opening = BeginSession();
            if (IsCurrentSession(opening)) OpenForm(opening, screenshot, fileType);
        }

        /// <summary>Opens immediately without a screenshot, capture, or an end-of-frame wait.</summary>
        public void ShowCodecksFormWithoutScreenshot()
        {
            if (destroying || !enabled) return;
            int opening = BeginSession();
            if (IsCurrentSession(opening)) OpenForm(opening, null, DefaultScreenshotFileType);
        }

        private void OpenForm(int opening, byte[] screenshot, CodecksCardCreator.CodecksFileType fileType)
        {
            if (textArea == null || sendButton == null)
            { HideCodecksForm(); return; }
            queuedScreenshot = screenshot;
            queuedScreenshotType = fileType;
            textArea.SetTextWithoutNotify("");
            sendButton.interactable = true;
            if (statusText != null) statusText.text = string.Empty;
            gameObject.SetActive(true);
            if (!IsCurrentSession(opening)) return;
            if (!gameObject.activeInHierarchy) { HideCodecksForm(); return; }
            StopPendingOpening();
            lifecycle.Open(opening);
        }

        private static CodecksCardCreator.CodecksFileType DefaultScreenshotFileType =>
#if UNITY_STANDALONE
            CodecksCardCreator.CodecksFileType.JPG;
#else
            CodecksCardCreator.CodecksFileType.PNG;
#endif

        /// <summary>Override to return the default platform encoding (standalone JPG, otherwise PNG), or null to omit capture.</summary>
        protected virtual byte[] CaptureScreenshot()
        {
            Texture2D screenshotTexture = null;
            try
            {
                screenshotTexture = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshotTexture != null)
                {
#if UNITY_STANDALONE
                    return screenshotTexture.EncodeToJPG();
#else
                    return screenshotTexture.EncodeToPNG();
#endif
                }
                else
                {
                    Debug.LogWarning("Codecks report form could not capture a screenshot; the report will be sent without one.");
                }
            }
            catch (Exception)
            {
                Debug.LogWarning("Codecks report form could not capture a screenshot; the report will be sent without one.");
            }
            finally
            {
                if (screenshotTexture != null)
                    Destroy(screenshotTexture);
            }

            return null;
        }

        /// <summary>
        /// Hides the Codecks Report Form.
        /// </summary>
        public void HideCodecksForm()
        {
            if (State == CodecksFormState.Closed) { if (this != null) gameObject.SetActive(false); return; }
            CloseSession(true);
        }

        private IEnumerator HideCodecksFormWithDelayCoroutine(int activeSession)
        {
            yield return new WaitForSecondsRealtime(1);
            if (IsCurrentSession(activeSession))
                HideCodecksForm();
        }

        /// <summary>
        /// Called when the -Send Report- button is clicked.
        /// </summary>
        public void OnButtonSend()
        {
            if (State != CodecksFormState.Open || submissionInFlight)
                return;

            if (textArea.text.Length < 10)
            {
                statusText.text = statusShortText;
                return;
            }

            int activeSession = session;
            string reportText = $"{textArea.text}\n\n{GetMetaText()}";
            if (!IsCurrentSession(activeSession) || State != CodecksFormState.Open || submissionInFlight) return;
            var files = new Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)>();
            if (queuedScreenshot != null)
            {
                string fileName = queuedScreenshotType == CodecksCardCreator.CodecksFileType.JPG ? "screenshot.jpg" : "screenshot.png";
                files[fileName] = (queuedScreenshot, queuedScreenshotType);
            }

            submissionInFlight = true;
            awaitingSubmission = true;
            statusText.text = statusSending;
            sendButton.interactable = false;

            cardCreator.CreateNewCard(
                text: reportText,
                files: files,
                severity: (CodecksCardCreator.CodecksSeverity)categoryDropdown.value,
                userEmail: emailInput.text,
                resultDelegate: (success, result) => HandleSubmissionResult(activeSession, success, result));
        }

        private void HandleSubmissionResult(int activeSession, bool success, string result)
        {
            if (!IsCurrentSession(activeSession) || !gameObject.activeInHierarchy)
                return;

            awaitingSubmission = false;
            if (success)
            {
                statusText.text = statusSent;
                sendButton.interactable = false;
                StartCoroutine(HideCodecksFormWithDelayCoroutine(activeSession));
                return;
            }

            Debug.LogWarning($"Codecks report submission failed: {result}");
            submissionInFlight = false;
            sendButton.interactable = true;
            statusText.text = statusError;
        }

        private int BeginSession()
        {
            int opening = lifecycle.Begin(ResetSession,
                () => this != null && !destroying && enabled ? AcquireScope?.Invoke() : null, HideCodecksForm);
            if (lifecycle.IsCurrent(opening) && (this == null || destroying || !enabled)) CloseSession(true);
            return opening;
        }

        private void ResetSession(int current)
        {
            StopPendingOpening();
            session = current;
            submissionInFlight = false;
            awaitingSubmission = false;
            queuedScreenshot = null;
        }

        private void StopPendingOpening()
        {
            var guard = pendingOpening;
            pendingOpening = null;
            if (guard != null) guard.Disarm();
        }

        private void CloseSession(bool hide)
        {
            lifecycle.Close(current =>
            {
                ResetSession(current);
                if (hide && this != null) gameObject.SetActive(false);
            });
        }

        private bool IsCurrentSession(int activeSession) =>
            this != null && !destroying && enabled && lifecycle.IsCurrent(activeSession);

        /// <summary>
        /// Called when the Cancel button is clicked.
        /// </summary>
        public void OnButtonCancel()
        {
            HideCodecksForm();
        }

        /// <summary>
        /// Adds game-related information to the report. Override this in a consumer-owned subclass to add metadata
        /// without modifying the package cache.
        /// </summary>
        protected virtual string GetMetaText()
        {
            var metaText = new StringBuilder();
            metaText.AppendLine("```");
            metaText.AppendLine($"Platform: {Application.platform}");
            metaText.AppendLine($"App Version: {Application.version}");
            metaText.AppendLine("```");
            return metaText.ToString();
        }
    }
}
