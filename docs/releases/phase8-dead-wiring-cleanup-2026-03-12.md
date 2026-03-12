# Phase 8 Dead Wiring Cleanup Checkpoint (2026-03-12)

## Scope
- Goal: remove stale `MainWindow` helper wiring from earlier extraction phases and route remaining export dialog setup through centralized file workflow rules.
- Repo: ColumnPadStudio
- Branch: main

## Structural Changes
1. MainWindow cleanup:
   - Removed dead private wrappers that were no longer used by runtime flows:
     - `IsWorkspaceSessionJson(...)`
     - `LooksLikeTextExport(...)`
     - `LooksLikeMarkdownExport(...)`
2. Export dialog wiring cleanup:
   - Added shared helper: `CreateExportDialog(SaveFileKind kind)`
   - Rewired:
     - `ExportTxt_Click(...)`
     - `ExportMarkdown_Click(...)`
   - Both now derive dialog defaults from `FileWorkflowService.BuildSaveDialog(...)`.

## Refactor Summary
- Eliminated duplicate hardcoded export dialog setup in `MainWindow`.
- Reduced dead code-behind surface while keeping save/export behavior unchanged.
- Kept all UI orchestration in place (no redesign), but centralized dialog defaults in service-level logic.

## Test Coverage Updates
- Removed smoke-test reflection dependency on private `MainWindow` helper methods that were deleted.
- Added/kept service-level checks for:
  - workspace-session JSON detection (`WorkspaceSessionFileService`)
  - export filename defaults from `FileWorkflowService` (`ColumnPad_export.txt`, `ColumnPad_export.md`)

## Validation Pipeline
1. `dotnet build .\ColumnPadStudio.sln -c Release` -> PASS (0 warnings, 0 errors)
2. `dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release` -> PASS (`Domain tests passed (25 checks).`)
3. `dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build` -> PASS (`Smoke tests passed (135 checks).`)

## Notes
- This phase focuses on reconstruction hygiene: removing dead helper plumbing and reducing future drift between UI handlers and service rules.
- Public behavior remains stable while `MainWindow` responsibilities continue to shrink.
