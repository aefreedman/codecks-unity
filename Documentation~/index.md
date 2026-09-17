# Codecks Bug & Feedback Reporter for Unity

An independently maintained UPM fork of the [Codecks Unity plugin](https://github.com/codecks-io/codecks-unity), originally developed by Codecks GmbH and released under the [MIT license](../LICENSE.md). This fork is not affiliated with or endorsed by Codecks GmbH.

This Markdown manual is the maintained consumer documentation. The former PDF is not a supported release artifact.

## Install through Package Manager

The package requires Unity 6000.5 or later and Git on the machine that resolves packages.

1. Open **Window > Package Manager**.
2. Select **Add package from git URL**.
3. Enter `https://github.com/aefreedman/codecks-unity.git`.
4. Select the package, open **Samples**, and import the implementation you need.

The package is currently prerelease (`0.1.0-pre.1`) and does not have an approved release tag. After a tag is approved and published, use `https://github.com/aefreedman/codecks-unity.git#v<package-version>` to pin a release. Do not use an untagged branch as a stable dependency.

## Samples

### Feedback Reporter (uGUI)

Import **Feedback Reporter (uGUI)**, then open `CodecksSampleScene`. It uses Canvas, the existing `CodecksCardCreatorForm`, and TextMesh Pro. Set `Canvas/CardCreator > CodecksCardCreator > Default Token` to a report token before entering Play mode.

If text is missing, import **Window > TextMeshPro > Import TMP Essential Resources**. The sample uses the standard TMP Liberation Sans font asset. Its EventSystem uses `StandaloneInputModule`, which supports the legacy Input Manager or **Active Input Handling: Both**. New-Input-System-only projects must replace that module with `InputSystemUIInputModule`, configure its UI actions, and keep only one input module enabled.

Subclass `CodecksCardCreatorForm` in `Assets` and override `GetMetaText()` to append project metadata without editing PackageCache. Preserve the serialized UI references when replacing the form component.

### Feedback Reporter (UI Toolkit)

Import **Feedback Reporter (UI Toolkit)**, then open `CodecksUIToolkitFeedbackReporterScene`. It needs no TMP resources or EventSystem. The form is a runtime UI Toolkit integration built on **Panel Renderer**, not `UIDocument`.

The sample GameObject has a Panel Renderer, `CodecksCardCreator`, and `CodecksUIToolkitFeedbackController`. Its Panel Renderer uses `CodecksFeedbackReporter.uxml` as the Visual Tree Asset and `CodecksFeedbackPanelSettings` as Panel Settings. The supplied Panel Settings references a sample-owned runtime theme.

To embed the form in a project:

1. Copy the UXML, USS, controller, and branding asset from the imported sample into `Assets`.
2. Add `PanelRenderer`, `CodecksCardCreator`, and `CodecksUIToolkitFeedbackController` to a GameObject.
3. Assign the copied Visual Tree Asset and Panel Settings to the Panel Renderer, and the same `CodecksCardCreator` to the controller.
4. Preserve the required UXML element names: `codecks-feedback-launcher`, `codecks-feedback-overlay`, `codecks-feedback-report`, `codecks-feedback-severity`, `codecks-feedback-email`, `codecks-feedback-send`, `codecks-feedback-cancel`, and `codecks-feedback-status`.
5. Restyle the `codecks-feedback-*` classes in the copied USS. When several Panel Renderers share Panel Settings, choose explicit sort orders. The supplied controller focuses the report field on open.

The controller registers a Panel Renderer reload callback and rebuilds bindings when its UI reloads. Keep that lifecycle if changing the source asset or Panel Settings. Add project metadata in a consumer-owned subclass by overriding `GetMetadata()`.

## Tokens, privacy, and security

A report token can be embedded in a player build. Set `CodecksCardCreator.defaultToken`, or provide `Resources/Codecks/codecksToken.txt`; the resource token wins when both exist. Do not commit report tokens, and revoke a token if it is exposed.

An access key must never ship in a player or source repository. Use `CodecksTokenCreator.CreateAndSetNewToken` only from a trusted editor or build process, wait for its callback, and protect the access key with the build system's secret handling. The package has no secure secret-storage facility.

Reports may send a description, optional severity, optional email, platform/app-version metadata, and a screenshot. Configure report-token upload limits in Codecks. Obtain consent and apply your own privacy policy before collecting player email addresses, screenshots, or other personal/project data.

## Runtime behavior and platform limits

Both supplied forms require at least ten report characters. They capture a screenshot at the end of the current frame before showing the overlay; this avoids capturing the form itself. Screenshot capture needs a graphics-capable runtime. On capture failure, the form logs a warning and sends the report without an attachment.

The request path uses UnityWebRequest and supports report creation plus server-provided attachment uploads. Failed requests leave the form available for a retry. The uGUI form uses JPG attachments on standalone builds and PNG elsewhere; the UI Toolkit form follows the same policy.

The package has been compiled in Unity 6000.5.5 and behavior-checked in Unity 6000.6.0f1 with EditMode, localhost UnityWebRequest create/upload PlayMode, and graphics-capable form PlayMode coverage. A disposable Windows IL2CPP build was attempted on 6000.6.0f1, but Unity reported that its selected IL2CPP backend is not installed, so no IL2CPP player support or player visual behavior is claimed. A successful standalone IL2CPP build/run and a disposable-token live submission remain release gates; do not infer support for untested player platforms from editor checks.

## Migrate from a copied plugin folder

1. Back up project-specific forms, subclasses, and UI assets; revoke accidentally exposed credentials.
2. Identify scenes, prefabs, and scripts that reference the old copied plugin before removing it.
3. Install this UPM package and import the appropriate sample through Package Manager.
4. Move custom code and UI assets into `Assets`, reattach references, and use the protected metadata hooks rather than modifying PackageCache.
5. Convert any UI Toolkit integration to Panel Renderer plus its reload callback lifecycle; do not substitute `UIDocument` for the supplied embedding contract.
6. Validate with a disposable report token before shipping.

The existing uGUI runtime component names and script GUIDs are preserved for migration. Imported sample assets are consumer-owned and are the supported place to customize layouts and styles.

## Attribution, branding, and support

The [MIT license](../LICENSE.md) retains the original Codecks GmbH copyright notice. This fork's support channel is its [issue tracker](https://github.com/aefreedman/codecks-unity/issues); Codecks GmbH does not support or endorse this fork.

The upstream plugin supplied the bundled Codecks naming and visual assets. The MIT license does not itself grant trademark or logo rights. Codecks' published Terms of Service require entitlement to use trademark-protected content, and no separate public brand-use permission was identified during release preparation. Obtain permission before changing or redistributing branding in a way that requires it, and do not claim Codecks endorsement.

## Release policy

The version remains prerelease until clean branch-install verification, player validation, disposable-token live submission validation, branding permission review, and publication approval are complete. A future tag must match `package.json`. This documentation does not authorize tagging, pushing, or publishing.
