#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
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
    public class CodecksUIToolkitVisualTests
    {
        [UnityTest]
        public IEnumerator WhiteTextAndUndistortedLogo_NormalWindow() => Inspect(1920, 1080, "normal");

        [UnityTest]
        public IEnumerator WhiteTextAndUndistortedLogo_SmallWindow() => Inspect(1366, 768, "small");

        private IEnumerator Inspect(int width, int height, string label)
        {
            using var size = new TemporaryGameViewSize(width, height);
            string scene = AssetDatabase.FindAssets("CodecksUIToolkitFeedbackReporterScene t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath).Single(p => p.StartsWith("Assets/Samples/"));
            EditorSceneManager.LoadSceneInPlayMode(scene, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            var controller = UnityEngine.Object.FindAnyObjectByType<CodecksUIToolkitFeedbackController>();
            controller.ShowCodecksForm();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            var root = (VisualElement)typeof(CodecksUIToolkitFeedbackController).GetField("root", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var texts = root.Query<TextElement>().ToList();
            Assert.That(texts.Count, Is.GreaterThan(8));
            foreach (var text in texts)
                Assert.That(text.resolvedStyle.color, Is.EqualTo(Color.white), string.Join(",", text.GetClasses()));
            foreach (string name in new[] { "codecks-feedback-report", "codecks-feedback-email" })
            {
                var input = root.Q<TextField>(name).Q(className: "unity-text-field__input");
                Assert.That(input.resolvedStyle.color, Is.EqualTo(Color.white));
                Assert.That(input.resolvedStyle.backgroundColor.r, Is.LessThan(0.5f), "White text must have a dark input background.");
            }
            Assert.That(root.Q<DropdownField>("codecks-feedback-severity").Q<TextElement>().resolvedStyle.color, Is.EqualTo(Color.white));
            var brand = root.Q("codecks-feedback-brand");
            var texture = brand.resolvedStyle.backgroundImage.texture;
            Assert.That(texture.width, Is.EqualTo(1676), "Do not rescale NPOT logo width during import.");
            Assert.That(texture.height, Is.EqualTo(512));
            Assert.That(brand.resolvedStyle.backgroundSize.sizeType, Is.EqualTo(BackgroundSizeType.Contain));
            root.Q<ScrollView>().ScrollTo(brand);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            Assert.That(brand.worldBound.yMin, Is.GreaterThanOrEqualTo(0));
            Assert.That(brand.worldBound.yMax, Is.LessThanOrEqualTo(root.worldBound.height));
            Directory.CreateDirectory("Evidence/Tweaks");
            File.WriteAllLines("Evidence/Tweaks/" + label + "-resolved-styles.txt",
                texts.Select(t => string.Join(",", t.GetClasses()) + "=" + ColorUtility.ToHtmlStringRGBA(t.resolvedStyle.color))
                .Concat(new[] { "logo-source=1676x512", "logo-imported=" + texture.width + "x" + texture.height, "background-size=" + brand.resolvedStyle.backgroundSize.sizeType }));
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            var source = new Texture2D(2, 2);
            try
            {
                Assert.That(frame.width, Is.EqualTo(width));
                Assert.That(frame.height, Is.EqualTo(height));
                File.WriteAllBytes("Evidence/Tweaks/" + label + ".png", frame.EncodeToPNG());
                ImageConversion.LoadImage(source, File.ReadAllBytes(AssetDatabase.GetAssetPath(texture)));
                RectInt originalMark = ColoredMarkBounds(source, new RectInt(0, 0, source.width, source.height));
                float scale = frame.width / root.worldBound.width;
                Rect b = brand.worldBound;
                var pixelBounds = new RectInt((int)(b.xMin * scale), frame.height - (int)(b.yMax * scale), (int)(b.width * scale), (int)(b.height * scale));
                RectInt renderedMark = ColoredMarkBounds(frame, pixelBounds);
                Assert.That(renderedMark.width / (float)renderedMark.height,
                    Is.EqualTo(originalMark.width / (float)originalMark.height).Within(0.12f), "Rendered colored logo geometry must preserve the source aspect.");
            }
            finally
            {
                UnityEngine.Object.Destroy(frame);
                UnityEngine.Object.Destroy(source);
                controller.HideCodecksForm();
            }
        }

        private static RectInt ColoredMarkBounds(Texture2D image, RectInt region)
        {
            int minX = image.width, minY = image.height, maxX = -1, maxY = -1;
            for (int y = Mathf.Max(0, region.yMin); y < Mathf.Min(image.height, region.yMax); y++)
            for (int x = Mathf.Max(0, region.xMin); x < Mathf.Min(image.width, region.xMax); x++)
            {
                var p = image.GetPixel(x, y);
                bool red = p.r > 0.5f && p.r > p.g * 1.4f && p.r > p.b * 1.3f;
                bool cyan = p.g > 0.5f && p.b > 0.5f && p.r < p.g * 0.8f;
                if (p.a < 0.5f || (!red && !cyan)) continue;
                minX = Mathf.Min(minX, x); minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x); maxY = Mathf.Max(maxY, y);
            }
            Assert.That(maxX, Is.GreaterThan(minX), "Actual colored logo must be visible.");
            Assert.That(maxY, Is.GreaterThan(minY));
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private sealed class TemporaryGameViewSize : IDisposable
        {
            private readonly EditorWindow view;
            private readonly PropertyInfo selection;
            private readonly int previous;
            public TemporaryGameViewSize(int width, int height)
            {
                var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                view = Resources.FindObjectsOfTypeAll(type).Cast<EditorWindow>().First(); // Never focus or create a window.
                selection = type.GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                previous = (int)selection.GetValue(view);
                // Existing Unity 6000.6 standalone Full HD and WXGA sizes; no custom sizes/settings created.
                selection.SetValue(view, width == 1920 ? 3 : 4);
            }
            public void Dispose() => selection.SetValue(view, previous);
        }
    }
}
#endif