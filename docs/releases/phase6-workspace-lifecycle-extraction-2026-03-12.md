# Phase 6 Workspace Lifecycle Extraction Checkpoint (2026-03-12)

## Scope
- Goal: extract workspace tab/session lifecycle rules from `MainWindow` into dedicated files/services without changing UI behavior.
- Repo: ColumnPadStudio
- Branch: main

## Structural Changes
1. Added service:
   - `src/ColumnPadStudio/Services/WorkspaceLifecycleService.cs`
2. Moved workspace session model out of window code-behind:
   - `src/ColumnPadStudio/ViewModels/WorkspaceSession.cs`

## Refactor Summary
- `MainWindow.NextWorkspaceName()` now delegates to `WorkspaceLifecycleService.NextWorkspaceName(...)`.
- `MainWindow.CloseWorkspaceTab_Click(...)` now delegates close guards and next-index selection to:
  - `WorkspaceLifecycleService.CanCloseWorkspace(...)`
  - `WorkspaceLifecycleService.NextActiveWorkspaceIndexAfterClose(...)`
- Removed in-file `WorkspaceSession` class from `MainWindow` (dead code-behind responsibility reduced).
- `MainWindow` now references the dedicated `WorkspaceSession` viewmodel file.

## Test Coverage Updates
- Expanded smoke tests for workspace lifecycle service behavior:
  - case-insensitive next workspace naming
  - close guard when only one workspace remains
  - close allowance with multiple workspaces
  - next active index selection after close

## Validation Pipeline
1. `dotnet build .\ColumnPadStudio.sln -c Release` -> PASS (0 warnings, 0 errors)
2. `dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release` -> PASS (`Domain tests passed (25 checks).`)
3. `dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build` -> PASS (`Smoke tests passed (127 checks).`)

## Notes
- This phase continues reconstruction by shrinking `MainWindow` responsibilities and centralizing lifecycle rules in services/viewmodels.
- Existing workspace UX behavior is preserved while making future maintenance safer.
