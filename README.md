# ColumnPad

[![Release](https://img.shields.io/github/v/release/Awetspoon/ColumnPadStudio?display_name=tag)](https://github.com/Awetspoon/ColumnPadStudio/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6)](https://github.com/Awetspoon/ColumnPadStudio)

ColumnPad is a Windows desktop writing app that lets you work in multiple side-by-side columns, keep reusable workspace layouts, and manage long-form notes without losing structure.

## What ColumnPad Does
- Splits your writing space into independent columns so you can draft, compare, and organize ideas in parallel.
- Saves complete workspace layouts (`.columnpad.json`) so projects reopen exactly how you left them.
- Keeps each column clean and readable with lined paper, line numbers, list/checklist markers, wrapping, and theme controls.
- Includes a diagram-based Workflow Builder for planning repeatable processes and exporting them as workflow JSON.

## Features
- Multi-column writing layout with drag-resize splitters.
- Workspace tabs for parallel notes and drafts.
- Open/save `.txt` and `.md` documents directly.
- Save/reopen full workspaces as `.columnpad.json` layouts.
- Auto-recovery snapshots for crash/session restore.
- Theme presets, line numbers, word wrap, lined-paper mode, and per-column font controls.
- List helpers: bullets, checklist conversion, and check toggling.
- Workflow Builder with diagram nodes/connections, built-in templates, and workflow JSON import/export.

## Requirements
- Windows 10/11
- .NET 8 SDK
- Optional: Visual Studio with .NET Desktop Development workload

## Setup
```powershell
git clone https://github.com/Awetspoon/ColumnPadStudio.git
cd ColumnPadStudio
```

## Install (Release EXE)
1. Download `ColumnPadStudio.exe` from the latest [GitHub Release](https://github.com/Awetspoon/ColumnPadStudio/releases).
2. Place the executable in a permanent folder (for example `C:\Apps\ColumnPad`).
3. Launch `ColumnPadStudio.exe`.
4. If Windows SmartScreen/Defender pauses launch, use `More info -> Run anyway` for trusted builds.

## Run
```powershell
dotnet run --project .\src\ColumnPadStudio\ColumnPadStudio.csproj -c Release
```

## Build
```powershell
dotnet build .\ColumnPadStudio.sln -c Release
```

## Tests
### Domain Tests
```powershell
dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release
```

### Smoke Tests
```powershell
dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release
```

## Publish
```powershell
dotnet publish .\src\ColumnPadStudio\ColumnPadStudio.csproj -p:PublishProfile=FolderProfile
```

Publish output: `src\ColumnPadStudio\publish\`
Release executable: `src\ColumnPadStudio\publish\ColumnPadStudio.exe`

## Project Structure
- `src/ColumnPadStudio/` - WPF desktop app (UI, viewmodels, services, controls)
- `src/ColumnPadStudio.Domain/` - shared domain rules and parsing logic
- `tests/ColumnPadStudio.Domain.Tests/` - domain rules test suite
- `tests/ColumnPadStudio.SmokeTests/` - runnable app/viewmodel smoke tests
- `docs/` - screenshots and repo documentation assets
- `docs/REPOSITORY_STRUCTURE.md` - repository layout and conventions
- `tools/` - helper scripts (branding/assets)
- `RELEASE_CHECKLIST.md` - release process checklist
- `CHANGELOG.md` - change history

## Screenshots
![ColumnPad application screenshot](docs/columnpad-screenshot.png)

## Configuration
No `.env` variables are required for local run/build.

## License
MIT. See [LICENSE](LICENSE).

