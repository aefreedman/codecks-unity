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
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

namespace Codecks.Samples.UIToolkitFeedbackReporter.Tests
{
    public class CodecksFormCustomizationTests
    {
        [UnityTest] public IEnumerator UGUI_ProvidedPNG() => Submit(false, CodecksCardCreator.CodecksFileType.PNG);
        [UnityTest] public IEnumerator UGUI_ProvidedJPG() => Submit(false, CodecksCardCreator.CodecksFileType.JPG);
        [UnityTest] public IEnumerator UGUI_NoScreenshot() => Submit(false, null);
        [UnityTest] public IEnumerator Toolkit_ProvidedPNG_WithoutLauncher() => Submit(true, CodecksCardCreator.CodecksFileType.PNG);
        [UnityTest] public IEnumerator Toolkit_ProvidedJPG_WithoutLauncher() => Submit(true, CodecksCardCreator.CodecksFileType.JPG);
        [UnityTest] public IEnumerator Toolkit_NoScreenshot_WithoutLauncher() => Submit(true, null);
        [UnityTest] public IEnumerator UGUI_DefaultAndSupersededSessions() => Sessions(false);
        [UnityTest] public IEnumerator Toolkit_DefaultAndSupersededSessions_WithoutLauncher() => Sessions(true);
        [Test] public void UGUI_InvalidImagesFailBeforeOpening() => InvalidImages(false);
        [Test] public void Toolkit_InvalidImagesFailBeforeOpening() => InvalidImages(true);

        private IEnumerator Submit(bool toolkit, CodecksCardCreator.CodecksFileType? encoding)
        {
            using var form = new Form(toolkit);
            using var server = new LocalServer();
            form.Settings.endpoint = server.Url + "create";
            Texture2D callerTexture = null;
            byte[] bytes = null;
            try
            {
                if (encoding.HasValue)
                {
                    callerTexture = new Texture2D(2, 2);
                    callerTexture.SetPixels(new[] { Color.red, Color.green, Color.blue, Color.white });
                    callerTexture.Apply();
                    bytes = encoding == CodecksCardCreator.CodecksFileType.PNG ? callerTexture.EncodeToPNG() : callerTexture.EncodeToJPG();
                    form.Open(bytes, encoding.Value);
                    Assert.That(form.Bytes, Is.SameAs(bytes));
                }
                else form.OpenWithoutScreenshot();
                Assert.That(form.IsOpen, Is.True, "Explicit modes open synchronously without an EOF wait.");
                Assert.That(form.Captures, Is.Zero);
                form.SetReport("Caller-supplied report is long enough");
                form.Send();
                form.Send();
                float deadline = Time.realtimeSinceStartup + 8f;
                while (!form.Succeeded && Time.realtimeSinceStartup < deadline)
                {
                    if (server.Worker.IsFaulted) throw server.Worker.Exception;
                    yield return null;
                }
                Assert.That(form.Succeeded, Is.True);
                Assert.That(form.Captures, Is.Zero);
                Assert.That(server.CreateCount, Is.EqualTo(1), "Send remains guarded once per session.");
                Assert.That(server.CreateBody, Does.Contain("consumer metadata"));
                if (encoding.HasValue)
                {
                    string name = encoding == CodecksCardCreator.CodecksFileType.PNG ? "screenshot.png" : "screenshot.jpg";
                    string mime = encoding == CodecksCardCreator.CodecksFileType.PNG ? "image/png" : "image/jpeg";
                    Assert.That(server.CreateBody, Does.Contain(name));
                    Assert.That(server.UploadCount, Is.EqualTo(1));
                    Assert.That(Encoding.UTF8.GetString(server.UploadBody), Does.Contain(mime));
                    Assert.That(Enumerable.Range(0, server.UploadBody.Length - bytes.Length + 1)
                        .Any(i => server.UploadBody.Skip(i).Take(bytes.Length).SequenceEqual(bytes)), Is.True,
                        "The multipart upload must contain the caller's exact encoded bytes.");
                    Assert.That(callerTexture != null, Is.True, "Controller must not own a caller's texture.");
                }
                else
                {
                    Assert.That(server.CreateBody, Does.Contain("\"fileNames\":[]"));
                    Assert.That(server.UploadCount, Is.Zero);
                    Assert.That(form.Bytes, Is.Null);
                }
                byte[] copy = bytes?.ToArray();
                form.Close();
                if (bytes != null) Assert.That(bytes, Is.EqualTo(copy), "Closing must not mutate caller data.");
            }
            finally { if (callerTexture != null) UnityEngine.Object.Destroy(callerTexture); }
        }

