# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

## [v2.4.0] - 2026-07-15

### Added
- Added a non-blocking GitHub release check that shows an in-app update link only when a newer stable release is available.

### Changed
- Standardised the application and future release asset name as `ColumnPadStudio.exe` without version text in the filename.
- Split the main shell, column state, persistence, workflow service, Workflow Builder, and editor-menu code into focused files without changing their public behavior.
- Updated the solution grouping and repository guide so application and test projects are easier to navigate.

### Fixed
- Removed stale picture and column event subscriptions when those items are cleared or removed.
- Added defensive service-boundary checks around recovery and Workflow Builder dependencies.

### Tested
- Passed the Release build with no warnings or errors.
- Passed 32 domain checks and 289 app smoke checks.
- Published and launch-checked the portable single-file Windows executable.


## [v2.3.0] - 2026-07-15

### Added
- Added in-column picture placement with drag-and-drop insertion, movement, proportional resizing, front/behind-text layering, and native layout persistence.

### Changed
- Reworked default column sizing so columns fill the available editor space while preserving explicitly saved widths.
- Centralised checklist-row remapping across typing, paste, delete, replace, and undo operations.
- Strengthened workflow import validation, dirty-state tracking, save prompts, and load warnings.
- Hardened native layout and recovery validation so invalid data is rejected without replacing valid workspace state.

### Fixed
- Fixed pictures appearing as blank white panels even when their image files loaded correctly.
- Fixed picture resizing jitter by calculating each resize from a stable drag origin.
- Fixed single-text mode transitions so column objects and their richer state are preserved.
- Removed the obsolete picture tray controls and kept one in-column picture system.

### Tested
- Passed the Release build, 32 domain checks, and 278 smoke checks.
- Visually verified picture loading and rendering in the published single-file Windows app.

## [v2.2.4] - 2026-07-09

### Changed
- Cleaned Workflow Builder file actions so saved library workflows, JSON import, and readable exports are grouped more clearly.
- Moved workflow readable export formatting into a focused service file while keeping the same public workflow export behavior.
- Split workflow starter template definitions and builder helpers into clearer workflow files.
- Reworked the README into a download-first app page for normal users.

### Fixed
- Fixed Workflow Builder window ownership so minimizing ColumnPad no longer minimizes Workflow Builder.
- Fixed the Workflow Builder inspector panel so details align at the top instead of floating halfway down the right panel.

### Tested
- Added smoke checks for Workflow Builder independence, the grouped export action, unique workflow starter IDs, starter node presence, and starter connection wiring.

## [v2.2.3] - 2026-06-13

### Added
- Added a ColumnPad-specific app-building standard that maps the app's idea, scope, structure, storage, safety, testing, release, maintenance, upgrade, and migration rules.
- Added a small workflow identity rule so workflow, node, and link IDs are normalized separately from visible labels.

### Changed
- Updated public documentation so image attachments, readable workflow exports, and the new app-building standard are reflected.
- Centralised the crash-log storage folder through `AppStoragePaths`.
- Pinned the release publish profile to the cached .NET 8 runtime pack so self-contained publishing does not fail when NuGet cannot reach the latest runtime patch online.
- Documented the `Domain/Text` rule layer in the repository structure guide.
- Routed workflow JSON opened from the main File menu into the existing Workflow Builder import path instead of treating it as an invalid layout.

### Fixed
- Hardened atomic text saving so app-owned saves create missing target folders and clean up temporary files.
- Fixed Release publishing so the domain project does not place a loose `.pdb` beside the single EXE release asset.
- Fixed workflow ID cleanup so internal IDs are trimmed without being treated like user-facing names.
- Removed no-op workflow normalization lines so workflow cleanup code only does real work.

### Removed
- Removed the stale root cleanup diary so the repo keeps only current app docs and release notes.

## [v2.2.2] - 2026-06-05

### Changed
- Added overflow scroll arrows to the workspace tab strip; they appear only when workspace tabs no longer fit.
- Restored direct File menu export commands for clean `.txt` and `.md` workspace exports.
- Simplified the column-header right-click menu so global reset-all actions stay in the main Columns menu.
- Tidied the View menu editor-font panel so it uses a neutral embedded menu panel instead of looking like a large highlighted menu row.
- Added a shared embedded-menu panel style for real controls hosted inside menu dropdowns.
- Tightened repo ignore rules for local config, `.env` files, certificates, and private key files.

### Fixed
- Fixed duplicate-column behaviour so gutter mode and checklist checked rows are preserved.
- Fixed search result line numbers for text containing standalone carriage returns.
- Fixed preference saving to use the same atomic write path as other app-owned JSON storage.
- Fixed View menu theme handlers so current theme labels map directly to the correct theme presets.

## [v2.2.1] - 2026-05-31

### Changed
- Reworked the editor line-number gutter from a second scrolling text box into a lightweight rendered gutter for smoother selection scrolling.
- Renamed the writing-strip language control to `Proofing` so it describes spell-check/proofing language rather than translation.
- Kept the existing proofing language range while adding clearer status/tooltip wording about Windows/WPF dictionary availability.
- Updated README wording around ColumnPad's purpose, proofing, and single-exe releases.

