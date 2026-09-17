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

        /// <summary>
        /// Shows the Codecks Report Form.
        /// </summary>
        public void ShowCodecksForm()
        {
            cardCreator.StartCoroutine(ShowCodecksFormCoroutine());
        }

        private IEnumerator ShowCodecksFormCoroutine()
        {
            yield return new WaitForEndOfFrame();

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
            catch (Exception ex)
            {
                Debug.LogWarning($"Codecks report form could not capture a screenshot: {ex.Message}. The report will be sent without one.");
            }
            finally
            {
                if (screenshotTexture != null)
                    Destroy(screenshotTexture);
            }

            textArea.text = "";
            sendButton.interactable = true;
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hides the Codecks Report Form.
        /// </summary>
        public void HideCodecksForm()
        {
            queuedScreenshot = null;
            gameObject.SetActive(false);
        }

        private IEnumerator HideCodecksFormWithDelayCoroutine()
        {
            yield return new WaitForSecondsRealtime(1);
            HideCodecksForm();
        }

        /// <summary>
        /// Called when the -Send Report- button is clicked.
        /// </summary>
        public void OnButtonSend()
        {
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

            statusText.text = statusSending;
            sendButton.interactable = false;

            cardCreator.CreateNewCard(
                text: reportText,
                files: files,
                severity: (CodecksCardCreator.CodecksSeverity)categoryDropdown.value,
                userEmail: emailInput.text,
                resultDelegate: (success, result) =>
                {
                    if (success)
                    {
                        statusText.text = statusSent;
                        sendButton.interactable = false;
                        StartCoroutine(HideCodecksFormWithDelayCoroutine());
                    }
                    else
                    {
                        Debug.LogWarning($"Codecks report submission failed: {result}");
                        sendButton.interactable = true;
                        statusText.text = statusError;
                    }
                });
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
