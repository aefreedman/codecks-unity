# Changelog

All notable changes to this independent UPM fork are documented here.

## [Unreleased]

### Changed

- Reporters now require an explicit `CodecksSettings` reference. Automatic Resources lookup and legacy token-file/component fallbacks are removed; hidden obsolete token/endpoint members remain only for migration compatibility and cannot configure requests.
- **Tools > Codecks > Set Up Imported Samples** creates/reuses the default settings asset and wires missing references in closed standard imported sample scenes. It preserves configured assets/references and skips open or non-sample scenes.

### Added

- Shared `CodecksSettings` ScriptableObject configuration for both forms.
- **Tools > Codecks > Create or Select Report Settings** creates/selects the default asset without overwriting existing configuration.

### Fixed

- Made Toolkit captions/help/privacy and inherited form text white against dark form backgrounds.
- Preserved the Toolkit logo's source aspect ratio by disabling power-of-two rescaling during import and using bounded, centered contain sizing.
- Included a rendering camera in the standalone UI Toolkit sample so screenshots contain the game background without manual scene setup.
- Matched the UI Toolkit form's layout, copy and colors more closely to the uGUI sample, with scrollable controls for small windows and the actual Powered by Codecks image beside the form.
- Distinguished card creation from attachment upload failure in the UI Toolkit status; retrying an attachment failure can create another report.

## 0.1.0-pre.1 (unreleased prerelease)

### Added

- UPM package metadata targeting Unity 6000.5 or later.
- Independently importable uGUI and runtime UI Toolkit feedback reporter samples.
- A Panel Renderer-based UI Toolkit template with a sample-owned runtime theme and reload-safe bindings.
- Request validation, bounded timeouts, safe error handling and request cleanup.

### Changed

- Replaced copied-folder installation guidance with Package Manager Git installation and sample import guidance.
- Documented consumer-owned customization hooks for uGUI metadata and UI Toolkit metadata/styling.
- Preserved legacy uGUI runtime public names and script GUIDs for migration.

### Known limits

- Windows x86-64 Mono is the validated standalone target. IL2CPP and other targets/backends are unvalidated.
- Screenshot capture is optional: a capture failure logs a warning and sends the report without an attachment.
