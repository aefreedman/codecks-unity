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
                Assert.That(form.IsSubmitting, Is.False, "Successful callback clears pending status before dismissal.");
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

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_ExplicitReplacementRetainsScope(bool toolkit)
        {
            using var form = new Form(toolkit);
            int acquired = 0, disposed = 0;
            var states = new System.Collections.Generic.List<CodecksFormState>();
            form.Observe(state => { states.Add(state); if (state == CodecksFormState.Open) Assert.That(form.IsOpen, Is.True); });
            form.Acquire = () => { acquired++; return new Scope(() => disposed++); };
            form.OpenWithoutScreenshot();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Open));
            form.Open(new byte[] { 1 }, CodecksCardCreator.CodecksFileType.PNG);
            Assert.That(acquired, Is.EqualTo(1));
            Assert.That(disposed, Is.Zero);
            form.Close(); form.Close();
            Assert.That(disposed, Is.EqualTo(1));
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(states, Is.EqualTo(new[] { CodecksFormState.Opening, CodecksFormState.Open,
                CodecksFormState.Opening, CodecksFormState.Open, CodecksFormState.Closed }));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_OpeningObserverAbortsBeforeAcquisition(bool toolkit)
        {
            using var form = new Form(toolkit);
            int acquired = 0;
            form.Acquire = () => { acquired++; return null; };
            form.Observe(state => { if (state == CodecksFormState.Opening) form.Close(); });
            form.OpenWithoutScreenshot();
            Assert.That(acquired, Is.Zero);
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(form.IsOpen, Is.False);
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_AcquisitionCloseReopenDisposesStaleOwnership(bool toolkit)
        {
            using var form = new Form(toolkit);
            int acquired = 0, disposed = 0;
            form.Acquire = () =>
            {
                acquired++;
                if (acquired == 1) { form.Close(); form.OpenWithoutScreenshot(); }
                return new Scope(() => disposed++);
            };
            form.OpenWithoutScreenshot();
            Assert.That(acquired, Is.EqualTo(2));
            Assert.That(disposed, Is.EqualTo(1));
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Open));
            form.Close();
            Assert.That(disposed, Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_AcquisitionReplacementSharesPendingOwner(bool toolkit)
        {
            using var form = new Form(toolkit);
            int acquired = 0, disposed = 0;
            var bytes = new byte[] { 5 };
            form.Acquire = () =>
            {
                acquired++;
                form.Open(bytes, CodecksCardCreator.CodecksFileType.PNG);
                return new Scope(() => disposed++);
            };
            form.OpenWithoutScreenshot();
            Assert.That(acquired, Is.EqualTo(1));
            Assert.That(form.Bytes, Is.SameAs(bytes));
            form.Close();
            Assert.That(disposed, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_AcquisitionThrowAbortsAndCanRetry(bool toolkit)
        {
            using var form = new Form(toolkit);
            form.Acquire = () => throw new InvalidOperationException();
            LogAssert.Expect(LogType.Warning, "Codecks modal scope acquisition failed; opening was aborted.");
            form.OpenWithoutScreenshot();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(form.IsOpen, Is.False);
            form.Acquire = () => null;
            form.OpenWithoutScreenshot();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Open));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_ClosedObserverReopenAndThrowingDisposalAreIsolated(bool toolkit)
        {
            using var form = new Form(toolkit);
            int acquired = 0, disposed = 0;
            form.Acquire = () => { acquired++; return new Scope(() => { disposed++; throw new InvalidOperationException(); }); };
            form.OpenWithoutScreenshot();
            bool reopen = true;
            form.Observe(state => { if (state == CodecksFormState.Closed && reopen) { reopen = false; form.OpenWithoutScreenshot(); } });
            LogAssert.Expect(LogType.Warning, "Codecks modal scope disposal threw; ownership has been released.");
            form.Close();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Open));
            Assert.That(form.IsOpen, Is.True);
            Assert.That(acquired, Is.EqualTo(2));
            Assert.That(disposed, Is.EqualTo(1));
            LogAssert.Expect(LogType.Warning, "Codecks modal scope disposal threw; ownership has been released.");
            form.Close();
            Assert.That(disposed, Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_OpenObserverDisablesAndDisposalCannotReopenDisabledComponent(bool toolkit)
        {
            using var form = new Form(toolkit);
            int disposed = 0;
            form.Acquire = () => new Scope(() => { disposed++; form.OpenWithoutScreenshot(); });
            form.Observe(state => { if (state == CodecksFormState.Open) form.Disable(); });
            form.OpenWithoutScreenshot();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(form.IsOpen, Is.False);
            Assert.That(disposed, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_ThrowingObserverDoesNotLeak(bool toolkit)
        {
            using var form = new Form(toolkit);
            int disposed = 0;
            form.Acquire = () => new Scope(() => disposed++);
            form.Observe(state => { if (state == CodecksFormState.Opening) throw new InvalidOperationException(); });
            LogAssert.Expect(LogType.Warning, "Codecks modal state observer threw; remaining current observers will still be notified.");
            form.OpenWithoutScreenshot(); form.Close();
            Assert.That(disposed, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_StaleResultDoesNotAffectNewSubmittingSession(bool toolkit)
        {
            using var form = new Form(toolkit);
            form.OpenWithoutScreenshot();
            int stale = form.Session;
            form.SetSubmitting(true);
            Assert.That(form.IsSubmitting, Is.True);
            form.Close();
            Assert.That(form.IsSubmitting, Is.False);
            form.OpenWithoutScreenshot();
            form.SetSubmitting(true);
            form.Deliver(stale);
            Assert.That(form.IsSubmitting, Is.True);
            Assert.That(form.Status, Is.Empty);
            form.Close();
        }

        [Test]
        public void Lifecycle_ToolkitReloadReleasesAndRestoresBindings()
        {
            using var form = new Form(true);
            int disposed = 0;
            form.Acquire = () => new Scope(() => disposed++);
            form.OpenWithoutScreenshot(); form.Reload();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(disposed, Is.EqualTo(1));
            form.OpenWithoutScreenshot();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Open));
            form.Close();
            Assert.That(disposed, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator Lifecycle_UGUI_CaptureReentrancyCannotResurrect() => CaptureReentrancy(false);
        [UnityTest] public IEnumerator Lifecycle_Toolkit_CaptureReentrancyCannotResurrect() => CaptureReentrancy(true);
        private IEnumerator CaptureReentrancy(bool toolkit)
        {
            using var form = new Form(toolkit);
            int acquired = 0, disposed = 0;
            form.Acquire = () => { acquired++; return new Scope(() => disposed++); };
            form.CaptureAction = () => { form.Close(); form.OpenWithoutScreenshot(); };
            form.OpenDefault();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Opening));
            Assert.That(acquired, Is.EqualTo(1));
            yield return new WaitForEndOfFrame(); yield return null;
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Open));
            Assert.That(form.Bytes, Is.Null);
            Assert.That(acquired, Is.EqualTo(2));
            Assert.That(disposed, Is.EqualTo(1));
            form.Close();
            Assert.That(disposed, Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_AcquisitionDisableAborts(bool toolkit)
        {
            using var form = new Form(toolkit);
            int disposed = 0;
            form.Acquire = () => { form.Disable(); return new Scope(() => disposed++); };
            form.OpenWithoutScreenshot();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(form.IsOpen, Is.False);
            Assert.That(disposed, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_DisposalCanReopenWithoutStaleHide(bool toolkit)
        {
            using var form = new Form(toolkit);
            int acquired = 0, disposed = 0;
            form.Acquire = () =>
            {
                int index = ++acquired;
                return new Scope(() => { disposed++; if (index == 1) form.OpenWithoutScreenshot(); });
            };
            form.OpenWithoutScreenshot(); form.Close();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Open));
            Assert.That(form.IsOpen, Is.True);
            Assert.That(acquired, Is.EqualTo(2));
            form.Close();
            Assert.That(disposed, Is.EqualTo(2));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_SuccessClearsSubmittingButRetainsOnceOnlyGuard(bool toolkit)
        {
            using var form = new Form(toolkit);
            form.OpenWithoutScreenshot(); form.SetSubmitting(true);
            form.Deliver(form.Session);
            Assert.That(form.IsSubmitting, Is.False);
            Assert.That(form.SendLatched, Is.True);
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Open));
            form.OpenWithoutScreenshot();
            Assert.That(form.IsSubmitting, Is.False);
            Assert.That(form.SendLatched, Is.False);
        }

        [Test]
        public void Lifecycle_ToolkitUnavailableRootReleasesScope()
        {
            using var form = new Form(true);
            int disposed = 0;
            form.Acquire = () => new Scope(() => disposed++);
            form.OpenWithoutScreenshot();
            LogAssert.Expect(LogType.Error, "Codecks UI Toolkit feedback reporter received an empty Panel Renderer root.");
            form.ReloadEmpty();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(disposed, Is.EqualTo(1));
            form.OpenWithoutScreenshot();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
        }

        [UnityTest] public IEnumerator Lifecycle_UGUI_PendingCloseAndDestroyRelease() => PendingCloseAndDestroy(false);
        [UnityTest] public IEnumerator Lifecycle_Toolkit_PendingCloseAndDestroyRelease() => PendingCloseAndDestroy(true);
        private IEnumerator PendingCloseAndDestroy(bool toolkit)
        {
            using var form = new Form(toolkit);
            int disposed = 0;
            form.Acquire = () => new Scope(() => disposed++);
            form.OpenDefault(); form.Close();
            Assert.That(disposed, Is.EqualTo(1));
            yield return new WaitForEndOfFrame(); yield return null;
            Assert.That(form.Captures, Is.Zero);
            form.OpenWithoutScreenshot(); form.DestroyHost();
            yield return null;
            Assert.That(disposed, Is.EqualTo(2));
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
        }

        [UnityTest] public IEnumerator Lifecycle_UGUI_CaptureThrowReleases() => CaptureThrow(false);
        [UnityTest] public IEnumerator Lifecycle_Toolkit_CaptureThrowReleases() => CaptureThrow(true);
        private IEnumerator CaptureThrow(bool toolkit)
        {
            using var form = new Form(toolkit);
            int disposed = 0;
            form.Acquire = () => new Scope(() => disposed++);
            form.CaptureAction = () => throw new InvalidOperationException();
            LogAssert.Expect(LogType.Warning, toolkit ? "Codecks UI Toolkit capture override threw; opening was aborted." : "Codecks report capture override threw; opening was aborted.");
            form.OpenDefault();
            yield return new WaitForEndOfFrame(); yield return null;
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(disposed, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_OpeningObserverReplacementSuppressesStaleNotifications(bool toolkit)
        {
            using var form = new Form(toolkit);
            int acquired = 0;
            var bytes = new byte[] { 6 };
            var observed = new System.Collections.Generic.List<CodecksFormState>();
            form.Acquire = () => { acquired++; return null; };
            form.Observe(state => { if (state == CodecksFormState.Opening) form.Open(bytes, CodecksCardCreator.CodecksFileType.PNG); });
            form.Observe(state => observed.Add(state));
            form.OpenWithoutScreenshot();
            Assert.That(form.Bytes, Is.SameAs(bytes));
            Assert.That(acquired, Is.EqualTo(1));
            Assert.That(observed, Is.EqualTo(new[] { CodecksFormState.Open }), "Do not deliver an obsolete Opening after nested Open.");
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_AcquisitionDestroyReleasesReturnedScope(bool toolkit)
        {
            using var form = new Form(toolkit);
            int disposed = 0;
            form.Acquire = () => { form.DestroyHostImmediately(); return new Scope(() => disposed++); };
            form.OpenWithoutScreenshot();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(disposed, Is.EqualTo(1));
        }

        [TestCase(false)] [TestCase(true)]
        public void Lifecycle_OpeningObserverDestroySkipsAcquisition(bool toolkit)
        {
            using var form = new Form(toolkit);
            int acquired = 0;
            form.Acquire = () => { acquired++; return null; };
            form.Observe(state => { if (state == CodecksFormState.Opening) form.DestroyHostImmediately(); });
            form.OpenWithoutScreenshot();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(acquired, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Lifecycle_ToolkitRendererDisableReleases()
        {
            using var form = new Form(true);
            int disposed = 0;
            form.Acquire = () => new Scope(() => disposed++);
            form.OpenWithoutScreenshot(); form.DisableRenderer();
            yield return null;
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(disposed, Is.EqualTo(1));
        }

        [Test]
        public void Lifecycle_ToolkitReloadDisposalRebindCannotBeOverwrittenByStaleReload()
        {
            using var form = new Form(true);
            int acquired = 0, disposed = 0;
            form.Acquire = () =>
            {
                int index = ++acquired;
                return new Scope(() =>
                {
                    disposed++;
                    if (index == 1) { form.ReloadVersion(3); form.OpenWithoutScreenshot(); }
                });
            };
            form.OpenWithoutScreenshot(); form.Reload();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Open));
            Assert.That(form.IsOpen, Is.True);
            Assert.That(acquired, Is.EqualTo(2));
            Assert.That(disposed, Is.EqualTo(1));
            form.Close();
            Assert.That(disposed, Is.EqualTo(2));
        }

        [TestCase("disabled")] [TestCase("destroyed")] [TestCase("backend-disabled")] [TestCase("backend-destroyed")]
        public void Lifecycle_UGUI_InactiveOpeningGuardReleasesWithoutEndOfFrame(string invalidation)
        {
            using var form = new Form(false);
            int disposed = 0;
            form.Acquire = () => new Scope(() => disposed++);
            form.OpenDefault();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Opening));
            var guard = form.OpeningGuard;
            switch (invalidation)
            {
                case "disabled": form.Disable(); break;
                case "destroyed": form.DestroyHostImmediately(); break;
                case "backend-disabled": form.DisableBackend(); break;
                case "backend-destroyed": form.DestroyBackend(); break;
            }
            if (guard != null) guard.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(guard, null);
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed), "Normal Update/lifecycle, without any EOF or rendered frame.");
            Assert.That(disposed, Is.EqualTo(1));
            Assert.That(form.Captures, Is.Zero);
        }

        [Test]
        public void Lifecycle_ToolkitBackendDestroyedPendingOpeningReleasesWithoutEndOfFrame()
        {
            using var form = new Form(true);
            int disposed = 0;
            form.Acquire = () => new Scope(() => disposed++);
            form.OpenDefault(); form.DestroyBackend(); form.PulseToolkitUpdate();
            Assert.That(form.State, Is.EqualTo(CodecksFormState.Closed));
            Assert.That(disposed, Is.EqualTo(1));
            Assert.That(form.Captures, Is.Zero);
        }

        private sealed class Scope : IDisposable
        {
            private readonly Action dispose;
            public Scope(Action dispose) => this.dispose = dispose;
            public void Dispose() => dispose();
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
            public CodecksFormState State => baseType == typeof(CodecksCardCreatorForm) ? ugui.State : toolkit.State;
            public bool IsSubmitting => baseType == typeof(CodecksCardCreatorForm) ? ugui.IsSubmitting : toolkit.IsSubmitting;
            public bool SendLatched => Field<bool>("submissionInFlight");
            public MonoBehaviour OpeningGuard => backend.GetComponents<MonoBehaviour>().Single(c => c.GetType().Name == "CodecksPendingOpening");
            public void DisableBackend() => backend.GetComponent<CodecksCardCreator>().enabled = false;
            public void DestroyBackend() => UnityEngine.Object.DestroyImmediate(backend);
            public void PulseToolkitUpdate() => baseType.GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Controller, null);
            public void DestroyHost() => UnityEngine.Object.Destroy(host);
            public void DestroyHostImmediately() => UnityEngine.Object.DestroyImmediate(host);
            public void DisableRenderer() => host.GetComponent<PanelRenderer>().enabled = false;
            public void ReloadEmpty() => baseType.GetMethod("OnUIReload", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Controller, new object[] { null, null, 2 });
            public Func<IDisposable> Acquire { set { if (ugui != null) ugui.AcquireScope = value; else toolkit.AcquireScope = value; } }
            public Action CaptureAction { set { if (ugui != null) ugui.CaptureAction = value; else toolkit.CaptureAction = value; } }
            public void Observe(Action<CodecksFormState> observer) { if (ugui != null) ugui.StateChanged += observer; else toolkit.StateChanged += observer; }
            public void Disable() => ((MonoBehaviour)Controller).enabled = false;
            public void SetSubmitting(bool value)
            {
                baseType.GetField("submissionInFlight", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Controller, value);
                baseType.GetField("awaitingSubmission", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(Controller, value);
            }
            public void Reload() => ReloadVersion(2);
            public void ReloadVersion(int version) => baseType.GetMethod("OnUIReload", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Controller, new object[] { null, root, version });
            public int Captures => baseType == typeof(CodecksCardCreatorForm) ? ugui.Captures : toolkit.Captures;
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
        public Action CaptureAction;
        protected override byte[] CaptureScreenshot() { Captures++; CaptureAction?.Invoke(); return new byte[] { 7, 8, 9 }; }
        protected override string GetMetaText() => "consumer metadata";
    }
    public class ProbeToolkit : CodecksUIToolkitFeedbackController
    {
        public int Captures;
        public Action CaptureAction;
        protected override byte[] CaptureScreenshot() { Captures++; CaptureAction?.Invoke(); return new byte[] { 7, 8, 9 }; }
        protected override string GetMetadata() => "consumer metadata";
    }
}