### Fixed
- Restored spelling suggestions and `Ignore All` to the custom editor right-click menu.
- Fixed line-count and checklist metadata handling for pasted or loaded text that contains standalone carriage returns.
- Fixed gutter refresh/sync after font, wrap, style, load, paste, and line-number changes.
- Improved inactive text-selection visibility while scrolling.

## [v2.2.0] - 2026-05-28

### Changed
- Reworked Workflow Builder into a canvas-first layout with workflow starters, a saved-workflow library, node palette, and focused inspector.
- Expanded workflow starters with more ColumnPad-native writing, research, release, meeting, and decision workflows.
- Reduced the main toolbar into a smaller writing-options strip for spell check, language, and lined paper.
- Moved editor font and theme controls into the View menu so the top UI has one clearer command structure.
- Split editor interaction code into focused keyboard, gutter, menu, and paste partial files.
- Split shared menu and context-menu styling into its own resource dictionary.
- Updated docs to match the current invisible right-edge column resize behavior.

### Fixed
- Fixed Markdown editorconfig line endings to match repository attributes.
- Fixed dark-mode lined paper so the editor paper background follows the selected theme.

## [v2.1.0] - 2026-05-24

### Added
- Added per-node workflow colours with a right-click menu, drag movement in the workflow preview, and JSON persistence for chosen node colours.
- Added crash logging for unexpected WPF errors.
- Added a UI QA checklist for theme, context-menu, editing, file, and workflow-builder checks.

### Changed
- Split the oversized main shell into focused partial files for shell core, file/session/recovery work, editor commands, and workspace-tab wiring.
- Split shell editor-surface commands into focused files for column actions, search/replace, view modes, and keyboard shortcuts.
- Split file-session shell logic into focused files for lifecycle/recovery, destructive confirmations, workspace-session JSON, save-before-exit, and file/export commands.
- Split `MainViewModel` into focused partial files for core state/properties, column actions, file-state tracking, document persistence, font/language setup, layout migration, JSON helpers, and layout schema records.
- Split the workflow builder view model into focused partial files for library/template actions, node/link editing, and live preview wiring.
- Split the editor control into focused partial files for core setup, line-number lifecycle rendering, and editor interaction behaviour.
- Centralised theme resources and persisted app theme selection so the chosen mode stays across launches.
- Split WPF resources into theme brushes and reusable control styles.
- Centralised app storage path helpers so workflow/recovery/preference storage no longer depends on view-model code.
- Centralised atomic file writing for workflow and recovery saves.
- Cleaned repo documentation so startup flow, architecture, release flow, and folder responsibilities are clearer.

### Fixed
- Fixed single-line text inputs inheriting editor-like scrollbar behavior from shared textbox styling.
- Fixed theme/recovery loading so recovered and opened workspaces preserve the current app theme consistently.
- Fixed context-menu hover styling and removed the blank blue left gutter from right-click dropdowns.
- Fixed direct save behavior after opening native `.columnpad.json` layout files.
- Fixed workflow link selection visibility.
- Fixed prompt dialog theme consistency.


## [v1.3.1] - 2026-03-12

### Changed
- Clarified README documentation to better explain ColumnPad's core writing workflow, workspace model, and release usage.

### Fixed
- Fixed clipboard paste normalization so copied content keeps expected line breaks instead of fragmenting into broken rows.
- Fixed inline workspace/column rename behavior so typing replaces selected rename text instead of appending unexpectedly.

## [v1.3.0] - 2026-03-12

### Added
- Added a domain project (`src/ColumnPadStudio.Domain`) and domain test suite for cleaner separation of parsing and workspace rules.
- Added dedicated services for file workflow routing, workspace session persistence, lifecycle naming/closing, and text search.
- Added diagram-first workflow templates with node/link modeling and cleaner builder editing tools.

### Changed
- Reconstructed editor line-marker behavior so gutter numbering, bullet/checklist modes, and clean typing text no longer conflict.
- Rebuilt Workflow Builder into a cleaner diagram-focused UI while preserving import/export, templates, and editing controls.
- Refactored `MainViewModel` and related wiring to remove dead logic and improve maintainability.

### Fixed
- Fixed lined-paper and text alignment issues so typed text and line rows stay visually flush.
- Fixed Enter/new-line behavior for normal writing flow across columns.
- Fixed marker-mode behavior so bullets/checklists are rendered in gutter mode without duplicating symbols in text content.
- Fixed selection scoping so active interactions stay isolated to the selected column/editor.
- Fixed workspace/session load and import paths to preserve structure and avoid inline/flattened legacy text regressions.

## [v1.2.3] - 2026-03-11

### Fixed
- Removed hard MaxWidth/MaxHeight work-area caps from the main window so maximized layout fully fills the desktop work area without a bottom black strip.

## [v1.2.2] - 2026-03-11

### Fixed
- Fixed a startup crash in the release executable caused by WPF language binding conversion on the editor control.
- Explicitly set ConverterCulture on the editor Language binding to ensure safe conversion from language tags (for example en-US).

## [v1.2.1] - 2026-03-11

### Fixed
- Improved Windows launch reliability for the single-file release executable by disabling single-file compression and forcing full self-extract content.

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
