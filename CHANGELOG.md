# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

## [v1.2.0] - 2026-03-11

### Added
- Added built-in workflow template catalog entries and JSON import/export actions in the Workflow Builder.
- Added lined-paper writing mode controls in both the View menu and top toolbar.

### Changed
- Reorganized repository layout into `src/` and `tests/` roots for clearer Visual Studio solution structure and scaling.
- Updated solution file grouping, project references, run/test/publish paths, and maintenance docs to match the new layout.
- Extended workflow schema support with category/version metadata and new step kinds for column count, spell check, language, and lined-paper settings.

### Fixed
- Fixed layout JSON parsing for new language/lined-paper fields and removed a merge artifact that broke compilation.
- Fixed Workflow Builder dialog imports/exports by wiring the required file-dialog namespace.

## [v1.1.4] - 2026-03-11

### Added
- Added `View -> Column Mode` with `Ctrl+Shift+2` to restore multi-column layout after entering single text mode.
- Added per-workspace memory of last multi-column count so column mode restores to the previous layout size.
- Added workflow builder scaffold files (models, service, viewmodel, and window) and wired the app menu entry.

### Changed
- Refined menu separator styling to remove odd extra divider gaps and improve dropdown visual consistency.
- Updated README structure and repository ignore/attributes files for cleaner GitHub maintenance.
- Refreshed `docs/columnpad-screenshot.png` with an updated in-app capture.

### Fixed
- Fixed startup safety around active workspace binding by hardening `ActiveVm` resolution and initialization ordering.
- Improved selection readability by reducing selection fill opacity while keeping highlighted text visible.

## [v1.1.3] - 2026-03-09

### Fixed
- Fixed text selection in the editor so highlighted words stay readable instead of turning into solid blue blocks while dragging.
- Adjusted light and default theme selection colors to use lighter highlight fills with dark selected text.
- Reduced selection opacity so highlighted text remains visible during click-and-drag selection.

## [v1.1.2] - 2026-03-09

### Changed
- Refreshed the GitHub screenshot with a clean blank-layout capture.
- Strengthened editor text selection contrast so selections stay clearly visible in every theme.
- Added version metadata to the Windows build and updated release docs for v1.1.2.

### Fixed
- Fixed imported text and markdown workspace exports so they open in a clean state instead of appearing dirty immediately.
- Fixed active-column status updates so renames and checklist progress refresh the status bar correctly.
- Fixed column focus and autosave width persistence so they do not overwrite transient action messages.
- Fixed open/save/import/export actions to report file I/O errors instead of failing abruptly.
- Fixed bullet and checklist formatting over mixed content so blank separator lines stay untouched.

## [v1.1.1] - 2026-03-08

### Changed
- Improved the GitHub landing page with a cleaner README layout and an in-app screenshot.

### Fixed
- Fixed editor text selection visibility so selected text remains readable instead of becoming a solid block.
- Applied editor-level selection and caret styling directly to the writing surface for more reliable theme behavior.
- Adjusted light and default theme selection text colors so highlighted text stays legible.

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
