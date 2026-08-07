# ColumnPad

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6)

ColumnPad is a Windows writing app for notes, plans, prompts, checklists, and structured text. It keeps ideas separated in clean side-by-side columns while workspaces remain local, recoverable, and easy to export.

## Project Status

Active development. Current release: **v2.5.0**.

Release notes: [v2.5.0](docs/releases/v2.5.0.md).

ColumnPad is a portable Windows desktop app. It does not require an account, cloud storage, or an always-on connection.

## Download and Install

Download `ColumnPadStudio.exe` from the [latest GitHub release](../../releases/latest), save it in a permanent folder, and run it like a normal Windows application.

The released executable is self-contained. Visual Studio, Git, and the .NET SDK are not required to use it.

ColumnPad is not code-signed yet, so Windows SmartScreen may warn the first time it is opened. Download releases only from this repository.

## Screenshot

![ColumnPad desktop interface](docs/columnpad-screenshot.png)

The screenshot shows the current three-column writing surface with clean sample content and no personal data.

## Main Features

- Standard 320 px, saved Custom 220-5000 px, and explicit Fit Columns to Window sizing.
- Stable fixed-width columns with automatic main-window left/right scrolling as more columns are added.
- One global Snap All Columns Together setting with an adjustable gap that does not resize columns.
- Per-column resizing, freeze/unfreeze, reset-to-default actions, and independent vertical scrolling for long text.
- Workspace-wide adjustable line-number gutter width from 32-160 px.
- Workspace tabs plus Single Text Mode and Column Mode.
- Plain `.txt`, concise readable `.json`, full-fidelity `.columnpad.json`, and multi-workspace session files.
- Auto-recovery, crash logging, and save-before-exit safeguards.
- Line numbers, word wrap, spell checking, proofing-language selection, and paste helpers for bullets and checklists.
- Ruled, Soft Ruled, and Strong Ruled paper styles aligned with the editor and gutter.
- Light, dark, and default themes with saved preferences.
- Theme-aware per-column text-colour presets and custom colours.
- In-column pictures with drag-and-drop placement, proportional resizing, text layering, and portable native-layout storage.
- Workflow Builder templates, node colours, connection editing, workflow JSON import/export, and readable text exports.
- A quiet, best-effort check for newer stable GitHub releases at startup.

## Supported Platforms and Technology

- Windows 10 or Windows 11, x64.
- C#, .NET 10 LTS, and WPF.
- Self-contained, single-file Windows publishing for releases.

## Privacy and Local Storage

No account, API key, server, or project-level configuration is required.

ColumnPad keeps preferences, recovery data, saved workflows, imported image copies, and crash logs in app-managed local application storage. Native layouts embed bounded image data so a saved workspace remains portable if the original imported file is moved or deleted.

At startup, ColumnPad may make a brief request to the public GitHub releases endpoint to check for a newer stable version. The check never sends document content and never blocks writing or startup.

## Using ColumnPad

1. Create columns or workspace tabs for the topics you want to keep separate.
2. Use **Columns > Column Width** to choose Standard, Custom, or Fit Columns to Window sizing.
3. Use each column's **Actions** menu for rename, move, resize, reset, formatting, pictures, and other column-specific actions. Right-clicking a column header is reserved for renaming.
4. Use the **View** menu to adjust gutters, paper style, themes, wrapping, proofing, and Single Text or Column Mode.
5. Use **File** commands to open, save, export, restore, or print text, JSON, native layouts, and workspace sessions.
6. Open **Workflows** to create, import, edit, and save reusable workflow diagrams.

## File Formats

- `.txt` stores normal text documents and readable multi-column text exports.
- `.json` stores concise readable column text exports.
- `.columnpad.json` stores full layouts, including columns, formatting, settings, and embedded pictures.
- `.workflow.json` stores editable Workflow Builder diagrams.

Markdown document and export support has been retired. Existing Markdown files remain readable in ordinary text editors but are not presented as a ColumnPad file type.

## Building from Source

Developer requirements:

- Windows 10 or Windows 11.
- .NET 10 SDK.
- Optional: Visual Studio with the .NET Desktop Development workload.

Clone and build:

```powershell
git clone <repository-url>
cd ColumnPadStudio
dotnet restore
dotnet build .\ColumnPadStudio.sln -c Release
```

Run from source:

```powershell
dotnet run --project .\src\ColumnPadStudio\ColumnPadStudio.csproj -c Release
```

Publish the portable single-file executable:

```powershell
dotnet publish .\src\ColumnPadStudio\ColumnPadStudio.csproj -p:PublishProfile=FolderProfile
```

The release output is `src\ColumnPadStudio\publish\ColumnPadStudio.exe` with no loose runtime files beside it.

## Testing

After a Release build, run both executable test suites:

```powershell
dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release --no-build
dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build
```

Before publishing, also follow [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) and [docs/UI_QA_CHECKLIST.md](docs/UI_QA_CHECKLIST.md).

## Project Structure

```text
src/ColumnPadStudio/          WPF app shell, controls, services, resources, and workflows
src/ColumnPadStudio.Domain/   Reusable text, list, and workspace rules
tests/                        Domain and app-level smoke checks
docs/                         Release notes, architecture notes, workflows, screenshots, and QA guidance
tools/                        Maintenance helpers
```

See [docs/REPOSITORY_STRUCTURE.md](docs/REPOSITORY_STRUCTURE.md) for more detail. Larger changes should follow [docs/APP_BUILDING_STANDARD.md](docs/APP_BUILDING_STANDARD.md).

## Known Limitations

- The portable executable is unsigned and may trigger a SmartScreen warning.
- ColumnPad is Windows-only and currently has no installer or uninstall entry.
- Update checks notify the user but do not install updates automatically.

## Contributing

Keep changes focused, preserve local-data and secret exclusions, update relevant documentation, and run the Release build plus both test suites before opening a pull request. Refresh screenshots only when the visible interface has meaningfully changed.

## License

MIT. See [LICENSE](LICENSE).
