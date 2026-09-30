# Codecks Bug & Feedback Reporter for Unity

An independently maintained UPM fork of the [Codecks Unity plugin](https://github.com/codecks-io/codecks-unity), originally developed by Codecks GmbH and released under the [MIT license](../LICENSE.md). This fork is not affiliated with or endorsed by Codecks GmbH.

This Markdown manual is the maintained consumer documentation. The former PDF is not a supported release artifact.

This feature branch prepares **0.2.0 (unreleased)**. The published `v0.1.0` install below does not include the modal lifecycle or form-only template additions described here; no `v0.2.0` tag is claimed.

## Install through Package Manager

The package requires Unity 6000.5 or later and Git on the machine that resolves packages.

1. Open **Window > Package Manager**.
2. Select **Add package from git URL**.
3. Enter `https://github.com/aefreedman/codecks-unity.git#v0.1.0`.
4. Select the package, open **Samples**, and import the implementation you need.

Versioned releases use matching `v<package-version>` Git tags. The installation URL above requires the published `v0.1.0` tag. Do not use an untagged branch as a stable dependency.

## Samples

### Feedback Reporter (uGUI)

Import **Feedback Reporter (uGUI)**, run the sample setup action described below while its scene is closed, then open `CodecksSampleScene`. It uses Canvas, `CodecksCardCreatorForm`, and TextMesh Pro. Configure the assigned shared report settings asset before entering Play mode; do not enter tokens on scene components.

If text is missing, import **Window > TextMeshPro > Import TMP Essential Resources**. The sample uses the standard TMP Liberation Sans font asset. Its EventSystem uses `StandaloneInputModule`, which supports the legacy Input Manager or **Active Input Handling: Both**. New-Input-System-only projects must replace that module with `InputSystemUIInputModule`, configure its UI actions, and keep only one input module enabled.

Subclass `CodecksCardCreatorForm` in `Assets` and override `GetMetaText()` to append project metadata without editing PackageCache. Preserve the serialized UI references when replacing the form component.

### Feedback Reporter (UI Toolkit)

Import **Feedback Reporter (UI Toolkit)**, then open `CodecksUIToolkitFeedbackReporterScene`. It needs no TMP resources or EventSystem. The form is a runtime UI Toolkit integration built on **Panel Renderer**, not `UIDocument`.

The sample GameObject has a Panel Renderer, `CodecksCardCreator`, and `CodecksUIToolkitFeedbackController`. Its Panel Renderer uses `CodecksFeedbackReporter.uxml` as the Visual Tree Asset and `CodecksFeedbackPanelSettings` as Panel Settings. The supplied Panel Settings references a sample-owned runtime theme.

To embed the form in a project:

1. In forthcoming 0.2.0, copy `CodecksFeedbackForm.uxml`, `CodecksFeedbackReporter.uss`, the controller, and branding asset from the imported sample into `Assets`. Copy `CodecksFeedbackReporter.uxml` too if retaining the standalone launcher; it composes the form through a relative template reference.
2. Add `PanelRenderer`, `CodecksCardCreator`, and `CodecksUIToolkitFeedbackController` to a GameObject.
3. Assign the copied form-only `CodecksFeedbackForm.uxml` (or standalone composition) and Panel Settings to the Panel Renderer, and the same `CodecksCardCreator` to the controller.
4. Preserve the required UXML element names: `codecks-feedback-overlay`, `codecks-feedback-report`, `codecks-feedback-severity`, `codecks-feedback-email`, `codecks-feedback-send`, `codecks-feedback-cancel`, and `codecks-feedback-status`.
5. Restyle the `codecks-feedback-*` classes in the copied USS. When several Panel Renderers share Panel Settings, choose explicit sort orders. The supplied controller focuses the report field on open.

The `codecks-feedback-launcher` button is optional: an external menu/hotkey may call the public opening API below. The controller registers a Panel Renderer reload callback and rebuilds bindings when its UI reloads. Keep that lifecycle if changing the source asset or Panel Settings. Add project metadata in a consumer-owned subclass by overriding `GetMetadata()`.

## Consumer-controlled capture and opening

Both `CodecksCardCreatorForm` and `CodecksUIToolkitFeedbackController` expose:

```csharp
void ShowCodecksForm(); // Preserved no-arg UnityEvent entry: EOF capture, then open.
void ShowCodecksForm(byte[] screenshot, CodecksCardCreator.CodecksFileType fileType);
void ShowCodecksFormWithoutScreenshot();
```

The latter two open immediately, skip capture/EOF waiting, and start a new session that invalidates pending captures and old UI callbacks. JPG/PNG filename and MIME follow the explicit type regardless of platform. Null/empty bytes and unsupported types throw argument exceptions; valid JPG/PNG encoding is the caller's responsibility, not automatically decoded. The controller retains but does not modify/dispose caller bytes or textures; keep bytes unchanged during submission. Default capture owns only its own temporary texture. Explicit opening may replace a visible session; it does not cancel already-dispatched requests.

In an existing menu component, `reporter` can be either form type. Capture before opening the pause menu, not after:

```csharp
IEnumerator OpenFeedbackBeforePauseMenu()
{
    yield return new WaitForEndOfFrame(); // Caller decides its capture timing.
    var image = ScreenCapture.CaptureScreenshotAsTexture();
    if (image == null)
    {
        pauseMenu.SetActive(true);
        reporter.ShowCodecksFormWithoutScreenshot();
        yield break;
    }
    byte[] png;
    try { png = image.EncodeToPNG(); }
    finally { Destroy(image); } // Caller owns this texture.
    pauseMenu.SetActive(true);
    reporter.ShowCodecksForm(png, CodecksCardCreator.CodecksFileType.PNG);
}
```

For no-image reports from an existing menu/hotkey, call `reporter.ShowCodecksFormWithoutScreenshot()`. Toolkit binding no longer requires its sample launcher element, but the renderer/controller must be enabled and initialized before opening.

Both forms support `protected virtual byte[] CaptureScreenshot()` for replacing default capture logic; preserve its platform encoding contract (standalone JPG, otherwise PNG), or return null. Its no-arg opener still waits for EOF; use explicit opening to bypass that wait or provide a different encoding. Metadata hooks remain `CodecksCardCreatorForm.GetMetaText()` and `CodecksUIToolkitFeedbackController.GetMetadata()`. For example:

```csharp
public sealed class ProjectFeedbackForm : CodecksCardCreatorForm
{
    protected override byte[] CaptureScreenshot() => null;
    protected override string GetMetaText() => base.GetMetaText() + "Build channel: internal";
}
// Toolkit subclass uses the same CaptureScreenshot signature and overrides GetMetadata instead.
```

These are ordinary consumer-owned subclasses/public calls. `CodecksCardCreator.CreateNewCard` remains directly usable; no provider/service framework is required.

## Consumer-owned modal integration

These APIs are **forthcoming 0.2.0**, not in the published `v0.1.0` install. Both `Codecks.Runtime.CodecksCardCreatorForm` and the imported `Codecks.Samples.UIToolkitFeedbackReporter.CodecksUIToolkitFeedbackController` expose:

```csharp
CodecksFormState State { get; } // Codecks.Runtime: Closed, Opening, Open
event Action<CodecksFormState> StateChanged;
Func<IDisposable> AcquireScope { get; set; } // Optional, assigned in code, not serialized
bool IsSubmitting { get; } // Only this session's outstanding backend operation
```

Assign `AcquireScope` to your existing project modal coordinator's acquisition method (returning an independent `IDisposable` ownership token). Returning null means no ownership. The package has no pause, gameplay-lock, action-map or coordinator dependencies. Leave it unset for the standalone sample. Your scope decides which gameplay/presentation commands to suppress, how to preserve UI text/navigation input, and how to restore previous focus when its ownership ends. Do not disable the UI input needed by the form itself.

### Ordering, ownership and reentrancy

1. Opening invalidates old capture/result/dismissal tokens and resets session submission/image data. An active replacement retains its scope, even during scope acquisition.
2. Set `Opening`, synchronously notify `StateChanged`, then invoke `AcquireScope`, then capture. An observer that closes first prevents acquisition. Default opening waits for end-of-frame with the overlay hidden; supplied/no-image opening skips capture and opens synchronously. An already-active no-argument opening is ignored; explicit opening replaces the session.
3. Show the view, then set/notify `Open`. Toolkit focuses the report field before notifying `Open`. Reentrant close/replacement invalidates older work; it cannot resurrect the old UI or capture.
4. Close invalidates the session, clears `IsSubmitting`/image data, detaches ownership and hides the UI (teardown also detaches bindings). Set/notify `Closed`, **then** dispose the detached scope exactly once. Disable/destroy/abort/reload use the same ownership cleanup. A `Closed` observer may reopen and acquire a new scope before the old token is disposed: the coordinator must release only that token, not unlock or refocus a newer modal. Prefer token-owned focus restoration after release, and restore only when that token's modal still owns the restoration decision. `Closed` is not a notification that scope disposal has already finished.

State listeners are isolated: exceptions warn, and remaining listeners run only while that transition is current. Obsolete notifications stop after reentrancy. Acquisition exceptions warn and abort opening (including a nested replacement sharing that acquisition); an independent close/reopen owner survives an older failure. A scope returned after its owner closed is disposed immediately. Disposal exceptions warn without leaking package ownership or causing a second disposal. Throwing capture overrides abort opening; unavailable built-in capture instead opens without an image. Toolkit null-root/reload/renderer unavailability closes ownership before binding recovery. The uGUI pending-capture guard also releases ownership when a never-active form or its capture host is disabled/destroyed, by normal lifecycle/Update callbacks without depending on EOF. No callbacks can execute while the whole runtime is suspended.

`IsSubmitting` is true only while the current session awaits its backend result; result success/failure, close or replacement makes it false. A successful send retains the separate once-only guard until dismissal/replacement even though `IsSubmitting` is false; a failed send permits retry. Closing/replacing does not cancel already-dispatched backend operations; stale completions cannot change the new session or release its scope. Do not treat false as proof that all network work has stopped.

### Keyboard/gamepad and focus

Route your project's commands through its coordinator while `Opening` or `Open`: feedback cancel/back calls `HideCodecksForm()`, not the underlying pause menu's back handler as well. Gate opening commands using `State`; use `StateChanged` to update your presentation. Preserve UI navigation, typing, submit/cancel bindings and your project's focus policy. The package does **not** poll raw Escape, switch action maps, change time scale or automatically restore caller focus. Scope acquisition happens before EOF, so if your scope shows a pause/menu overlay it can enter default capture: capture first using the supplied-image API above, or make the scope input-only until after capture. Toolkit's report-field focus does not establish a project's gamepad navigation policy.

### Form-only embedding and pointer boundary

Use `CodecksFeedbackForm.uxml` directly as the Panel Renderer Visual Tree Asset, or compose it once inside a full-screen host template. Keep its relative shared USS/branding dependencies beside it, the required named elements, and a single controller binding root per form. It contains no launcher and opens through the same public APIs. The standalone `CodecksFeedbackReporter.uxml` composes that exact form plus its optional launcher; do not copy/paste a second form. Retain the visible Powered by Codecks image beside the dialog, including when restyling.

Outer form/standalone/template-instance layout wrappers use `PickingMode.Ignore`. The hidden overlay does not intercept picking; the open overlay uses `PickingMode.Position` and fills its host, including empty space outside the scrollable dialog. Size an embedded host to the whole presentation area that must be blocked. Choose panel sort order intentionally; this is a boundary within that panel, not a global input lock across other panels, uGUI raycasters or gameplay.

The controller registers **bubble-phase** boundaries on the overlay for pointer down/up/move/cancel/over/out, mouse down/up/move/over/out, click, context-click and wheel. It calls `StopPropagation`, not `PreventDefault` or `StopImmediatePropagation`: child field/button handlers, scrolling, default focus and other handlers on the overlay still work. Bindings are removed on teardown/reload. Ancestor bubble handlers do not receive these modal events. Ancestor **trickle-down** handlers have already run and cannot be undone; they must consult your coordinator before issuing presentation commands. Detached dropdown menus and unrelated panels are outside the overlay's ancestry and require the same caller-owned command gate. Keyboard/gamepad commands are not swallowed by this pointer boundary.

## Tokens, privacy, and security

After importing samples, close their scenes and run **Tools > Codecks > Set Up Imported Samples**. It creates/selects `Assets/Resources/Codecks/CodecksSettings.asset` and assigns that asset to missing settings references in standard imported sample scenes under `Assets/Samples/`. Repeated setup preserves the existing asset/token and existing explicit references. Open sample scenes are skipped (including clean scenes); close them and rerun. Other user scenes are never edited or saved. Configure the selected asset's scoped report token and endpoint once.

`CodecksCardCreator.settings` is **required**. There is no automatic Resources lookup or token-file/component fallback, even if a conventional asset exists. Custom/revised scenes outside the standard imported sample location must explicitly reference the desired settings asset; the bootstrap intentionally does not rewrite them. **Tools > Codecks > Create or Select Report Settings** remains available for asset-only setup.

An empty/whitespace token or invalid endpoint fails clearly before network dispatch. Endpoints must be absolute HTTP(S) URLs without credentials, query or fragment. Timeout remains the existing fixed 30 seconds.

Migration: `defaultToken` and `codecksURL` are hidden obsolete fields retained only for source/serialized compatibility and are ignored. Legacy `Resources/Codecks/codecksToken.txt` is no longer read. Manually configure the settings asset and assign references (or use the imported-sample bootstrap); no token is copied automatically. Existing configured settings assets at the default path are reused, not duplicated or reset.

Resources/ScriptableObject credentials remain extractable from client builds. Keep configured assets/tokens out of source control; use scoped revocable report tokens, never access keys. This is not a secure secret store.
An access key must never ship in a player or source repository. Use `CodecksTokenCreator.CreateAndSetNewToken` only from a trusted editor or build process, protect the access key with the build system's secret handling, and configure the referenced settings asset explicitly: that method's legacy token-file output no longer configures the reporter. The package has no secure secret-storage facility.

Reports may send a description, optional severity, optional email, platform/app-version metadata, and a screenshot. Configure report-token upload limits in Codecks. Obtain consent and apply your own privacy policy before collecting player email addresses, screenshots, or other personal/project data.

## Runtime behavior and platform limits

Both supplied forms require at least ten report characters. They capture a screenshot at the end of the current frame before showing the overlay; this avoids capturing the form itself. Screenshot capture needs a graphics-capable runtime. On capture failure, the form logs a warning and sends the report without an attachment.

The request path uses UnityWebRequest and supports report creation plus server-provided attachment uploads. Failed requests leave the form available for a retry. Card creation can succeed before attachment upload fails; the Toolkit form warns that retrying such a partial failure creates a new report. The uGUI form uses JPG attachments on standalone builds and PNG elsewhere; the UI Toolkit form follows the same policy.

The package has been compiled in Unity 6000.5.5 and behavior-checked in Unity 6000.6.0f1 with EditMode, localhost UnityWebRequest create/upload PlayMode, and graphics-capable form PlayMode coverage. A disposable Unity 6000.6.0f1 Windows x86-64 `StandaloneWindows64` player using Mono built and ran successfully. It submitted a localhost report body, high severity, email, and multipart text attachment; the player observed one callback and the endpoint observed one create request plus one upload request. This evidence is limited to Windows x86-64 Mono. IL2CPP is optional and unvalidated; do not infer support for untested player platforms from editor checks.

## Migrate from a copied plugin folder

1. Back up project-specific forms, subclasses, and UI assets; revoke accidentally exposed credentials.
2. Identify scenes, prefabs, and scripts that reference the old copied plugin before removing it.
3. Install this UPM package and import the appropriate sample through Package Manager.
4. Move custom code and UI assets into `Assets`, reattach references, and use the protected metadata hooks rather than modifying PackageCache.
5. Convert any UI Toolkit integration to Panel Renderer plus its reload callback lifecycle; do not substitute `UIDocument` for the supplied embedding contract.
6. Validate with a disposable report token before shipping.

The existing uGUI runtime component names and script GUIDs are preserved for migration. Imported sample assets are consumer-owned and are the supported place to customize layouts and styles. If a `CodecksCardCreator` is destroyed during a report or attachment upload, the request is cancelled and its callback receives one failure result; retain the component until that callback when the result is needed.

## Attribution, branding, and support

The [MIT license](../LICENSE.md) retains the original Codecks GmbH copyright notice. This fork's support channel is its [issue tracker](https://github.com/aefreedman/codecks-unity/issues); Codecks GmbH does not support or endorse this fork.

The upstream plugin supplied the bundled Codecks naming and visual assets. Retain the visible Powered by Codecks image beside the report form when adapting the samples. The software license does not itself grant trademark rights; do not claim Codecks endorsement.
