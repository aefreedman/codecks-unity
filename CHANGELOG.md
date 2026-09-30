# Changelog

All notable changes to this independent UPM fork are documented here. Releases use matching package versions and `v<version>` Git tags. Version 0.x APIs may change between minor releases; migration guidance accompanies breaking changes.

## 0.1.0

### Added

- UPM packaging for Unity 6000.5 and later, with independently importable uGUI and Panel Renderer UI Toolkit feedback samples.
- Shared `CodecksSettings` configuration and Editor actions to create/select settings and wire missing references in closed standard imported sample scenes. Existing configuration and references are preserved.
- Matching form APIs to open with caller-owned JPG/PNG bytes or without a screenshot, bypassing default capture. Existing no-argument capture and UnityEvent bindings remain supported.
- Overridable screenshot capture and metadata hooks; optional Toolkit launcher for project-owned menus and hotkeys.
- Request validation, bounded timeouts, safe diagnostics, once-only completion and request cleanup.
- Consumer setup, styling, embedding, customization and migration documentation.

### Changed

- Reporters require an explicit `CodecksSettings` reference. Automatic Resources lookup and legacy token-file/component fallbacks are removed. Hidden obsolete token/endpoint fields remain for source/serialized compatibility but cannot configure requests.
- Installation uses Package Manager rather than copying plugin folders. Original uGUI runtime names and script GUIDs are preserved.

### Fixed

- Toolkit binding recovery after renderer, source asset or Panel Settings changes; stale-session isolation during reopen and delayed completion.
- Screenshot capture before the feedback overlay, with report-only fallback when default capture is unavailable.
- Toolkit camera, dark/purple layout, white form text, small-window scrolling and visible Powered by Codecks branding with preserved logo aspect ratio.
- Partial attachment failures are distinguished from successful card creation; retry guidance warns that another report may be created.

### Known limits

- Windows x86-64 Mono is the validated player target. IL2CPP and other player targets/backends are unvalidated. Minimum-editor evidence covers compilation/import, not all runtime behavior.
- Default screenshot capture requires a graphics-capable runtime. Capture overrides follow the documented platform encoding contract; caller-supplied bytes must match the explicit JPG/PNG type.
- Settings assets in client builds do not keep report tokens secret. Use scoped revocable tokens and keep configured assets out of source control.
- Reopening a form invalidates old UI callbacks, not already-dispatched backend requests.
