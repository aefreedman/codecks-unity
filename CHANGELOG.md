# Changelog

All notable changes to this independent UPM fork are documented here. Versions remain prereleases until the documented release gates are approved.

## [Unreleased]

### Release gates

- Verify clean-project installation from the published branch and eventual pinned tag.
- Windows x86-64 `StandaloneWindows64` Mono build/run validation passed on Unity 6000.6.0f1: a disposable player submitted a localhost report body, high severity, email, and multipart attachment, with one callback observed by the player and one create plus one upload observed by the endpoint. IL2CPP is optional and unvalidated; it is not a release gate.
- Submit one report using a user-provided disposable token; no live report has been submitted during package preparation.
- Confirm permission for continued distribution or modification of Codecks branding beyond the software license.
- Obtain approval before creating a matching Git tag, pushing, or publishing.

## 0.1.0-pre.1 (unreleased prerelease)

### Added

- UPM package metadata targeting Unity 6000.5 or later.
- Independently importable uGUI and runtime UI Toolkit feedback reporter samples.
- A Panel Renderer-based UI Toolkit template with a sample-owned runtime theme and reload-safe bindings.
- Runtime request validation and focused EditMode coverage for report creation, upload handling, screenshots, and UI lifecycle behavior.
- Localhost UnityWebRequest PlayMode coverage for report creation and multipart attachment upload, plus graphics-capable PlayMode coverage for both supplied forms.

### Changed

- Replaced copied-folder installation guidance with Package Manager Git installation and sample import guidance.
- Documented consumer-owned customization hooks for uGUI metadata and UI Toolkit metadata/styling.
- Preserved legacy uGUI runtime public names and script GUIDs for migration.

### Known limits

- The package compiles in Unity 6000.5.5 and has focused behavior checks in Unity 6000.6.0f1 EditMode.
- Windows x86-64 Mono is the validated standalone target. IL2CPP remains optional and unvalidated; this does not imply support for other Windows architectures, targets, or backends. A live disposable-token submission remains an external release gate.
- Screenshot capture is optional: a capture failure logs a warning and sends the report without an attachment.
