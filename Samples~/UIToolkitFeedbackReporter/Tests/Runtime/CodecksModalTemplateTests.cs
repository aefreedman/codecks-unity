#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Codecks.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Codecks.Samples.UIToolkitFeedbackReporter.Tests
{
    public class CodecksModalTemplateTests
    {
        [UnityTest]
        public IEnumerator FormOnly_BindsWithoutLauncher_AndBlocksPointerBubbling() => Inspect(false);

        [UnityTest]
        public IEnumerator Standalone_ComposesOneForm_AndBlocksPointerBubbling() => Inspect(true);

        private IEnumerator Inspect(bool standalone)
        {
            var host = new GameObject("Codecks modal template test");
            host.SetActive(false);
            PanelSettings settings = null;
            try
            {
                string name = standalone ? "CodecksFeedbackReporter" : "CodecksFeedbackForm";
                string path = AssetDatabase.FindAssets(name + " t:VisualTreeAsset")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Single(p => p.StartsWith("Assets/Samples/") && p.EndsWith("/" + name + ".uxml"));
                string settingsPath = AssetDatabase.FindAssets("CodecksFeedbackPanelSettings t:PanelSettings")
                    .Select(AssetDatabase.GUIDToAssetPath).Single(p => p.StartsWith("Assets/Samples/"));
                settings = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PanelSettings>(settingsPath));
                var renderer = host.AddComponent<PanelRenderer>();
                renderer.panelSettings = settings;
                renderer.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(path);
                var creator = host.AddComponent<CodecksCardCreator>();
                var controller = host.AddComponent<CodecksUIToolkitFeedbackController>();
                typeof(CodecksUIToolkitFeedbackController).GetField("cardCreator", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, creator);
                host.SetActive(true);
                yield return null;
                yield return null;
                var root = (VisualElement)typeof(CodecksUIToolkitFeedbackController)
                    .GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                Assert.That(root, Is.Not.Null);
                Assert.That(root.panel, Is.Not.Null, "Use a real runtime panel, not an unattached test tree.");
                Assert.That(root.Query<VisualElement>("codecks-feedback-overlay").ToList().Count, Is.EqualTo(1));
                Assert.That(root.Q<Button>("codecks-feedback-launcher") != null, Is.EqualTo(standalone));
                foreach (var wrapper in root.Query<VisualElement>(className: "codecks-feedback-root").ToList())
                    Assert.That(wrapper.pickingMode, Is.EqualTo(PickingMode.Ignore));
                var instance = root.Q<TemplateContainer>();
                if (standalone) Assert.That(instance.pickingMode, Is.EqualTo(PickingMode.Ignore));
                var overlay = root.Q("codecks-feedback-overlay");
                Assert.That(overlay.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
                controller.ShowCodecksFormWithoutScreenshot();
                yield return null;
                Assert.That(controller.State, Is.EqualTo(CodecksFormState.Open));
                Assert.That(overlay.pickingMode, Is.EqualTo(PickingMode.Position));
                Assert.That(overlay.worldBound.width, Is.EqualTo(root.worldBound.width).Within(1));
                Assert.That(overlay.worldBound.height, Is.EqualTo(root.worldBound.height).Within(1));
                Assert.That(root.panel.Pick(new Vector2(overlay.worldBound.xMin + 1, overlay.worldBound.yMin + 1)), Is.SameAs(overlay),
                    "The blank full-screen modal area must block underlying picking.");
                Assert.That(root.Q("codecks-feedback-brand").resolvedStyle.backgroundImage.texture, Is.Not.Null);

                int parentEvents = 0, fieldEvents = 0, sameTargetEvents = 0;
                root.RegisterCallback<PointerDownEvent>(_ => parentEvents++);
                root.RegisterCallback<PointerUpEvent>(_ => parentEvents++);
                root.RegisterCallback<PointerMoveEvent>(_ => parentEvents++);
                root.RegisterCallback<MouseDownEvent>(_ => parentEvents++);
                root.RegisterCallback<MouseUpEvent>(_ => parentEvents++);
                root.RegisterCallback<ClickEvent>(_ => parentEvents++);
                root.RegisterCallback<WheelEvent>(_ => parentEvents++);
                overlay.RegisterCallback<PointerDownEvent>(_ => sameTargetEvents++);
                var field = root.Q<TextField>("codecks-feedback-report");
                field.RegisterCallback<PointerDownEvent>(_ => fieldEvents++);
                var input = field.Q(className: "unity-text-field__input");
                Send<PointerDownEvent>(input);
                Send<PointerUpEvent>(input);
                Send<PointerMoveEvent>(input);
                Send<MouseDownEvent>(input);
                Send<MouseUpEvent>(input);
                Send<ClickEvent>(input);
                Send<WheelEvent>(input);
                Assert.That(parentEvents, Is.Zero);
                Assert.That(fieldEvents, Is.EqualTo(1), "Internal descendant handlers must still run.");
                Assert.That(sameTargetEvents, Is.EqualTo(1), "Do not stop other overlay handlers immediately.");
                field.value = "Internal editing remains enabled";
                field.Focus();
                Assert.That(field.value, Is.EqualTo("Internal editing remains enabled"));
                Assert.That(root.panel.focusController.focusedElement, Is.Not.Null);

                int buttonClicks = 0;
                var cancel = root.Q<Button>("codecks-feedback-cancel");
                cancel.clicked += () => buttonClicks++;
                root.Q<ScrollView>().ScrollTo(cancel);
                yield return null;
                var position = cancel.worldBound.center;
                using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = position }))
                { down.target = cancel; cancel.SendEvent(down); }
                using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = position }))
                { up.target = cancel; cancel.SendEvent(up); }
                Assert.That(buttonClicks, Is.EqualTo(1), "Pointer boundary must preserve internal button actions.");
                Assert.That(controller.State, Is.EqualTo(CodecksFormState.Closed));
                Assert.That(parentEvents, Is.Zero);

                // Teardown removes boundaries from stale trees instead of accumulating handlers.
                controller.enabled = false;
                Send<PointerDownEvent>(overlay);
                Assert.That(parentEvents, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.Destroy(host);
                if (settings != null) UnityEngine.Object.Destroy(settings);
            }
        }

        private static void Send<T>(VisualElement target) where T : EventBase<T>, new()
        {
            using var evt = EventBase<T>.GetPooled();
            evt.target = target;
            target.SendEvent(evt);
        }
    }
}
#endif
