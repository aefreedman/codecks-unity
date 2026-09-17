using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Codecks.Runtime;
#if UNITY_EDITOR
using UnityEditor;
#endif
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

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator ImportedTemplate_PanelRendererReloadsAndDetachesOldBindings()
        {
            const string temporaryTemplatePath = "Assets/CodecksFeedbackReporterReloadTest.uxml";
            var host = new GameObject("Codecks UI Toolkit reload test");
            host.SetActive(false);
            UnityEngine.Object copiedSettings = null;
            try
            {
                string templatePath = AssetDatabase.FindAssets("CodecksFeedbackReporter t:VisualTreeAsset")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Single(path => path.StartsWith("Assets/Samples/"));
                string settingsPath = AssetDatabase.FindAssets("CodecksFeedbackPanelSettings t:PanelSettings")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Single(path => path.StartsWith("Assets/Samples/"));
                Assert.That(AssetDatabase.CopyAsset(templatePath, temporaryTemplatePath), Is.True);

                var renderer = host.AddComponent<PanelRenderer>();
                renderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(templatePath);
                renderer.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(settingsPath);
                var creator = host.AddComponent<CodecksCardCreator>();
                var controller = host.AddComponent<CodecksUIToolkitFeedbackController>();
                SetPrivate(controller, "cardCreator", creator);
                host.SetActive(true);
                yield return null;
                yield return null;

                var oldRoot = GetPrivate<VisualElement>(controller, "root");
                var oldLauncher = GetPrivate<Button>(controller, "launcherButton");
                Assert.That(oldRoot, Is.Not.Null);
                Assert.That(oldLauncher, Is.Not.Null);

                copiedSettings = UnityEngine.Object.Instantiate(renderer.panelSettings);
                renderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(temporaryTemplatePath);
                renderer.panelSettings = (PanelSettings)copiedSettings;
                yield return null;
                yield return null;

                var newRoot = GetPrivate<VisualElement>(controller, "root");
                var newLauncher = GetPrivate<Button>(controller, "launcherButton");
                Assert.That(newRoot, Is.Not.Null.And.Not.SameAs(oldRoot));
                Assert.That(newLauncher, Is.Not.Null.And.Not.SameAs(oldLauncher));
                int sessionBeforeOldClick = GetPrivate<int>(controller, "session");
                oldLauncher.SendEvent(new ClickEvent());
                Assert.That(GetPrivate<int>(controller, "session"), Is.EqualTo(sessionBeforeOldClick));

                newLauncher.SendEvent(new ClickEvent());
                yield return new WaitForEndOfFrame();
                Assert.That(GetPrivate<VisualElement>(controller, "overlay").style.display.value, Is.EqualTo(DisplayStyle.Flex));
                GetPrivate<Button>(controller, "cancelButton").SendEvent(new ClickEvent());
                Assert.That(GetPrivate<VisualElement>(controller, "overlay").style.display.value, Is.EqualTo(DisplayStyle.None));

                controller.enabled = false;
                renderer.enabled = false;
                yield return null;
                renderer.enabled = true;
                controller.enabled = true;
                yield return null;
                yield return null;
                Assert.That(GetPrivate<VisualElement>(controller, "root"), Is.Not.Null);

                LogAssert.Expect(LogType.Error, "Codecks UI Toolkit feedback reporter received an empty Panel Renderer root.");
                renderer.visualTreeAsset = null;
                yield return null;
                Assert.That(GetPrivate<VisualElement>(controller, "root"), Is.Null);
                renderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(temporaryTemplatePath);
                yield return null;
                yield return null;
                Assert.That(GetPrivate<VisualElement>(controller, "root"), Is.Not.Null);
            }
            finally
            {
                if (copiedSettings != null)
                    UnityEngine.Object.Destroy(copiedSettings);
                UnityEngine.Object.Destroy(host);
                AssetDatabase.DeleteAsset(temporaryTemplatePath);
            }
        }
#endif

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
