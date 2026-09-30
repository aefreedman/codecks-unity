# Codecks Bug & Feedback Reporter for Unity

An independently maintained UPM fork of the [Codecks Unity plugin](https://github.com/codecks-io/codecks-unity), originally developed by Codecks GmbH and released under the [MIT license](../LICENSE.md). This fork is not affiliated with or endorsed by Codecks GmbH.

This Markdown manual is the maintained consumer documentation. The former PDF is not a supported release artifact.

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

1. Copy the UXML, USS, controller, and branding asset from the imported sample into `Assets`.
2. Add `PanelRenderer`, `CodecksCardCreator`, and `CodecksUIToolkitFeedbackController` to a GameObject.
3. Assign the copied Visual Tree Asset and Panel Settings to the Panel Renderer, and the same `CodecksCardCreator` to the controller.
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
