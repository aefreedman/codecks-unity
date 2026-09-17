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
        private int session;
        private bool submissionInFlight;

        private void OnDisable()
        {
            InvalidateSession();
        }

        /// <summary>
        /// Shows the Codecks Report Form.
        /// </summary>
        public void ShowCodecksForm()
        {
            InvalidateSession();
            cardCreator.StartCoroutine(ShowCodecksFormCoroutine(session));
        }

        private IEnumerator ShowCodecksFormCoroutine(int activeSession)
        {
            yield return new WaitForEndOfFrame();
            if (!IsCurrentSession(activeSession))
                yield break;

            queuedScreenshot = null;
            Texture2D screenshotTexture = null;
            try
            {
                screenshotTexture = ScreenCapture.CaptureScreenshotAsTexture();
                if (screenshotTexture != null)
                {
#if UNITY_STANDALONE
                    queuedScreenshot = screenshotTexture.EncodeToJPG();
#else
                    queuedScreenshot = screenshotTexture.EncodeToPNG();
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

            if (!IsCurrentSession(activeSession))
                yield break;

            textArea.text = "";
            submissionInFlight = false;
            sendButton.interactable = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the Codecks Report Form.
        /// </summary>
        public void HideCodecksForm()
        {
            InvalidateSession();
            gameObject.SetActive(false);
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
            if (submissionInFlight)
                return;

            if (textArea.text.Length < 10)
            {
                statusText.text = statusShortText;
                return;
            }

            string reportText = $"{textArea.text}\n\n{GetMetaText()}";
            var files = new Dictionary<string, (byte[], CodecksCardCreator.CodecksFileType)>();
            if (queuedScreenshot != null)
            {
#if UNITY_STANDALONE
                files["screenshot.jpg"] = (queuedScreenshot, CodecksCardCreator.CodecksFileType.JPG);
#else
                files["screenshot.png"] = (queuedScreenshot, CodecksCardCreator.CodecksFileType.PNG);
#endif
            }

            int activeSession = session;
            submissionInFlight = true;
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

        private void InvalidateSession()
        {
            session++;
            submissionInFlight = false;
            queuedScreenshot = null;
        }

        private bool IsCurrentSession(int activeSession)
        {
            return activeSession == session;
        }

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
