# Phase 1 Domain Extraction Checkpoint (2026-03-11)

## Scope
- Goal: extract list/workspace rules from UI/ViewModel layers into a reusable domain library with behavior parity.
- Repo: ColumnPadStudio
- Branch: main

## Structural Changes
1. Added domain project:
   - `src/ColumnPadStudio.Domain/ColumnPadStudio.Domain.csproj`
2. Added domain test runner:
   - `tests/ColumnPadStudio.Domain.Tests/ColumnPadStudio.Domain.Tests.csproj`
   - `tests/ColumnPadStudio.Domain.Tests/Program.cs`
3. Added new domain namespaces:
   - `Lists`
   - `Workspaces`
4. Added project references:
   - `src/ColumnPadStudio/ColumnPadStudio.csproj` -> `src/ColumnPadStudio.Domain/ColumnPadStudio.Domain.csproj`
   - `tests/ColumnPadStudio.Domain.Tests/ColumnPadStudio.Domain.Tests.csproj` -> `src/ColumnPadStudio.Domain/ColumnPadStudio.Domain.csproj`
5. Added projects to solution:
   - `ColumnPadStudio.Domain`
   - `ColumnPadStudio.Domain.Tests`

## Extracted Logic
- List marker parsing/normalization and auto-continue rules:
  - `src/ColumnPadStudio.Domain/Lists/ListMarkerRules.cs`
- Marker types:
  - `src/ColumnPadStudio.Domain/Lists/ListMarkerKind.cs`
  - `src/ColumnPadStudio.Domain/Lists/LineMarkerInfo.cs`
- Checklist metrics calculation:
  - `src/ColumnPadStudio.Domain/Lists/ChecklistMetricsCalculator.cs`
- Workspace column bounds:
  - `src/ColumnPadStudio.Domain/Workspaces/WorkspaceConstraints.cs`

## App Wiring Updates
- `ColumnEditorControl` now delegates marker behavior to domain rules.
- `ColumnViewModel` now computes checklist metrics using `ChecklistMetricsCalculator`.
- `MainViewModel` now clamps column count through `WorkspaceConstraints`.

## Validation Pipeline
1. `dotnet build .\ColumnPadStudio.sln -c Release` -> PASS (0 warnings, 0 errors)
2. `dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release` -> PASS (`Domain tests passed (13 checks).`)
3. `dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build` -> PASS (`Smoke tests passed (104 checks).`)

## Notes
- Solution folder nesting was normalized after adding new projects so `src` and `tests` map cleanly.
- Smoke tests are passing with existing baseline count (104), indicating behavior parity for extracted rules.
