# Phase 0 Baseline Freeze (2026-03-11)

## Scope
- Goal: freeze a reliable pre-refactor baseline before Phase 1 architecture extraction.
- Repo: ColumnPadStudio
- Branch: main

## Baseline Metadata
- Captured at: 2026-03-11 21:44:42 +00:00
- HEAD commit before checkpoint commit: ade93f364168e926c376e45f183e2f752a38ae2a

## Validation Pipeline
1. `dotnet clean .\\ColumnPadStudio.sln -c Release` -> PASS
2. `dotnet build .\\ColumnPadStudio.sln -c Release` -> PASS (0 warnings, 0 errors)
3. `dotnet run --project .\\tests\\ColumnPadStudio.SmokeTests\\ColumnPadStudio.SmokeTests.csproj -c Release` -> PASS
4. Smoke result: 104 checks passed

## Release Artifact Snapshot
- Path: `src/ColumnPadStudio/bin/Release/net8.0-windows/ColumnPadStudio.exe`
- Size (bytes): 184832
- Last write: 2026-03-11 21:43:11 +00:00
- SHA256: `F643F752A3A85C8BE352787ADFC905BE591E28EB556928255D81E063F7132C07`

## Working Tree Included in Baseline
- README.md
- RELEASE_CHECKLIST.md
- src/ColumnPadStudio/Controls/ColumnEditorControl.xaml
- src/ColumnPadStudio/Controls/ColumnEditorControl.xaml.cs
- src/ColumnPadStudio/MainWindow.xaml.cs
- src/ColumnPadStudio/ViewModels/ColumnViewModel.cs
- src/ColumnPadStudio/ViewModels/MainViewModel.cs
- tests/ColumnPadStudio.SmokeTests/Program.cs

## Notes
- This freeze includes reconstructed save/load/session logic hardening, line-paper alignment fixes, Enter behavior fixes, and regression tests.
- Next step after checkpoint: Phase 1 domain extraction with behavior parity gates.
