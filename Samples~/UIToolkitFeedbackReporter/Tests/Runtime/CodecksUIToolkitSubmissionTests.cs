#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Codecks.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Codecks.Samples.UIToolkitFeedbackReporter.Tests
{
    public class CodecksUIToolkitSubmissionTests
    {
        [UnityTest]
        public IEnumerator RealController_CapturesRenderedBackgroundAndCompletesLocalhostUpload()
        {
            yield return Submit(false, false);
        }

        [UnityTest]
        public IEnumerator RealController_AttachmentFailureReportsPartialSuccess()
        {
            yield return Submit(true, false);
        }

        [UnityTest]
        public IEnumerator RealController_LateCompletionDoesNotOverwriteReopenedSession()
        {
            yield return Submit(false, true);
        }

        private IEnumerator Submit(bool rejectUpload, bool reopen)
        {
            string path = AssetDatabase.FindAssets("CodecksUIToolkitFeedbackReporterScene t:Scene")
                .Select(AssetDatabase.GUIDToAssetPath).Single(p => p.StartsWith("Assets/Samples/"));
            EditorSceneManager.LoadSceneInPlayMode(path, new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
            yield return null;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            var controller = UnityEngine.Object.FindAnyObjectByType<CodecksUIToolkitFeedbackController>();
            var creator = UnityEngine.Object.FindAnyObjectByType<CodecksCardCreator>();
            Assert.That(UnityEngine.Camera.allCamerasCount, Is.EqualTo(1), "Standalone sample must render without manual setup.");
            using var server = new LocalServer(rejectUpload);
            creator.codecksURL = server.Url + "create";
            creator.defaultToken = "local-test-only";
            controller.ShowCodecksForm();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            var root = Field<VisualElement>(controller, "root");
            var bytes = Field<byte[]>(controller, "queuedScreenshot");
            Directory.CreateDirectory("Evidence");
            File.WriteAllBytes("Evidence/mock-captured-attachment.jpg", bytes);
            Assert.That(Camera.main.backgroundColor.r, Is.EqualTo(0.4901961f).Within(0.001f));
            AssertRenderedBackground(bytes);
            var brand = root.Q("codecks-feedback-brand");
            Assert.That(brand.resolvedStyle.backgroundImage.texture, Is.Not.Null);
            Assert.That(brand.resolvedStyle.height, Is.GreaterThan(40));
            Assert.That(brand.worldBound.width, Is.GreaterThan(100));
            AssertRenderedLogo(root, brand);
            Directory.CreateDirectory("Evidence");
            File.WriteAllBytes("Evidence/mock-captured-attachment.jpg", bytes);
            ScreenCapture.CaptureScreenshot("Evidence/mock-form-with-logo.png");
            root.Q<TextField>("codecks-feedback-report").value = "Local controller submission regression";
            Click(root.Q<Button>("codecks-feedback-send"));
            Click(root.Q<Button>("codecks-feedback-send"));
            Assert.That(root.Q<Label>("codecks-feedback-status").text, Is.EqualTo("Sending report..."));
            float deadline = Time.realtimeSinceStartup + 10;
            while (!server.UploadReceived && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(server.UploadReceived, Is.True, "Actual multipart upload should reach localhost.");
            if (reopen)
            {
                controller.HideCodecksForm();
                controller.ShowCodecksForm();
                yield return new WaitForEndOfFrame();
                yield return null;
            }
            if (rejectUpload)
                LogAssert.Expect(LogType.Warning, "Codecks UI Toolkit report submission failed: error uploading file screenshot.jpg: ProtocolError");
            server.ReleaseUpload = true;
            while (Field<bool>(controller, "submissionInFlight") && Time.realtimeSinceStartup < deadline)
            {
                if (root.Q<Label>("codecks-feedback-status").text.StartsWith("Thank you!"))
                    break;
                yield return null;
            }
            // Late callbacks are intentionally ignored after cancel/reopen.
            if (reopen)
                yield return new WaitForSecondsRealtime(0.3f);
            string status = root.Q<Label>("codecks-feedback-status").text;
            if (reopen)
                Assert.That(status, Is.Empty);
            else if (rejectUpload)
            {
                Assert.That(status, Does.StartWith("Report created, but the screenshot upload failed."));
                Assert.That(root.Q<Button>("codecks-feedback-send").enabledSelf, Is.True);
            }
            else
            {
                Assert.That(status, Is.EqualTo("Thank you! Your report was sent."));
                ScreenCapture.CaptureScreenshot("Evidence/mock-success-ui.png");
            }
            Assert.That(server.CreateCount, Is.EqualTo(1), "Double send must not create duplicate cards.");
            Assert.That(server.UploadCount, Is.EqualTo(1));
            var active = creator.GetType().GetField("activeOperations", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(creator);
            Assert.That((int)active.GetType().GetProperty("Count").GetValue(active), Is.Zero, "Completion must release the operation exactly once.");
            byte[] uploaded = ExtractJpeg(server.UploadBody);
            Assert.That(uploaded, Is.EqualTo(bytes), "Upload must contain exactly the captured image, not a second capture of the form.");
            AssertRenderedBackground(uploaded);
            if (!rejectUpload && !reopen)
            {
                yield return new WaitForSecondsRealtime(1.1f);
                Assert.That(root.Q("codecks-feedback-overlay").style.display.value, Is.EqualTo(DisplayStyle.None));
            }
            controller.HideCodecksForm();
        }

        private static void AssertRenderedBackground(byte[] bytes)
        {
            var decoded = new Texture2D(2, 2);
            try
            {
                Assert.That(ImageConversion.LoadImage(decoded, bytes), Is.True);
                Assert.That(decoded.width, Is.EqualTo(Screen.width));
                Assert.That(decoded.height, Is.EqualTo(Screen.height));
                // The form would cover the centre-left sample, while the launcher does not.
                foreach (var position in new[] { new Vector2(0.05f, 0.05f), new Vector2(0.4f, 0.5f), new Vector2(0.95f, 0.95f) })
                {
                    var pixel = decoded.GetPixel((int)(position.x * decoded.width), (int)(position.y * decoded.height));
                    Assert.That(pixel.r, Is.EqualTo(0.49f).Within(0.04f));
                    Assert.That(pixel.g, Is.EqualTo(0.365f).Within(0.04f));
                    Assert.That(pixel.b, Is.EqualTo(0.663f).Within(0.04f));
                }
            }
            finally { UnityEngine.Object.Destroy(decoded); }
        }

        private static void AssertRenderedLogo(VisualElement root, VisualElement brand)
        {
            var frame = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                float scale = frame.width / root.worldBound.width;
                Rect bounds = brand.worldBound;
                int red = 0, cyan = 0;
                for (int x = Mathf.Max(0, (int)(bounds.xMin * scale)); x < Mathf.Min(frame.width, (int)(bounds.xMax * scale)); x++)
                for (int y = Mathf.Max(0, (int)(bounds.yMin * scale)); y < Mathf.Min(frame.height, (int)(bounds.yMax * scale)); y++)
                {
                    Color pixel = frame.GetPixel(x, frame.height - 1 - y);
                    if (pixel.r > 0.5f && pixel.r > pixel.g * 1.4f && pixel.r > pixel.b * 1.3f) red++;
                    if (pixel.g > 0.5f && pixel.b > 0.5f && pixel.r < pixel.g * 0.8f) cyan++;
                }
                Assert.That(red, Is.GreaterThan(3), "Actual Codecks logo red pixels must be rendered, not just referenced.");
                Assert.That(cyan, Is.GreaterThan(3), "Actual Codecks logo cyan pixels must be rendered.");
            }
            finally { UnityEngine.Object.Destroy(frame); }
        }

        private static byte[] ExtractJpeg(byte[] body)
        {
            int start = -1, end = -1;
            for (int i = 0; i < body.Length - 1; i++)
            {
                if (start < 0 && body[i] == 255 && body[i + 1] == 216) start = i;
                if (start >= 0 && body[i] == 255 && body[i + 1] == 217) { end = i + 2; break; }
            }
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return body.Skip(start).Take(end - start).ToArray();
        }

        private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Click(Button button) => button.clickable.GetType().GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(button.clickable, new object[] { null });

        private sealed class LocalServer : IDisposable
        {
            private readonly HttpListener listener = new HttpListener();
            private readonly Task worker;
            public string Url { get; }
            public volatile bool UploadReceived;
            public volatile bool ReleaseUpload;
            public int CreateCount;
            public int UploadCount;
            public byte[] UploadBody;

            public LocalServer(bool reject)
            {
                var portProbe = new TcpListener(IPAddress.Loopback, 0);
                portProbe.Start();
                int port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
                portProbe.Stop();
                Url = "http://127.0.0.1:" + port + "/";
                listener.Prefixes.Add(Url);
                listener.Start();
                worker = Task.Run(async () =>
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var context = await listener.GetContextAsync();
                        using var body = new MemoryStream();
                        await context.Request.InputStream.CopyToAsync(body);
                        if (context.Request.Url.AbsolutePath == "/create")
                        {
                            CreateCount++;
                            string request = Encoding.UTF8.GetString(body.ToArray());
                            if (!request.Contains("screenshot.jpg")) throw new InvalidOperationException("Missing screenshot request");
                            byte[] response = Encoding.UTF8.GetBytes("{\"ok\":true,\"cardId\":\"local-card\",\"uploadUrls\":[{\"fileName\":\"screenshot.jpg\",\"url\":\"" + Url + "upload\",\"fields\":{}}]}");
                            context.Response.ContentType = "application/json";
                            await context.Response.OutputStream.WriteAsync(response, 0, response.Length);
                        }
                        else
                        {
                            UploadCount++;
                            UploadBody = body.ToArray();
                            UploadReceived = true;
                            while (!ReleaseUpload && listener.IsListening) await Task.Delay(10);
                            context.Response.StatusCode = reject ? 500 : 204;
                        }
                        context.Response.Close();
                    }
                });
            }
            public void Dispose()
            {
                ReleaseUpload = true;
                listener.Close();
                if (worker.IsFaulted) throw worker.Exception;
            }
        }
    }
}
#endif
