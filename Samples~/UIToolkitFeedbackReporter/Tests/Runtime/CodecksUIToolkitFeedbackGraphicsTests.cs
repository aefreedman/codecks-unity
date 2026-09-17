using System;
using System.Collections;
using System.Reflection;
using Codecks.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Codecks.Samples.UIToolkitFeedbackReporter.Tests
{
    public class CodecksUIToolkitFeedbackGraphicsTests
    {
        [UnityTest]
        public IEnumerator ShowCodecksForm_CapturesFrameBeforeShowingOverlay()
        {
            var host = new GameObject("Codecks UI Toolkit graphics test");
            Texture2D decoded = null;
            try
            {
                var creator = host.AddComponent<CodecksCardCreator>();
                host.AddComponent<PanelRenderer>();
                var controller = host.AddComponent<CodecksUIToolkitFeedbackController>();
                Bind(controller, creator, CreateRequiredRoot(), 1);

                controller.ShowCodecksForm();
                yield return new WaitForEndOfFrame();
                yield return null;

                var overlay = GetPrivate<VisualElement>(controller, "overlay");
                byte[] screenshot = GetPrivate<byte[]>(controller, "queuedScreenshot");
                Assert.That(overlay.style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(screenshot, Is.Not.Null.And.Not.Empty);
                decoded = new Texture2D(2, 2);
                Assert.That(ImageConversion.LoadImage(decoded, screenshot), Is.True);
                Assert.That(decoded.width, Is.GreaterThan(1));
                Assert.That(decoded.height, Is.GreaterThan(1));
            }
            finally
            {
                if (decoded != null)
                    UnityEngine.Object.Destroy(decoded);
                UnityEngine.Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator ShowCodecksForm_WhenCaptureIsUnavailable_ShowsOverlayWithoutAttachment()
        {
            var host = new GameObject("Codecks UI Toolkit unavailable capture test");
            try
            {
                var creator = host.AddComponent<CodecksCardCreator>();
                host.AddComponent<PanelRenderer>();
                var controller = host.AddComponent<UnavailableCaptureController>();
                Bind(controller, creator, CreateRequiredRoot(), 1);

                controller.ShowCodecksForm();
                yield return new WaitForEndOfFrame();
                yield return null;

                Assert.That(GetPrivate<VisualElement>(controller, "overlay").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                Assert.That(GetPrivate<byte[]>(controller, "queuedScreenshot"), Is.Null);
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
            }
        }

        [UnityTest]
        public IEnumerator SuccessfulSubmission_DismissesWhileTimeScaleIsPaused()
        {
            var host = new GameObject("Codecks UI Toolkit paused dismissal test");
            float originalTimeScale = Time.timeScale;
            try
            {
                var creator = host.AddComponent<CodecksCardCreator>();
                host.AddComponent<PanelRenderer>();
                var controller = host.AddComponent<CodecksUIToolkitFeedbackController>();
                Bind(controller, creator, CreateRequiredRoot(), 1);
                GetPrivate<VisualElement>(controller, "overlay").style.display = DisplayStyle.Flex;

                Time.timeScale = 0f;
                typeof(CodecksUIToolkitFeedbackController)
                    .GetMethod("HandleSubmissionResult", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { 1, true, "card" });
                yield return new WaitForSecondsRealtime(1.1f);

                Assert.That(GetPrivate<VisualElement>(controller, "overlay").style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                UnityEngine.Object.Destroy(host);
            }
        }

        private static void Bind(CodecksUIToolkitFeedbackController controller, CodecksCardCreator creator, VisualElement root, int version)
        {
            SetPrivate(controller, "cardCreator", creator);
            typeof(CodecksUIToolkitFeedbackController)
                .GetMethod("OnUIReload", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, new object[] { null, root, version });
        }

        private static VisualElement CreateRequiredRoot()
        {
            var root = new VisualElement();
            var overlay = new VisualElement { name = "codecks-feedback-overlay" };
            overlay.Add(new TextField { name = "codecks-feedback-report" });
            overlay.Add(new DropdownField { name = "codecks-feedback-severity" });
            overlay.Add(new TextField { name = "codecks-feedback-email" });
            overlay.Add(new Button { name = "codecks-feedback-send" });
            overlay.Add(new Button { name = "codecks-feedback-cancel" });
            overlay.Add(new Label { name = "codecks-feedback-status" });
            root.Add(new Button { name = "codecks-feedback-launcher" });
            root.Add(overlay);
            return root;
        }

        private static void SetPrivate(object target, string name, object value)
        {
            typeof(CodecksUIToolkitFeedbackController)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string name)
        {
            return (T)typeof(CodecksUIToolkitFeedbackController)
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(target);
        }

        private sealed class UnavailableCaptureController : CodecksUIToolkitFeedbackController
        {
            protected override byte[] CaptureScreenshot()
            {
                return null;
            }
        }
    }
}
