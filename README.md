# ColumnPad

[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6)

ColumnPad is a Windows writing app for notes, plans, prompts, checklists, and structured text. It gives each idea a clean, side-by-side column while keeping workspaces local, recoverable, and easy to export.

## Project Status

Active development. Current release: **v2.4.1**.

Release notes: [v2.4.1](docs/releases/v2.4.1.md).

ColumnPad is a portable Windows desktop app. It does not require an account, cloud storage, or an always-on connection.

## Download and Install

Download the latest `ColumnPadStudio.exe` from the [GitHub Releases page](../../releases/latest), save it in a permanent folder, and run it like a normal Windows application.

The released executable is self-contained: Visual Studio, Git, and the .NET SDK are not required to use it.

ColumnPad is not code-signed yet. Windows SmartScreen may warn the first time it is opened; download releases only from this repository.

## Screenshot

![ColumnPad desktop interface](docs/columnpad-screenshot.png)

The screenshot reflects the current three-column writing surface and contains no personal or sample document data.

## Main Features

- Side-by-side writing columns with resize controls and workspace tabs.
- Single-text and column modes for different drafting styles.
- Plain-text, Markdown, native layout, and workspace-session open, save, and export flows.
- Auto-recovery, crash logging, and save-before-exit safeguards.
- Line numbers, word wrap, spell checking, proofing-language selection, lined-paper mode, and paste helpers for bullets and checklists.
- Light, dark, and default themes with saved preferences.
- In-column pictures with drag-and-drop placement, proportional resizing, and text layering.
- Workflow Builder templates, node colours, workflow JSON import/export, and readable text or Markdown workflow exports.
- A quiet, best-effort check for a newer stable GitHub release at startup.

## Supported Platforms and Technology

- Windows 10 or Windows 11, x64.
- C#, .NET 8, and WPF.
- Self-contained, single-file Windows publishing for releases.

## Privacy and Configuration

No account, API key, server, or project-level configuration is required.

ColumnPad keeps preferences, recovery data, workflow-library data, imported image copies, and crash logs in app-managed local application storage. Native layouts retain references to imported images rather than embedding image data, so keep the image copies with a layout when moving it to another computer.

At startup, ColumnPad may make a brief request to the public GitHub releases endpoint to check for a newer stable version. The check never blocks writing or startup and does not send document content.

## Using ColumnPad

1. Create columns or a workspace tab for each topic you want to keep separate.
2. Write directly, use the View and Columns menus to adjust the editing surface, and use the editor menus for search, paste, and checklist actions.
3. Use File commands to open, save, export, or restore text, Markdown, native layouts, and workspace sessions.
4. Open the Workflow Builder from the Workflows menu to start from a template or import a workflow.

## Building from Source

Developer requirements:

- Windows 10 or Windows 11.
- .NET 8 SDK or a newer SDK that can build `net8.0-windows`.
- Optional: Visual Studio with the .NET Desktop Development workload.

Clone the repository and build:

```powershell
git clone <repository-url>
cd ColumnPadStudio
dotnet restore
dotnet build .\ColumnPadStudio.sln -c Release
```

Run the app from source:

```powershell
dotnet run --project .\src\ColumnPadStudio\ColumnPadStudio.csproj -c Release
```

Publish the portable single-file executable:

```powershell
dotnet publish .\src\ColumnPadStudio\ColumnPadStudio.csproj -p:PublishProfile=FolderProfile
```

The publish output is `src\ColumnPadStudio\publish\ColumnPadStudio.exe`.

## Testing

After a Release build, run both executable test suites:

```powershell
dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release --no-build
dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build
```

Before publishing, also follow [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md) and the visual checks in [docs/UI_QA_CHECKLIST.md](docs/UI_QA_CHECKLIST.md).

## Project Structure

```text
src/ColumnPadStudio/          WPF app shell, controls, services, resources, and workflows
src/ColumnPadStudio.Domain/   Pure text, list, and workspace rules
tests/                        Domain and app-level smoke checks
docs/                         Release notes, architecture notes, workflows, screenshots, and QA guidance
tools/                        Maintenance and asset-generation helpers
```

For a detailed guide, see [docs/REPOSITORY_STRUCTURE.md](docs/REPOSITORY_STRUCTURE.md). Larger changes should follow [docs/APP_BUILDING_STANDARD.md](docs/APP_BUILDING_STANDARD.md).

## Known Limitations

- The portable executable is currently unsigned and has no installer, automatic update, or uninstall flow.
- The app is Windows-only.
- Imported images remain local files referenced by native layouts; moving a layout alone does not package its images.

## Contributing

Keep changes focused, preserve local-data and secret exclusions, update relevant documentation, and run the Release build plus both test suites before opening a pull request. Add or refresh screenshots only when the visible interface has meaningfully changed.

## License

MIT. See [LICENSE](LICENSE).
