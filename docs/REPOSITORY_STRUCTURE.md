# Repository Structure

This repository uses a clean `src / tests / docs / tools` layout so application code, test code, docs, and maintenance scripts stay separated.

## Top-Level Layout

```text
.
|-- src/
|   |-- ColumnPadStudio/
|   `-- ColumnPadStudio.Domain/
|-- tests/
|   |-- ColumnPadStudio.SmokeTests/
|   `-- ColumnPadStudio.Domain.Tests/
|-- docs/
|   |-- APP_BUILDING_STANDARD.md
|   |-- UI_QA_CHECKLIST.md
|   |-- releases/
|   `-- workflows/
|-- tools/
|-- ColumnPadStudio.sln
|-- README.md
|-- CHANGELOG.md
`-- RELEASE_CHECKLIST.md
```

## What Each Main Folder Does

### `src/ColumnPadStudio/`
The WPF desktop app itself.

Important areas:
- `App.xaml` and `App.xaml.cs`
  Shared application resources and app startup shell.
- `MainWindow.xaml`
  Main app window and top-level UI structure.
- `MainWindow.xaml.cs` and `MainWindow.ColumnHost.cs`
  Main shell startup/state coordination and column-control construction, sizing, selection, and event wiring.
- `MainWindow.FileSession.cs`, `MainWindow.Lifecycle.cs`, `MainWindow.WorkspaceSessions.cs`, `MainWindow.SaveBeforeExit.cs`, and `MainWindow.DestructiveActions.cs`
  Open/save/export/print commands, recovery/autosave lifecycle, workspace-session JSON handling, exit-save prompts, and destructive-action confirmation wiring.
- `MainWindow.EditorSurface.cs`
  Column actions and selected-editor commands.
- `MainWindow.Search.cs`, `MainWindow.ViewModes.cs`, and `MainWindow.Shortcuts.cs`
  Search/replace, theme/view mode switching, workflow launch, and keyboard shortcut routing.
- `MainWindow.Workspaces.cs`
  Workspace tab lifecycle and rename wiring.
- `Controls/`
  Reusable UI controls and dialogs. Large controls are split by responsibility: the column editor keeps image, spelling, paste, gutter, menu, and interaction behavior in named partial files; Workflow Builder keeps canvas interactions and file actions separate from window lifecycle.
- `Resources/`
  Shared WPF resource dictionaries loaded by `App.xaml`. `AppResources.xaml` is only an index. `ThemeBrushes.xaml` contains app brushes, system colour overrides, and shared geometry. `ControlStyles.xaml` contains reusable WPF control templates. `MenuStyles.xaml` contains shared menu and context-menu styling.
- `ViewModels/`
  Writable app state for the shell, columns, workflows, and workspace tabs. Larger view models are split into named partial files. `ColumnViewModel.Checklists.cs` and `ColumnViewModel.Images.cs` own rich column behavior. `MainViewModel.TextDocuments.cs`, `MainViewModel.LayoutPersistence.cs`, and `MainViewModel.Persistence.cs` keep text documents, native layouts, and save coordination distinct.
- `Services/`
  Focused single-job helpers, including `AppStoragePaths` as the single place for app storage folders and `GitHubReleaseUpdateService` for the optional, non-blocking stable-release check. Workflow storage, serialization, and readable text/Markdown exports remain one service split into clearly named partial files so the format has one source of truth.
- `Models/`
  Small shared file and storage contracts that are used by both view models and services.
- `Workflows/`
  Workflow models and built-in template catalog.
- `Assets/`
  Icons, splash, wordmark, and app branding.

### `src/ColumnPadStudio.Domain/`
Pure rules and parsing helpers with no WPF UI code.

Current sub-areas:
- `Lists/`
  Checklist metrics and list marker parsing.
- `Text/`
  Shared display-text cleanup rules for labels, names, and titles.
- `Workspaces/`
  Workspace import detection and workspace constraints.

### `tests/ColumnPadStudio.SmokeTests/`
Broad tests for shell-facing behavior like layout save/load, recovery, export/import, and view-model state.

### `tests/ColumnPadStudio.Domain.Tests/`
Smaller focused tests for domain rules.

### `docs/`
App standards, repository notes, screenshots, workflow maps, and release notes.

Important docs:
- `APP_BUILDING_STANDARD.md`
  ColumnPad-specific version of the app-building order: idea, purpose, scope, structure, UI, storage, safety, testing, release, maintenance, upgrade, and migration.
- `UI_QA_CHECKLIST.md`
  Manual visual and interaction checks for themes, editing, columns, files, workflow builder, and release smoke testing.
- `releases/`
  Version-specific release notes.
- `workflows/`
  Importable workflow maps, including a high-level map of the ColumnPadStudio program structure.

### `tools/`
Helper scripts that support maintenance or asset generation.

## Architecture Notes

The app is intentionally lightweight:
- no heavy dependency injection container
- no fake abstraction layers where they do not help
- one `MainWindow` shell coordinating the WPF surface
- domain-only rules moved into `ColumnPadStudio.Domain`
- file, recovery, theme, and workflow persistence logic kept in focused services

## Build Entry Points
- Build solution:
  `dotnet build .\ColumnPadStudio.sln -c Release`
- Run app:
  `dotnet run --project .\src\ColumnPadStudio\ColumnPadStudio.csproj -c Release`
- Run domain tests:
  `dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release`
- Run smoke tests:
  `dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release`
- Publish single-file exe:
  `dotnet publish .\src\ColumnPadStudio\ColumnPadStudio.csproj -p:PublishProfile=FolderProfile`

## Maintenance Guidance
- Keep `bin/`, `obj/`, and `publish/` as generated output only.
- Keep app-wide WPF resources in `src/ColumnPadStudio/Resources/`, not directly in `App.xaml`.
- Put new shared colours and theme brushes in `ThemeBrushes.xaml`.
- Put reusable control templates in `ControlStyles.xaml`.
- Put shared menu and right-click dropdown styling in `MenuStyles.xaml`.
- Put new app-facing helpers under `src/ColumnPadStudio/Services/` only if they have one clear job.
- Put pure parsing/rule logic under `src/ColumnPadStudio.Domain/` when it does not need WPF types.
- Avoid growing `MainWindow` or `MainViewModel` into single-file catch-alls again; add new partial files or focused helpers instead.
- Check `docs/APP_BUILDING_STANDARD.md` before larger feature work or release prep so changes stay inside the app's purpose, scope, structure, storage, testing, and release standards.
