# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

## [v1.1.0] - 2026-03-08

### Added
- Added multi-workspace auto-recovery with a manifest and per-workspace recovery files.
- Added save-before-exit prompts with format-aware dirty tracking.
- Added direct save-back support for opened `.txt` and `.md` files.
- Added clearer selected-column actions, including swap controls and better delete confirmations.
- Added release-ready single-file publish verification for the Windows build.

### Changed
- Refreshed the app branding, icon set, splash assets, and GitHub landing page presentation.
- Improved the toolbar, menus, and dropdown styling for dark mode and theme consistency.
- Updated the workspace and column UX to use clearer wording and more discoverable actions.
- Improved the release packaging flow so the published output can be shipped as a single `.exe`.

### Fixed
- Fixed unreadable dark-mode menu and toolbar dropdown text.
- Fixed `Esc` handling so it clears selection and exits toolbar dropdowns back to the editor instead of changing theme state.
- Fixed destructive actions so filled columns and unsaved workspace changes prompt before data is lost.
- Fixed active-column restore behavior when loading saved layouts.
- Fixed recovery behavior so all open workspaces can be restored instead of only the active one.

## [v1.0.0] - 2026-03-08

### Added
- Added `ColumnPadStudio.SmokeTests`, a minimal executable smoke-test project that validates core view-model behavior and layout JSON round-trip stability.
- Added `RELEASE_CHECKLIST.md` with end-to-end release verification steps.
- Added Notepad-style editor context menu actions (`Undo`, `Cut`, `Copy`, `Paste`, `Delete`, `Select All`).
- Added per-column `Paste Preset` options (`None`, `Bullets`, `Checklist`) with auto-formatting for pasted lines while preserving indentation.

### Changed
- Changed publish profile output to a repo-local folder: `./ColumnPadStudio/publish/`.
- Updated README publish and smoke-test documentation for local release flow.
- Narrowed storage-related exception handling in `MainWindow.xaml.cs` to expected I/O exceptions.
- Made checklist and bullet marker prefix checks use `StringComparison.Ordinal` for deterministic matching.
- Updated layout JSON schema to persist per-column paste preset state.

### Fixed
- Consolidated dark-theme control foreground and background bindings so toolbar, menu, tab, and button text remains readable in dark mode.
- Kept backward compatibility for legacy theme names (`Notepad Classic`, `High Contrast`, `Compact`) while using current names (`Light Mode`, `Dark Mode`, `Default Mode`).
- Improved list handling for indented lines when toggling bullets or checklists and continuing lists on Enter.