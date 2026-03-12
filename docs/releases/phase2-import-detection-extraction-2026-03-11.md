# Phase 2 Import/Detection Domain Extraction Checkpoint (2026-03-11)

## Scope
- Goal: remove duplicated import/detection logic from UI and ViewModel layers by centralizing it in domain rules.
- Repo: ColumnPadStudio
- Branch: main

## Structural Changes
1. Added domain import/detection rules:
   - `src/ColumnPadStudio.Domain/Workspaces/WorkspaceImportRules.cs`
2. Added domain data contract:
   - `ImportedColumn` record struct

## Removed Duplicate Logic
- `MainViewModel` no longer owns local export parsers for text/markdown imports.
- `MainWindow` no longer owns local workspace-session JSON detection and export-format detection logic.

## Wiring Updates
- `MainViewModel.LoadFromExportText` now calls `WorkspaceImportRules.ParseTextExportColumns`.
- `MainViewModel.LoadFromExportMarkdown` now calls `WorkspaceImportRules.ParseMarkdownExportColumns`.
- `MainWindow.IsWorkspaceSessionJson` now delegates to `WorkspaceImportRules.IsWorkspaceSessionJson`.
- `MainWindow.LooksLikeTextExport` now delegates to `WorkspaceImportRules.LooksLikeTextExport`.
- `MainWindow.LooksLikeMarkdownExport` now delegates to `WorkspaceImportRules.LooksLikeMarkdownExport`.

## Validation Additions
- Expanded domain tests for:
  - Text export detection
  - Markdown export detection
  - Text export parsing
  - Markdown export parsing
  - Workspace-session JSON detection

## Validation Pipeline
1. `dotnet build .\ColumnPadStudio.sln -c Release` -> PASS (0 warnings, 0 errors)
2. `dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release` -> PASS (`Domain tests passed (25 checks).`)
3. `dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build` -> PASS (`Smoke tests passed (104 checks).`)

## Notes
- Existing reflection-based smoke assertions remain valid via thin wrappers in `MainWindow`.
- Behavior is preserved while reducing duplicate logic and making future reconstruction safer.
