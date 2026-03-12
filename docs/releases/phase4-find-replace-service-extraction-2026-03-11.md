# Phase 4 Find/Replace Service Extraction Checkpoint (2026-03-11)

## Scope
- Goal: move find/replace engine logic out of `MainWindow` and into a dedicated service without changing UI behavior.
- Repo: ColumnPadStudio
- Branch: main

## Structural Changes
1. Added service:
   - `src/ColumnPadStudio/Services/TextSearchService.cs`
2. Added search contracts:
   - `SearchCursor`
   - `SearchResult`

## Refactor Summary
- `MainWindow` now delegates search/replace operations to `TextSearchService`:
  - Next-hit search across columns with wrap behavior
  - Character-index to line-number mapping
  - Replace-all counting and substitution logic
- Removed old inline search helpers from `MainWindow`:
  - `ComputeLineNumber`
  - `ReplaceAllWithCount`
- Preserved all existing command flows and status text behavior.

## Test Coverage Updates
- Expanded smoke tests to verify service behavior:
  - first-hit and next-hit cursor progression
  - cross-column hit and line-number reporting
  - wrap-around search behavior
  - no-hit behavior
  - replace-all count and output verification

## Validation Pipeline
1. `dotnet build .\ColumnPadStudio.sln -c Release` -> PASS (0 warnings, 0 errors)
2. `dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release` -> PASS (`Domain tests passed (25 checks).`)
3. `dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build` -> PASS (`Smoke tests passed (118 checks).`)

## Notes
- This phase continues reconstruction by separating text engine behavior from UI shell orchestration.
- Existing user-visible workflows (Find, Find Next, Replace All) remain behavior-compatible while reducing `MainWindow` complexity.