        private IEnumerator Sessions(bool toolkit)
        {
            using var form = new Form(toolkit);
            form.OpenDefault();
            Assert.That(form.IsOpen, Is.False);
            Assert.That(form.Captures, Is.Zero);
            yield return new WaitForEndOfFrame();
            yield return null;
            Assert.That(form.IsOpen, Is.True);
            Assert.That(form.Captures, Is.EqualTo(1));
            form.OpenDefault();
            Assert.That(form.Captures, Is.EqualTo(1));
            int oldSession = form.Session;
            form.Close();
            form.OpenDefault();
            var callerBytes = new byte[] { 1, 2, 3 };
            form.Open(callerBytes, CodecksCardCreator.CodecksFileType.PNG);
            Assert.That(form.IsOpen, Is.True);
            yield return new WaitForEndOfFrame();
            yield return null;
            Assert.That(form.Captures, Is.EqualTo(1), "Supplied opening invalidates pending default capture.");
            Assert.That(form.Bytes, Is.SameAs(callerBytes));
            form.Close();
            form.OpenDefault();
            form.Close();
            form.OpenWithoutScreenshot();
            yield return new WaitForEndOfFrame();
            yield return null;
            Assert.That(form.Captures, Is.EqualTo(1), "Close/no-image reopen invalidates delayed capture.");
            Assert.That(form.Bytes, Is.Null);
            form.Deliver(oldSession);
            Assert.That(form.Status, Is.Empty, "Stale completion cannot overwrite the new opening.");
            form.Deliver(form.Session);
            form.OpenWithoutScreenshot();
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(form.IsOpen, Is.True, "Old success dismissal cannot close a newer explicit opening.");
        }

        private void InvalidImages(bool toolkit)
        {
            using var form = new Form(toolkit);
            Assert.Throws<ArgumentNullException>(() => form.Open(null, CodecksCardCreator.CodecksFileType.PNG));
            Assert.Throws<ArgumentException>(() => form.Open(Array.Empty<byte>(), CodecksCardCreator.CodecksFileType.JPG));
            Assert.Throws<ArgumentOutOfRangeException>(() => form.Open(new byte[] { 1 }, CodecksCardCreator.CodecksFileType.Binary));
            Assert.That(form.Captures, Is.Zero);
            Assert.That(form.IsOpen, Is.False);
        }

