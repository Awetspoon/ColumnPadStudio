# ColumnPad

[![Release](https://img.shields.io/github/v/release/Awetspoon/ColumnPad?display_name=tag)](https://github.com/Awetspoon/ColumnPad/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6)](https://github.com/Awetspoon/ColumnPad)

ColumnPad is a Windows WPF text editor focused on side-by-side writing, fast workspace switching, and clean text workflows.

## Features
- Multi-column writing layout with drag-resize splitters.
- Workspace tabs for parallel notes and drafts.
- Open/save `.txt` and `.md` documents directly.
- Save/reopen full workspaces as `.columnpad.json` layouts.
- Auto-recovery snapshots for crash/session restore.
- Theme presets, line numbers, word wrap, and per-column font controls.
- List helpers: bullets, checklist conversion, and check toggling.
- Workflow Builder scaffold for saved workflow definitions.

## Requirements
- Windows 10/11
- .NET 8 SDK
- Optional: Visual Studio with .NET Desktop Development workload

## Setup
```powershell
git clone https://github.com/Awetspoon/ColumnPad.git
cd ColumnPad
```

## Run
```powershell
dotnet run --project .\ColumnPadStudio\ColumnPadStudio.csproj -c Release
```

## Build
```powershell
dotnet build .\ColumnPadStudio.sln -c Release
```

## Smoke Tests
```powershell
dotnet run --project .\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release
```

## Publish
```powershell
dotnet publish .\ColumnPadStudio\ColumnPadStudio.csproj -p:PublishProfile=FolderProfile
```

Publish output: `ColumnPadStudio\publish\`

## Project Structure
- `ColumnPadStudio/` - WPF desktop app (UI, viewmodels, services, controls)
- `ColumnPadStudio.SmokeTests/` - lightweight behavior smoke tests
- `docs/` - screenshots and repo documentation assets
- `tools/` - helper scripts (branding/assets)
- `RELEASE_CHECKLIST.md` - release process checklist
- `CHANGELOG.md` - change history

## Screenshots
![ColumnPad application screenshot](docs/columnpad-screenshot.png)

## Configuration
No `.env` variables are required for local run/build.

## License
MIT. See [LICENSE](LICENSE).