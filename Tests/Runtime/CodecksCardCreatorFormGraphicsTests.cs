using System.Collections;
using System.Reflection;
using Codecks.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Codecks.Tests.PlayMode
{
    public class CodecksCardCreatorFormGraphicsTests
    {
        [UnityTest]
        public IEnumerator ShowCodecksForm_CapturesRenderedFrameBeforeDistinctiveOverlay_AndIgnoresRepeatedShow()
        {
            var cameraHost = new GameObject("Codecks graphics camera");
            var backendHost = new GameObject("Codecks graphics backend");
            var formHost = new GameObject("Codecks distinctive magenta overlay");
            Texture2D captured = null;
            try
            {
                var camera = cameraHost.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.05f, 0.85f, 0.1f, 1f);
                camera.cullingMask = 0;

                var creator = backendHost.AddComponent<CodecksCardCreator>();
                formHost.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                var overlay = formHost.AddComponent<Image>();
                overlay.color = Color.magenta;
                var form = formHost.AddComponent<CodecksCardCreatorForm>();
                form.cardCreator = creator;
                form.textArea = CreateInput(formHost.transform, "Report");
                form.sendButton = CreateButton(formHost.transform, "Send");
                formHost.SetActive(false);

                form.ShowCodecksForm();
                yield return new WaitForEndOfFrame();
                yield return null;

                byte[] firstCapture = GetQueuedScreenshot(form);
                Assert.That(formHost.activeSelf, Is.True);
                Assert.That(firstCapture, Is.Not.Null.And.Not.Empty);

                captured = new Texture2D(2, 2);
                Assert.That(ImageConversion.LoadImage(captured, firstCapture), Is.True);
                Assert.That(captured.width, Is.GreaterThan(1));
                Assert.That(captured.height, Is.GreaterThan(1));
                Assert.That(CountPixels(captured, color => color.g > 0.6f && color.r < 0.3f && color.b < 0.3f), Is.GreaterThan(0));
                Assert.That(CountPixels(captured, color => color.r > 0.8f && color.g < 0.2f && color.b > 0.8f), Is.EqualTo(0));

                form.ShowCodecksForm();
                yield return new WaitForEndOfFrame();
                Assert.That(GetQueuedScreenshot(form), Is.SameAs(firstCapture));
            }
            finally
            {
                if (captured != null)
                    Object.Destroy(captured);
                Object.Destroy(cameraHost);
                Object.Destroy(backendHost);
                Object.Destroy(formHost);
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulSubmission_DismissesWhileTimeScaleIsPaused()
        {
            var backendHost = new GameObject("Codecks paused backend");
            var formHost = new GameObject("Codecks paused form");
            float originalTimeScale = Time.timeScale;
            try
            {
                var form = formHost.AddComponent<CodecksCardCreatorForm>();
                form.cardCreator = backendHost.AddComponent<CodecksCardCreator>();
                form.sendButton = CreateButton(formHost.transform, "Send");
                var statusHost = new GameObject("Status", typeof(TextMeshProUGUI));
                statusHost.transform.SetParent(formHost.transform, false);
                form.statusText = statusHost.GetComponent<TextMeshProUGUI>();
                form.statusSent = "Sent";
                form.textArea = CreateInput(formHost.transform, "Report");
                formHost.SetActive(false);
                form.ShowCodecksFormWithoutScreenshot();
                int session = (int)typeof(CodecksCardCreatorForm).GetField("session", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);

                Time.timeScale = 0f;
                typeof(CodecksCardCreatorForm)
                    .GetMethod("HandleSubmissionResult", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { session, true, "card" });
                yield return new WaitForSecondsRealtime(1.1f);

                Assert.That(formHost.activeSelf, Is.False);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Object.Destroy(backendHost);
                Object.Destroy(formHost);
            }
        }

        private static TMP_InputField CreateInput(Transform parent, string name)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TMP_InputField));
            host.transform.SetParent(parent, false);
            var input = host.GetComponent<TMP_InputField>();
            var textHost = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textHost.transform.SetParent(host.transform, false);
            input.textComponent = textHost.GetComponent<TextMeshProUGUI>();
            return input;
        }

        private static Button CreateButton(Transform parent, string name)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            host.transform.SetParent(parent, false);
            return host.GetComponent<Button>();
        }

        private static byte[] GetQueuedScreenshot(CodecksCardCreatorForm form)
        {
            return (byte[])typeof(CodecksCardCreatorForm)
                .GetField("queuedScreenshot", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(form);
        }

        private static int CountPixels(Texture2D texture, System.Func<Color, bool> predicate)
        {
            int count = 0;
            foreach (Color pixel in texture.GetPixels())
            {
                if (predicate(pixel))
                    count++;
            }

            return count;
        }
    }
}
