# Phase 5 Session Save-Routing Rule Extraction Checkpoint (2026-03-11)

## Scope
- Goal: move workspace-session save decision rules out of `MainWindow` into `WorkspaceSessionFileService`.
- Repo: ColumnPadStudio
- Branch: main

## Structural Changes
1. Extended service contracts:
   - `WorkspaceSessionSaveCandidate`
2. Added service rule methods:
   - `ShouldSaveWorkspaceSession(IReadOnlyList<WorkspaceSessionSaveCandidate>)`
   - `GetDirectWorkspaceSessionPath(IReadOnlyList<WorkspaceSessionSaveCandidate>)`

## Refactor Summary
- `MainWindow` now builds candidate snapshots from open workspaces and delegates save-routing decisions to service rules.
- Removed one dead wrapper method from `MainWindow`:
  - `IsExistingWorkspaceSessionFile`

## Test Coverage Updates
- Expanded smoke tests to verify session save-routing service behavior:
  - direct path reuse for single clean layout workspace
  - blocked direct path when Save As is required
  - forced session-save path when multiple workspaces are open

## Validation Pipeline
1. `dotnet build .\ColumnPadStudio.sln -c Release` -> PASS (0 warnings, 0 errors)
2. `dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release` -> PASS (`Domain tests passed (25 checks).`)
3. `dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build` -> PASS (`Smoke tests passed (122 checks).`)

## Notes
- Behavior remains aligned while reducing conditional orchestration logic inside `MainWindow`.
- This continues repository reconstruction with smaller UI classes and centralized rules/services.
