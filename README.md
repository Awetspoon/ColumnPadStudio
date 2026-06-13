# ColumnPad

[![Release](https://img.shields.io/github/v/release/Awetspoon/ColumnPadStudio?display_name=tag)](https://github.com/Awetspoon/ColumnPadStudio/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6)](https://github.com/Awetspoon/ColumnPadStudio)

ColumnPad is a Windows writing app for drafting notes, plans, prompts, checklists, and structured text in side-by-side columns. It is built for people who want a simple offline writing surface with columns, workspaces, line numbers, proofing, export, and a built-in workflow planner.

## Download For Windows

Download the latest portable Windows build from the [GitHub Releases page](https://github.com/Awetspoon/ColumnPadStudio/releases/latest).

Latest release asset:
- `ColumnPadStudio-v2.2.3-win-x64.exe`

For normal use, download the `.exe` and run it. You do not need Visual Studio or the .NET SDK to use the released app.

The app is currently shipped as a portable single-file executable, not an installer. Place it somewhere permanent, such as `C:\Apps\ColumnPad`, then run it from there.

Note: the EXE is not code-signed yet, so Windows SmartScreen may show a warning on first launch. Only run builds downloaded from this repository.

## Screenshot

![ColumnPad current desktop UI](docs/columnpad-screenshot.png)

## What ColumnPad Is For

- Writing in multiple independent columns without losing structure.
- Keeping separate workspace tabs for different notes or projects.
- Drafting app prompts, checklists, release notes, plans, comparisons, and structured writing.
- Opening and exporting clean `.txt` and `.md` documents.
- Saving full ColumnPad workspaces so the same columns, titles, settings, and content reopen later.
- Sketching repeatable workflows with the built-in workflow builder.

## Main Features

- Multi-column writing with invisible right-edge resize handles.
- Workspace tabs for separate writing sessions.
- Single-text mode and column mode switching.
- Clean `.txt` and `.md` open, save, and export.
- `.columnpad.json` workspace save/load.
- Multi-workspace session save/load.
- Auto-recovery and crash restore.
- Line numbers, word wrap, spell check, proofing-language selection, and lined-paper mode.
- Default, light, and dark theme modes with preference saving.
- Bullet/checklist paste helpers and checklist gutter support.
- Column picture attachments that save with native `.columnpad.json` layouts.
- Built-in workflow templates, workflow JSON import/export, human-readable workflow `.txt`/`.md` export, draggable workflow preview, and per-node colours.

## Current Release

ColumnPad v2.2.3 includes:

- Workflow JSON opened from File now routes to Workflow Builder.
- Cleaner workflow normalization code.
- Release build, tests, publish output, and startup probe rechecked.
- Existing `v2.2.2` UI polish, workspace scrolling, export, and repo hygiene work carried forward.

Full notes: [docs/releases/v2.2.3.md](docs/releases/v2.2.3.md)

## User Requirements

- Windows 10 or Windows 11.
- No account required.
- No internet required after download.
- No .NET SDK required for the released EXE.

## For Developers

Developer requirements:

- Windows 10 or Windows 11.
- .NET 8 SDK or newer SDK capable of building `net8.0-windows`.
- Optional: Visual Studio with the .NET Desktop Development workload.

Clone the repo:

```powershell
git clone https://github.com/Awetspoon/ColumnPadStudio.git
cd ColumnPadStudio
```

Run from source:

```powershell
dotnet run --project .\src\ColumnPadStudio\ColumnPadStudio.csproj -c Release
```

Build:

```powershell
dotnet build .\ColumnPadStudio.sln -c Release
```

Run tests:

```powershell
dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release
dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release
```

Publish a single EXE:

```powershell
dotnet publish .\src\ColumnPadStudio\ColumnPadStudio.csproj -p:PublishProfile=FolderProfile
```

Publish output:

- `src/ColumnPadStudio/publish/ColumnPadStudio.exe`

The publish profile is configured for a self-contained Windows x64 single-file build with trimming and bundle compression disabled.

## Project Structure

- `src/ColumnPadStudio/` - WPF app shell, UI, services, assets, resources, and workflow builder.
- `src/ColumnPadStudio.Domain/` - domain-only rules for lists, checklist metrics, and workspace import constraints.
- `tests/ColumnPadStudio.SmokeTests/` - app-level smoke checks for view-model and file/session flows.
- `tests/ColumnPadStudio.Domain.Tests/` - focused domain-rule checks.
- `docs/` - app-building standard, repository notes, release notes, screenshots, workflow maps, and QA checklists.
- `tools/` - helper scripts such as branding asset generation.

More structure detail: [docs/REPOSITORY_STRUCTURE.md](docs/REPOSITORY_STRUCTURE.md)

Development and release decisions should also follow the ColumnPad-specific app-building standard:
[docs/APP_BUILDING_STANDARD.md](docs/APP_BUILDING_STANDARD.md)

## Packaging Notes

- Current releases ship as a portable single `.exe`.
- The app is not code-signed yet.
- A full installer, Start menu shortcuts, uninstall support, and automatic updates can be added later.

## License

MIT. See [LICENSE](LICENSE).
