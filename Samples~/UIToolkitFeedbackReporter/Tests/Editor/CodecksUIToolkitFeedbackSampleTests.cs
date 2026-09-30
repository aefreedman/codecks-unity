using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Codecks.Samples.UIToolkitFeedbackReporter.Tests
{
    public class CodecksUIToolkitFeedbackSampleTests
    {
        [Test]
        public void OnDisable_InvalidatesPendingSessionAndSubmission()
        {
            var host = new GameObject("Codecks lifecycle test");
            try
            {
                host.AddComponent<PanelRenderer>();
                var controller = host.AddComponent<CodecksUIToolkitFeedbackController>();
                SetPrivate(controller, "session", 7);
                SetPrivate(controller, "overlayOpen", true);
                SetPrivate(controller, "submissionInFlight", true);

                InvokePrivate(controller, "OnDisable");

                Assert.That(GetPrivate<int>(controller, "session"), Is.EqualTo(8));
                Assert.That(GetPrivate<bool>(controller, "overlayOpen"), Is.False);
                Assert.That(GetPrivate<bool>(controller, "submissionInFlight"), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ShowCodecksForm_WhenAlreadyOpen_DoesNotStartAnotherCaptureSession()
        {
            var host = new GameObject("Codecks lifecycle test");
            try
            {
                var creator = host.AddComponent<Codecks.Runtime.CodecksCardCreator>();
                host.AddComponent<PanelRenderer>();
                var controller = host.AddComponent<CodecksUIToolkitFeedbackController>();
                SetPrivate(controller, "root", new VisualElement());
                SetPrivate(controller, "overlay", new VisualElement());
                SetPrivate(controller, "cardCreator", creator);
                SetPrivate(controller, "overlayOpen", true);
                SetPrivate(controller, "session", 3);

                controller.ShowCodecksForm();

                Assert.That(GetPrivate<int>(controller, "session"), Is.EqualTo(3));
                Assert.That(GetPrivate<object>(controller, "showCoroutine"), Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void NullRootReload_DetachesOldBindings_AndValidReloadRecovers()
        {
            var host = new GameObject("Codecks reload test");
            try
            {
                var creator = host.AddComponent<Codecks.Runtime.CodecksCardCreator>();
                host.AddComponent<PanelRenderer>();
                var controller = host.AddComponent<CodecksUIToolkitFeedbackController>();
                var oldLauncher = new Button();
                SetPrivate(controller, "root", new VisualElement());
                SetPrivate(controller, "launcherButton", oldLauncher);
                SetPrivate(controller, "session", 4);
                oldLauncher.clicked += controller.ShowCodecksForm;

                LogAssert.Expect(LogType.Error, "Codecks UI Toolkit feedback reporter received an empty Panel Renderer root.");
                InvokePrivate(controller, "OnUIReload", null, null, 5);

                Assert.That(GetPrivate<object>(controller, "root"), Is.Null);
                Assert.That(GetPrivate<object>(controller, "launcherButton"), Is.Null);
                oldLauncher.SendEvent(new ClickEvent());
                Assert.That(GetPrivate<int>(controller, "session"), Is.EqualTo(5));

                var validRoot = CreateRequiredRoot();
                SetPrivate(controller, "cardCreator", creator);
                InvokePrivate(controller, "OnUIReload", null, validRoot, 6);

                Assert.That(GetPrivate<VisualElement>(controller, "root"), Is.SameAs(validRoot));
                Assert.That(GetPrivate<Button>(controller, "launcherButton"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ImportedScene_HasPanelRendererAndRequiredBindings()
        {
            string scenePath = AssetDatabase.FindAssets("CodecksUIToolkitFeedbackReporterScene t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Single(path => path.StartsWith("Assets/Samples/"));
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var host = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.name == "Codecks UI Toolkit Feedback Reporter");
            var renderer = host.GetComponent<PanelRenderer>();

            Assert.That(scene.IsValid(), Is.True);
            Assert.That(Camera.main, Is.Not.Null, "Standalone scene must include a rendering camera.");
            Assert.That(Camera.main.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.visualTreeAsset, Is.Not.Null);
            Assert.That(renderer.panelSettings, Is.Not.Null);
            Assert.That(renderer.panelSettings.themeStyleSheet, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(renderer.panelSettings.themeStyleSheet), Does.Contain("Feedback Reporter (UI Toolkit)/CodecksDefaultRuntimeTheme.tss"));
            Assert.That(host.GetComponent<CodecksUIToolkitFeedbackController>(), Is.Not.Null);
            Assert.That(host.GetComponent<Codecks.Runtime.CodecksCardCreator>(), Is.Not.Null);
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

        private static void SetPrivate(object target, string fieldName, object value)
        {
            target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static T GetPrivate<T>(object target, string fieldName)
        {
            return (T)target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }
    }
}
