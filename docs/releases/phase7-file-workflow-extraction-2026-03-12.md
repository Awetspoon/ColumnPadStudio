# Phase 7 File Workflow Extraction Checkpoint (2026-03-12)

## Scope
- Goal: move open/save/export file workflow rules out of `MainWindow` and into a dedicated service without changing app behavior.
- Repo: ColumnPadStudio
- Branch: main

## Structural Changes
1. Added service:
   - `src/ColumnPadStudio/Services/FileWorkflowService.cs`
2. Added service contracts:
   - `FileDialogDefinition`
   - `OpenFileLoadKind`

## Refactor Summary
- `MainWindow.OpenLayout_Click(...)` now delegates file-type routing to:
  - `FileWorkflowService.SupportedOpenFileFilter`
  - `FileWorkflowService.ClassifyOpenFile(...)`
- `MainWindow.CreateSaveDialog(...)` now delegates dialog defaults to:
  - `FileWorkflowService.BuildSaveDialog(...)`
- `MainWindow.CreateWorkspaceSessionSaveDialog()` now delegates dialog defaults to:
  - `FileWorkflowService.BuildWorkspaceSessionSaveDialog(...)`
- Removed old filename helper logic from `MainWindow`:
  - `BuildSuggestedSaveFileName(...)`
  - `AppendCopySuffix(...)`

## Test Coverage Updates
- Expanded smoke tests to verify file workflow service behavior:
  - `.txt` / `.md` export-vs-document import detection
  - workspace-session `.json` classification
  - save dialog filename + extension behavior (including Save As copy suffix)
  - workspace-session save dialog preferred filename behavior

## Validation Pipeline
1. `dotnet build .\ColumnPadStudio.sln -c Release` -> PASS (0 warnings, 0 errors)
2. `dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release` -> PASS (`Domain tests passed (25 checks).`)
3. `dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build` -> PASS (`Smoke tests passed (137 checks).`)

## Notes
- This phase continues reconstruction by centralizing file workflow rules and reducing code-behind duplication.
- UI behavior is preserved while making save/open/export logic easier to test and maintain.
