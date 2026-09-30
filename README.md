# Codecks Bug & Feedback Reporter for Unity

An independently maintained UPM fork of the [Codecks Unity plugin](https://github.com/codecks-io/codecks-unity), originally developed by Codecks GmbH and released under the [MIT license](LICENSE.md). This fork is not affiliated with or endorsed by Codecks GmbH.

This package provides uGUI and runtime UI Toolkit feedback forms backed by `CodecksCardCreator`. It targets Unity 6000.5 or later. Read the [consumer manual](Documentation~/index.md) and [changelog](CHANGELOG.md). Fork-specific support belongs in this repository's [issue tracker](https://github.com/aefreedman/codecks-unity/issues), not Codecks support.

Version **0.2.0** adds observable modal lifecycle, caller-owned scope hooks and an embeddable form-only Toolkit template. Existing imported samples are project-owned copies: updating the package does not replace their controllers or UI assets. Import the 0.2.0 sample into its new version folder and deliberately merge changes into customized copies; preserve scene references, settings and branding.

## Install

1. Ensure Git is installed and available on the machine that resolves Unity packages.
2. In Unity, open **Window > Package Manager**, select **Add package from git URL**, and enter:

   ```text
   https://github.com/aefreedman/codecks-unity.git#v0.2.0
   ```

3. Open the package's **Samples** tab and import one or both samples:
   - **Feedback Reporter (uGUI)** for the existing Canvas/TMP implementation.
   - **Feedback Reporter (UI Toolkit)** for the Panel Renderer implementation.

Versioned releases use matching `v<package-version>` Git tags. The installation URL above requires the published `v0.2.0` tag; avoid untagged branches for stable dependencies.

## Choose an implementation

### uGUI

The uGUI sample contains `CodecksSampleScene`, a Canvas form, and TextMesh Pro fields. After importing it, run the setup action with its scene closed and configure the assigned shared settings asset as described below; do not enter tokens on scene components. If TMP text is missing, import **Window > TextMeshPro > Import TMP Essential Resources**; the sample uses TMP's standard Liberation Sans asset.

The sample EventSystem uses `StandaloneInputModule`, suitable for the legacy Input Manager or **Active Input Handling: Both**. In a new-Input-System-only project, replace it with `InputSystemUIInputModule`, configure that module's UI actions, and do not enable both modules on one EventSystem. See the [uGUI sample guide](Samples~/FeedbackReporter/README.md) for metadata customization and setup details.

### UI Toolkit

The UI Toolkit sample contains `CodecksUIToolkitFeedbackReporterScene`. Its form uses a **Panel Renderer**, not `UIDocument`; assign its Visual Tree Asset (UXML) and Panel Settings, then keep `CodecksCardCreator` and `CodecksUIToolkitFeedbackController` on the same GameObject. The supplied Panel Settings references a sample-owned runtime theme, so it does not rely on project UI Toolkit assets.

For embedding in 0.2.0, copy `CodecksFeedbackForm.uxml`, the shared USS, controller, and branding into `Assets` before editing. The existing standalone `CodecksFeedbackReporter.uxml` composes that same form plus its optional launcher; copy both UXML files if using it. Keep the controller's required named elements, use explicit Panel Renderer sort orders when sharing Panel Settings, and retain the reload callback lifecycle so bindings are rebuilt after the source asset or Panel Settings changes. The supplied controller focuses the report field when opening. See the [UI Toolkit sample guide](Samples~/UIToolkitFeedbackReporter/README.md) for required names, styling hooks, and metadata customization.

## Tokens and report data

After importing samples, close their scenes and run **Tools > Codecks > Set Up Imported Samples**. It creates/selects `Assets/Resources/Codecks/CodecksSettings.asset` and assigns that asset to missing settings references in standard imported sample scenes under `Assets/Samples/`. Repeated setup preserves the existing asset/token and existing explicit references. Open sample scenes are skipped (including clean scenes); close them and rerun. Other user scenes are never edited or saved. Configure the selected asset's scoped report token and endpoint once.

`CodecksCardCreator.settings` is **required**. There is no automatic Resources lookup or token-file/component fallback, even if a conventional asset exists. Custom/revised scenes outside the standard imported sample location must explicitly reference the desired settings asset; the bootstrap intentionally does not rewrite them. **Tools > Codecks > Create or Select Report Settings** remains available for asset-only setup.

An empty/whitespace token or invalid endpoint fails clearly before network dispatch. Endpoints must be absolute HTTP(S) URLs without credentials, query or fragment. Timeout remains the existing fixed 30 seconds.

Migration: `defaultToken` and `codecksURL` are hidden obsolete fields retained only for source/serialized compatibility and are ignored. Legacy `Resources/Codecks/codecksToken.txt` is no longer read. Manually configure the settings asset and assign references (or use the imported-sample bootstrap); no token is copied automatically. Existing configured settings assets at the default path are reused, not duplicated or reset.

Resources/ScriptableObject credentials remain extractable from client builds. Keep configured assets/tokens out of source control; use scoped revocable report tokens, never access keys. This is not a secure secret store.
An **access key** is more sensitive: keep it out of builds and source control. `CodecksTokenCreator.CreateAndSetNewToken` is for a trusted editor or build pipeline and writes a legacy report-token resource that no longer configures the reporter. Trusted tooling must configure the explicitly referenced settings asset instead; do not ship a build expecting token-file fallback. This package does not provide a secure secret store.

Reports can include optional severity, email, metadata, and a screenshot attachment. The standard forms wait until the end of the current frame before capture so the overlay is not included. If screenshot capture is unavailable, they warn and submit without an attachment. The forms require at least ten report characters and surface retryable submission failures to the user.

## Customize safely

Do not edit files in PackageCache. Import a sample, copy the consumer-owned UI assets/scripts into `Assets`, and subclass the provided component for project data:

- Override `CodecksCardCreatorForm.GetMetaText()` for uGUI.
- Override `CodecksUIToolkitFeedbackController.GetMetadata()` for UI Toolkit.

Both forms preserve `ShowCodecksForm()` for default capture/UnityEvents and add immediate `ShowCodecksForm(byte[], CodecksCardCreator.CodecksFileType)` (JPG/PNG) and `ShowCodecksFormWithoutScreenshot()`. Use a screenshot captured before opening a pause menu, or skip capture entirely. Both expose overridable `CaptureScreenshot()` alongside metadata hooks; Toolkit's sample launcher is optional for external menus/hotkeys. See the [consumer-controlled capture examples](Documentation~/index.md#consumer-controlled-capture-and-opening) for encoding/ownership and session rules.

Version 0.2.0 exposes matching modal state/events, current-session submitting state and an optional caller scope on both forms. The project owns pause, keyboard/gamepad command routing and focus restoration; Toolkit input boundaries stop bubble-phase pointer events only. See [modal integration](Documentation~/index.md#consumer-owned-modal-integration) for exact ordering and limitations.

`CodecksCardCreator.CreateNewCard` is also available for a completely custom UI. Preserve the public component names, serialized references, and callback behavior when migrating an existing integration.

## Migration from the old folder install

1. Back up project-owned customizations and revoke any token that was committed or shared unintentionally.
2. Remove the old copied plugin folder only after its scenes, prefabs, and scripts have been migrated; do not delete assets still referenced by your project.
3. Install this package through Package Manager and import the matching sample.
4. Reattach existing `CodecksCardCreator` and `CodecksCardCreatorForm` references where appropriate, then move custom subclasses and UI assets into `Assets`.
5. For a UI Toolkit integration, replace `UIDocument`-based assumptions with a Panel Renderer and its reload callback lifecycle.
6. Run the imported-sample bootstrap with sample scenes closed, then configure the selected settings asset. Custom scenes must explicitly reference it. Legacy token/endpoint fields no longer configure requests.
7. Test report submission with a disposable token before releasing a player build.

Existing runtime script GUIDs and public component names are preserved for the legacy uGUI integration, but sample assets are imported into the consumer project and should be treated as the new customization point.

## Tested support and current limits

- Minimum package declaration: Unity 6000.5. A clean 6000.5.5 project compiled the package and verified the uGUI/TMP sample.
- Primary behavior checks use Unity 6000.6.0f1, including shared configuration, localhost create/upload and graphics-capable form checks. They do not establish live-service success for every environment.
- Windows x86-64 standalone player validation passed on Unity 6000.6.0f1 using the Mono scripting backend. A disposable player submitted a localhost report body, high severity, email, and multipart text attachment; the player observed one callback and the endpoint observed one create request plus one upload request. This evidence covers `StandaloneWindows64` with Mono only. IL2CPP is optional and has not been validated.
- Screenshot capture depends on a graphics-capable runtime. When it fails, reports are sent without a screenshot rather than blocking the form.
- Destroying a `CodecksCardCreator` while a report or attachment upload is in progress cancels that operation, disposes its request, and calls its result delegate once with a failure. Keep the creator alive until its callback when the caller needs the report result.

## Attribution, branding, and license

The retained [MIT license](LICENSE.md) preserves Codecks GmbH's original copyright notice. The original repository is [codecks-io/codecks-unity](https://github.com/codecks-io/codecks-unity); this fork adds UPM packaging, reliability work, and the UI Toolkit sample.

The included Codecks naming and visual assets originate in the upstream plugin. Retain the visible Powered by Codecks image beside the report form when adapting the samples. The software license is not a trademark license; do not imply Codecks endorsement.