        private sealed class Form : IDisposable
        {
            private readonly GameObject backend = new GameObject("Customization dummy backend");
            private readonly GameObject host = new GameObject("Customization form");
            private readonly ProbeUGUI ugui;
            private readonly ProbeToolkit toolkit;
            private readonly VisualElement root;
            private readonly Type baseType;
            private object Controller => (object)ugui ?? toolkit;
            public CodecksSettings Settings { get; }
            public int Captures => ugui != null ? ugui.Captures : toolkit.Captures;
            public byte[] Bytes => Field<byte[]>("queuedScreenshot");
            public int Session => Field<int>("session");
            public bool IsOpen => ugui != null ? host.activeSelf : root.Q("codecks-feedback-overlay").style.display.value == DisplayStyle.Flex;
            public string Status => ugui != null ? ugui.statusText.text : root.Q<Label>("codecks-feedback-status").text;
            public bool Succeeded => Status == "Sent" || Status == "Thank you! Your report was sent.";
            public Form(bool useToolkit)
            {
                var creator = backend.AddComponent<CodecksCardCreator>();
                Settings = ScriptableObject.CreateInstance<CodecksSettings>();
                Settings.reportToken = "local-customization-only";
                creator.settings = Settings;
                if (useToolkit)
                {
                    host.AddComponent<PanelRenderer>();
                    toolkit = host.AddComponent<ProbeToolkit>();
                    baseType = typeof(CodecksUIToolkitFeedbackController);
                    baseType.GetField("cardCreator", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(toolkit, creator);
                    root = new VisualElement(); // Deliberately no sample launcher.
                    var overlay = new VisualElement { name = "codecks-feedback-overlay" };
                    overlay.Add(new TextField { name = "codecks-feedback-report" });
                    overlay.Add(new DropdownField { name = "codecks-feedback-severity", value = "None" });
                    overlay.Add(new TextField { name = "codecks-feedback-email" });
                    overlay.Add(new Button { name = "codecks-feedback-send" });
                    overlay.Add(new Button { name = "codecks-feedback-cancel" });
                    overlay.Add(new Label { name = "codecks-feedback-status" });
                    root.Add(overlay);
                    baseType.GetMethod("OnUIReload", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(toolkit, new object[] { null, root, 1 });
                }
                else
                {
                    host.SetActive(false);
                    ugui = host.AddComponent<ProbeUGUI>();
                    baseType = typeof(CodecksCardCreatorForm);
                    ugui.cardCreator = creator;
                    ugui.textArea = Input("Report");
                    ugui.emailInput = Input("Email");
                    ugui.categoryDropdown = Child("Severity", typeof(TMP_Dropdown)).GetComponent<TMP_Dropdown>();
                    ugui.statusText = Child("Status", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                    ugui.sendButton = Child("Send", typeof(UnityEngine.UI.Button)).GetComponent<UnityEngine.UI.Button>();
                    ugui.statusSent = "Sent";
                }
            }
            private GameObject Child(string name, Type component)
            {
                var child = new GameObject(name, typeof(RectTransform), component);
                child.transform.SetParent(host.transform, false);
                return child;
            }
            private TMP_InputField Input(string name)
            {
                var input = Child(name, typeof(TMP_InputField)).GetComponent<TMP_InputField>();
                input.textComponent = Child(name + " text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
                return input;
            }
            private T Field<T>(string name) => (T)baseType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Controller);
            public void OpenDefault() { if (ugui != null) ugui.ShowCodecksForm(); else toolkit.ShowCodecksForm(); }
            public void Open(byte[] bytes, CodecksCardCreator.CodecksFileType type) { if (ugui != null) ugui.ShowCodecksForm(bytes, type); else toolkit.ShowCodecksForm(bytes, type); }
            public void OpenWithoutScreenshot() { if (ugui != null) ugui.ShowCodecksFormWithoutScreenshot(); else toolkit.ShowCodecksFormWithoutScreenshot(); }
            public void Close() { if (ugui != null) ugui.HideCodecksForm(); else toolkit.HideCodecksForm(); }
            public void SetReport(string text) { if (ugui != null) ugui.textArea.text = text; else root.Q<TextField>("codecks-feedback-report").value = text; }
            public void Send()
            {
                if (ugui != null) ugui.OnButtonSend();
                else root.Q<Button>("codecks-feedback-send").clickable.GetType().GetMethod("Invoke", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(root.Q<Button>("codecks-feedback-send").clickable, new object[] { null });
            }
            public void Deliver(int session) => baseType.GetMethod("HandleSubmissionResult", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Controller, new object[] { session, true, "dummy-card" });
            public void Dispose() { UnityEngine.Object.Destroy(host); UnityEngine.Object.Destroy(backend); UnityEngine.Object.Destroy(Settings); }
        }

        private sealed class LocalServer : IDisposable
        {
            private readonly HttpListener listener = new HttpListener();
            public Task Worker { get; }
            public string Url { get; }
            public string CreateBody;
            public byte[] UploadBody;
            public int CreateCount, UploadCount;
            public LocalServer()
            {
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start(); int port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
                Url = "http://127.0.0.1:" + port + "/";
                listener.Prefixes.Add(Url); listener.Start();
                Worker = Task.Run(async () =>
                {
                    var create = await listener.GetContextAsync();
                    using (var reader = new StreamReader(create.Request.InputStream)) CreateBody = await reader.ReadToEndAsync();
                    CreateCount++;
                    string file = CreateBody.Contains("screenshot.png") ? "screenshot.png" : CreateBody.Contains("screenshot.jpg") ? "screenshot.jpg" : null;
                    string uploads = file == null ? "[]" : "[{\"fileName\":\"" + file + "\",\"url\":\"" + Url + "upload\",\"fields\":{}}]";
                    byte[] response = Encoding.UTF8.GetBytes("{\"ok\":true,\"cardId\":\"dummy\",\"uploadUrls\":" + uploads + "}");
                    create.Response.ContentType = "application/json";
                    await create.Response.OutputStream.WriteAsync(response, 0, response.Length); create.Response.Close();
                    if (file == null) return;
                    var upload = await listener.GetContextAsync();
                    using var body = new MemoryStream(); await upload.Request.InputStream.CopyToAsync(body);
                    UploadBody = body.ToArray(); UploadCount++;
                    upload.Response.StatusCode = 204; upload.Response.Close();
                });
            }
            public void Dispose() => listener.Close();
        }
    }

    public class ProbeUGUI : CodecksCardCreatorForm
    {
        public int Captures;
        protected override byte[] CaptureScreenshot() { Captures++; return new byte[] { 7, 8, 9 }; }
        protected override string GetMetaText() => "consumer metadata";
    }
    public class ProbeToolkit : CodecksUIToolkitFeedbackController
    {
        public int Captures;
        protected override byte[] CaptureScreenshot() { Captures++; return new byte[] { 7, 8, 9 }; }
        protected override string GetMetadata() => "consumer metadata";
    }
}
