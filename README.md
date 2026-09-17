# Codecks Bug & Feedback Reporter for Unity

An independently maintained UPM fork of the [Codecks Unity plugin](https://github.com/codecks-io/codecks-unity), originally developed by Codecks GmbH and released under the [MIT license](LICENSE.md). This fork is not affiliated with or endorsed by Codecks GmbH.

This package provides uGUI and runtime UI Toolkit feedback forms backed by `CodecksCardCreator`. It targets Unity 6000.5 or later. Read the [consumer manual](Documentation~/index.md) and [changelog](CHANGELOG.md). Fork-specific support belongs in this repository's [issue tracker](https://github.com/aefreedman/codecks-unity/issues), not Codecks support.

## Install

1. Ensure Git is installed and available on the machine that resolves Unity packages.
2. In Unity, open **Window > Package Manager**, select **Add package from git URL**, and enter:

   ```text
   https://github.com/aefreedman/codecks-unity.git
   ```

3. Open the package's **Samples** tab and import one or both samples:
   - **Feedback Reporter (uGUI)** for the existing Canvas/TMP implementation.
   - **Feedback Reporter (UI Toolkit)** for the Panel Renderer implementation.

The package is currently `0.1.0-pre.1`; no release tag has been approved. Do not treat an untagged branch URL as a stable release. After release approval and publication, install the approved pinned tag using the form `https://github.com/aefreedman/codecks-unity.git#v<package-version>`.

## Choose an implementation

### uGUI

The uGUI sample contains `CodecksSampleScene`, a Canvas form, and TextMesh Pro fields. After importing it, assign a report token to `Canvas/CardCreator > CodecksCardCreator > Default Token`. If TMP text is missing, import **Window > TextMeshPro > Import TMP Essential Resources**; the sample uses TMP's standard Liberation Sans asset.

The sample EventSystem uses `StandaloneInputModule`, suitable for the legacy Input Manager or **Active Input Handling: Both**. In a new-Input-System-only project, replace it with `InputSystemUIInputModule`, configure that module's UI actions, and do not enable both modules on one EventSystem. See the [uGUI sample guide](Samples~/FeedbackReporter/README.md) for metadata customization and setup details.

### UI Toolkit

The UI Toolkit sample contains `CodecksUIToolkitFeedbackReporterScene`. Its form uses a **Panel Renderer**, not `UIDocument`; assign its Visual Tree Asset (UXML) and Panel Settings, then keep `CodecksCardCreator` and `CodecksUIToolkitFeedbackController` on the same GameObject. The supplied Panel Settings references a sample-owned runtime theme, so it does not rely on project UI Toolkit assets.

For embedding, copy the sample UXML, USS, controller, and branding into `Assets` before editing. Keep the controller's required named elements, use explicit Panel Renderer sort orders when sharing Panel Settings, and retain the reload callback lifecycle so bindings are rebuilt after the source asset or Panel Settings changes. The supplied controller focuses the report field when opening. See the [UI Toolkit sample guide](Samples~/UIToolkitFeedbackReporter/README.md) for required names, styling hooks, and metadata customization.

## Tokens and report data

A **report token** may be embedded in a player build. Set it in `CodecksCardCreator.defaultToken`, or create `Resources/Codecks/codecksToken.txt`; the resource token takes precedence. Keep report tokens scoped and revocable, configure upload limits in Codecks, and do not commit tokens to source control.

An **access key** is more sensitive: keep it out of builds and source control. `CodecksTokenCreator.CreateAndSetNewToken` is for a trusted editor or build pipeline and writes a report token resource; wait for its callback before continuing a build. This package does not provide a secure secret store.

Reports can include optional severity, email, metadata, and a screenshot attachment. The standard forms wait until the end of the current frame before capture so the overlay is not included. If screenshot capture is unavailable, they warn and submit without an attachment. The forms require at least ten report characters and surface retryable submission failures to the user.

## Customize safely

Do not edit files in PackageCache. Import a sample, copy the consumer-owned UI assets/scripts into `Assets`, and subclass the provided component for project data:

- Override `CodecksCardCreatorForm.GetMetaText()` for uGUI.
- Override `CodecksUIToolkitFeedbackController.GetMetadata()` for UI Toolkit.

`CodecksCardCreator.CreateNewCard` is also available for a completely custom UI. Preserve the public component names, serialized references, and callback behavior when migrating an existing integration.

## Migration from the old folder install

1. Back up project-owned customizations and revoke any token that was committed or shared unintentionally.
2. Remove the old copied plugin folder only after its scenes, prefabs, and scripts have been migrated; do not delete assets still referenced by your project.
3. Install this package through Package Manager and import the matching sample.
4. Reattach existing `CodecksCardCreator` and `CodecksCardCreatorForm` references where appropriate, then move custom subclasses and UI assets into `Assets`.
5. For a UI Toolkit integration, replace `UIDocument`-based assumptions with a Panel Renderer and its reload callback lifecycle.
6. Test report submission with a disposable token before releasing a player build.

Existing runtime script GUIDs and public component names are preserved for the legacy uGUI integration, but sample assets are imported into the consumer project and should be treated as the new customization point.

## Tested support and current limits

- Minimum package declaration: Unity 6000.5. A clean 6000.5.5 project compiled the package and verified the uGUI/TMP sample.
- Primary behavior checks: Unity 6000.6.0f1 has 16 runtime EditMode tests, 4 imported UI Toolkit EditMode tests, loopback UnityWebRequest create/upload PlayMode tests, and graphics-capable PlayMode form tests. The loopback endpoint is bound to localhost and uses no credentials.
- Windows standalone IL2CPP build/run was attempted in a disposable Unity 6000.6.0f1 project, but Unity reported that the selected IL2CPP backend is not installed. Therefore no IL2CPP player support or player visual behavior is claimed.
- Screenshot capture depends on a graphics-capable runtime. When it fails, reports are sent without a screenshot rather than blocking the form.
- Destroying a `CodecksCardCreator` while a report or attachment upload is in progress cancels that operation, disposes its request, and calls its result delegate once with a failure. Keep the creator alive until its callback when the caller needs the report result.

## Attribution, branding, and license

The retained [MIT license](LICENSE.md) preserves Codecks GmbH's original copyright notice. The original repository is [codecks-io/codecks-unity](https://github.com/codecks-io/codecks-unity); this fork adds UPM packaging, reliability work, and the UI Toolkit sample.

The included Codecks naming and visual assets originate in the upstream plugin. MIT licensing covers the software and documentation, but it is not a trademark or logo license. A review of Codecks' published Terms of Service found no separate public brand-use grant; those terms require users to be entitled to use trademark-protected content. Confirm the necessary permission before redistributing modified branding or implying Codecks endorsement.

## Release policy

Versions remain prereleases until the release gates above, branch-install verification, branding permission review, and publication approval are complete. A release tag must match `package.json`; creating tags, pushing, and publishing are intentionally not performed by this package.
