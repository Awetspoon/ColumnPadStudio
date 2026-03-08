# ColumnPad (WPF)

ColumnPad is a Windows text editor built around side-by-side writing columns and workspace tabs. It supports normal single-file editing for `.txt` and `.md`, plus JSON layout files for full multi-column workspaces.

## Requirements
- Windows
- .NET 8 SDK or Visual Studio 2022/2026 with the .NET Desktop Development workload

## Run
Open `ColumnPadStudio.sln` in Visual Studio and press `F5`, or run:

```powershell
dotnet build .\ColumnPadStudio.sln -c Release
dotnet run --project .\ColumnPadStudio\ColumnPadStudio.csproj -c Release
```

## Smoke Tests

```powershell
dotnet run --project .\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release
```

## Publish

```powershell
dotnet publish .\ColumnPadStudio\ColumnPadStudio.csproj -p:PublishProfile=FolderProfile
```

Publish output:
`.\ColumnPadStudio\publish\`

## Current Behavior
- `Open...` opens `.txt`, `.md`, and layout JSON files.
- `Save` writes back to the current file when the workspace came from a real file.
- `Save As...` saves a new file or layout.
- Workspace tabs are auto-recovered across crashes using per-workspace recovery files.
- Columns can be resized with splitters and swapped left/right with commands.
- Removing a filled column prompts for confirmation and identifies the selected column.
- Toolbar controls let you change font family, style, size, and theme.
- Themes include `Light Mode`, `Dark Mode`, and `Default Mode`.
- Bullet/checklist formatting is available from menus, shortcuts, and the editor context menu.
- `Esc` clears text selection in the editor and exits toolbar dropdowns back to the editor.

## Repo Notes
- Release and smoke validation are documented in `RELEASE_CHECKLIST.md`.
- Change history notes live in `CHANGELOG.md`.
- Build output and local validation folders are intentionally ignored by `.gitignore`.
- Licensed under the MIT License. See `LICENSE`.