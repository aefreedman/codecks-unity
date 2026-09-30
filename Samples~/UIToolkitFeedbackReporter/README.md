# Feedback Reporter (UI Toolkit)

Import this sample from **Window > Package Manager > Codecks Bug & Feedback Reporter > Samples**, then open `CodecksUIToolkitFeedbackReporterScene`. The sample uses runtime UI Toolkit only: it does not require the uGUI sample or TMP Essential Resources. It contains no report token or credentials.

## Run the standalone sample

1. Import the sample and keep its scene closed while performing setup below.
2. Before opening the sample, run **Tools > Codecks > Set Up Imported Samples** with standard imported sample scenes closed. It creates/selects `Assets/Resources/Codecks/CodecksSettings.asset` and wires missing references. Configure the scoped report token/endpoint once. Repeated setup preserves assets and references; open scenes are skipped, not saved. The backend requires an explicit **Settings** reference: automatic Resources and legacy token-file/component fallbacks are removed. Hidden obsolete legacy fields are ignored. For custom/revised scenes outside `Assets/Samples/`, explicitly assign the settings asset; bootstrap never rewrites those scenes. No token is copied automatically. Configured assets are extractable in builds and must not be committed.
3. Open the sample scene, enter Play mode, select **Give Feedback**, write at least ten characters, choose severity, optionally enter an email address, and select **Send Report**.

The standalone scene includes a camera with a solid background, matching the uGUI example. A camera must render the game frame for meaningful screenshots; a panel alone does not clear/render the background.

The GameObject uses a **Panel Renderer**, not `UIDocument`. Its **Visual Tree Asset** is `CodecksFeedbackReporter.uxml`, its **Panel Settings** is `CodecksFeedbackPanelSettings`, and the controller and backend are on the same GameObject. The included PanelSettings uses the sample-owned `CodecksDefaultRuntimeTheme.tss`, so it has no dependency on project UI Toolkit assets. Raise Panel Renderer **Sort Order** if the form must render above another panel. UI Toolkit input uses the active runtime UI input setup for the project; no EventSystem or TMP resource is required by this sample.

When the launcher is selected, the controller waits through the current frame and captures a screenshot before the overlay is displayed. If capture is unavailable, it logs a warning and sends the report without an attachment. The form keeps the Panel Renderer reload subscription while closed, so it can reopen after closing, enabling the renderer, or assigning a new source asset or panel settings.

## Embed and restyle

Copy the UXML, USS, controller, and branding asset into your own `Assets` folder so they remain consumer-owned across package updates. Add `PanelRenderer`, `CodecksCardCreator`, and `CodecksUIToolkitFeedbackController` to a GameObject. Assign your copied UXML to **Visual Tree Asset** and your PanelSettings to **Panel Settings**, then assign the same `CodecksCardCreator` to the controller. The UXML named elements below are required by the controller. The optional `codecks-feedback-launcher` button may be removed when your menu/hotkey calls the public API:

- `codecks-feedback-overlay`
- `codecks-feedback-report`, `codecks-feedback-severity`, `codecks-feedback-email`
- `codecks-feedback-send`, `codecks-feedback-cancel`, `codecks-feedback-status`

When embedding, use your game's existing camera; do not add the standalone sample's camera on top of your existing rendering setup. The scrollable dialog keeps controls accessible in small windows. The independently imported `powered-by-codecks.png` is referenced by the USS (not a text substitute).

Restyle the documented `codecks-feedback-*` classes in your copied USS. If sharing a PanelSettings asset with another Panel Renderer, set intentional sort orders and ensure focus moves to the report field when opening; the controller does this for the supplied template. Keep the visible `codecks-feedback-brand` attribution when adapting the form.

To add project data without changing the package cache, inherit from `CodecksUIToolkitFeedbackController` in your project and override `GetMetadata()`. You may also override the existing `protected virtual byte[] CaptureScreenshot()` hook; it must return the default platform encoding (standalone JPG, otherwise PNG), or null. No-argument opening still waits for end-of-frame even with an override.

## External opening / caller-owned screenshots

Both this controller and uGUI `CodecksCardCreatorForm` expose the same opening API:

```csharp
reporter.ShowCodecksForm(); // Existing UnityEvent binding: default capture, then open.
reporter.ShowCodecksForm(encodedPng, CodecksCardCreator.CodecksFileType.PNG);
reporter.ShowCodecksFormWithoutScreenshot(); // Immediate: no capture and no attachment.
```

The supplied-image and no-image modes open immediately without waiting for end-of-frame and invalidate pending captures/old UI callbacks. They start a new form session, even if it was already open. JPG/PNG filenames and MIME types follow the explicit encoding, not the target platform. Null/empty bytes or another file type throw argument exceptions before opening; the caller is responsible for valid encoding (the controller does not decode it).

Capture before your pause menu is shown, then pass the encoded bytes. Own/dispose your capture texture yourself; the controller retains but never modifies/disposes the byte array, so keep it unchanged while submission is using it. Default capture alone creates/destroys its temporary texture. Closing/reopening does not cancel a report already dispatched, but its late callback cannot overwrite the new session. Call after the enabled Panel Renderer has initialized bindings. Your own launcher/menu/hotkey can use these methods without a sample launcher element.

The controller uses `CodecksCardCreator.CreateNewCard`, preserving its token, request, upload, and retryable error behavior. Success is shown only after card creation **and** attachment upload complete. An attachment failure can leave a real card on the service: the form explicitly warns that retrying creates another report. A connection error alone does not identify the remote service cause.

## Local regression checks

Import the sample tests and run the sample EditMode fixture plus PlayMode graphics/submission fixtures on a Windows standalone target with graphics enabled. The submission fixture uses only localhost HTTP endpoints and a dummy token; it opens the real standalone scene, decodes and checks captured background pixels, compares the multipart image to the queued capture, checks success/partial-failure UI, duplicate-send prevention, operation cleanup, and stale-session completion. It writes visual evidence to `Evidence/` in the disposable test project. No service submission is made by these tests.
