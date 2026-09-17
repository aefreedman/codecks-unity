# Changelog

All notable changes to this independent UPM fork are documented here. Versions remain prereleases until the documented release gates are approved.

## [Unreleased]

### Release gates

- Verify clean-project installation from the published branch and eventual pinned tag.
- Complete a graphics-capable player visual check and standalone IL2CPP build/run.
- Submit one report using a user-provided disposable token; no live report has been submitted during package preparation.
- Confirm permission for continued distribution or modification of Codecks branding beyond the software license.
- Obtain approval before creating a matching Git tag, pushing, or publishing.

## 0.1.0-pre.1 (unreleased prerelease)

### Added

- UPM package metadata targeting Unity 6000.5 or later.
- Independently importable uGUI and runtime UI Toolkit feedback reporter samples.
- A Panel Renderer-based UI Toolkit template with a sample-owned runtime theme and reload-safe bindings.
- Runtime request validation and focused EditMode coverage for report creation, upload handling, screenshots, and UI lifecycle behavior.

### Changed

- Replaced copied-folder installation guidance with Package Manager Git installation and sample import guidance.
- Documented consumer-owned customization hooks for uGUI metadata and UI Toolkit metadata/styling.
- Preserved legacy uGUI runtime public names and script GUIDs for migration.

### Known limits

- The package compiles in Unity 6000.5.5 and has focused behavior checks in Unity 6000.6.0f1 EditMode.
- Player visual validation, standalone IL2CPP build/run, and a live disposable-token submission remain incomplete release gates.
- Screenshot capture is optional: a capture failure logs a warning and sends the report without an attachment.
