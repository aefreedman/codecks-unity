# Feedback Reporter (UI Toolkit)

Import this sample from **Window > Package Manager > Codecks Bug & Feedback Reporter > Samples**, then open `CodecksUIToolkitFeedbackReporterScene`. The sample uses runtime UI Toolkit only: it does not require the uGUI sample or TMP Essential Resources. It contains no report token or credentials.

## Run the standalone sample

1. Open the scene and select `Codecks UI Toolkit Feedback Reporter`.
2. In `CodecksCardCreator`, set **Default Token** to a Codecks report token. A `Resources/Codecks/codecksToken.txt` token takes precedence when present.
3. Enter Play mode, select **Give Feedback**, write at least ten characters, choose severity, optionally enter an email address, and select **Send Report**.

The GameObject uses a **Panel Renderer**, not `UIDocument`. Its **Visual Tree Asset** is `CodecksFeedbackReporter.uxml`, its **Panel Settings** is `CodecksFeedbackPanelSettings`, and the controller and backend are on the same GameObject. The included PanelSettings uses the sample-owned `CodecksDefaultRuntimeTheme.tss`, so it has no dependency on project UI Toolkit assets. Raise Panel Renderer **Sort Order** if the form must render above another panel. UI Toolkit input uses the active runtime UI input setup for the project; no EventSystem or TMP resource is required by this sample.

When the launcher is selected, the controller waits through the current frame and captures a screenshot before the overlay is displayed. If capture is unavailable, it logs a warning and sends the report without an attachment. The form keeps the Panel Renderer reload subscription while closed, so it can reopen after closing, enabling the renderer, or assigning a new source asset or panel settings.

## Embed and restyle

Copy the UXML, USS, controller, and branding asset into your own `Assets` folder so they remain consumer-owned across package updates. Add `PanelRenderer`, `CodecksCardCreator`, and `CodecksUIToolkitFeedbackController` to a GameObject. Assign your copied UXML to **Visual Tree Asset** and your PanelSettings to **Panel Settings**, then assign the same `CodecksCardCreator` to the controller. The UXML named elements are required by the controller:

- `codecks-feedback-launcher`, `codecks-feedback-overlay`
- `codecks-feedback-report`, `codecks-feedback-severity`, `codecks-feedback-email`
- `codecks-feedback-send`, `codecks-feedback-cancel`, `codecks-feedback-status`

Restyle the documented `codecks-feedback-*` classes in your copied USS. If sharing a PanelSettings asset with another Panel Renderer, set intentional sort orders and ensure focus moves to the report field when opening; the controller does this for the supplied template. Keep the visible `codecks-feedback-brand` attribution when adapting the form.

To add project data without changing the package cache, inherit from `CodecksUIToolkitFeedbackController` in your project and override `GetMetadata()`. The controller uses `CodecksCardCreator.CreateNewCard`, preserving its token, request, upload, and retryable error behavior.
