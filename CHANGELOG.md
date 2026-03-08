# Changelog

All notable changes to this project are documented in this file.

## [Unreleased] - 2026-03-04

### Added
- Added `ColumnPadStudio.SmokeTests`, a minimal executable smoke-test project that validates core view-model behavior and layout JSON round-trip stability.
- Added `RELEASE_CHECKLIST.md` with end-to-end release verification steps.
- Added Notepad-style editor context menu actions (`Undo`, `Cut`, `Copy`, `Paste`, `Delete`, `Select All`).
- Added per-column `Paste Preset` options (`None`, `Bullets`, `Checklist`) with auto-formatting for pasted lines while preserving indentation.

### Changed
- Changed publish profile output to a repo-local folder: `.\ColumnPadStudio\publish\`.
- Updated README publish/smoke-test documentation for local release flow.
- Narrowed storage-related exception handling in `MainWindow.xaml.cs` to expected I/O exceptions.
- Made checklist/bullet marker prefix checks use `StringComparison.Ordinal` for deterministic matching.
- Updated layout JSON schema to persist per-column paste preset state.

### Fixed
- Consolidated dark-theme control foreground/background bindings so toolbar/menu/tab/button text remains readable in dark mode.
- Kept backward compatibility for legacy theme names (`Notepad Classic`, `High Contrast`, `Compact`) while using current names (`Light Mode`, `Dark Mode`, `Default Mode`).
- Improved list handling for indented lines (space-prefixed/nested content) when toggling bullets/checklists and continuing lists on Enter.
