# ColumnPad

[![Release](https://img.shields.io/github/v/release/Awetspoon/ColumnPadStudio?display_name=tag)](https://github.com/Awetspoon/ColumnPadStudio/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-green.svg)](LICENSE)
[![Platform: Windows](https://img.shields.io/badge/platform-Windows-0078D6)](https://github.com/Awetspoon/ColumnPadStudio/releases/latest)

ColumnPad is a Windows writing app for drafting notes, plans, prompts, checklists, and structured text in clean side-by-side columns. It is built for people who want an offline writing surface that keeps ideas separated, readable, recoverable, and easy to export.

## Download ColumnPad

Get the latest Windows app from the [ColumnPad Releases page](https://github.com/Awetspoon/ColumnPadStudio/releases/latest).

Latest download:

- `ColumnPadStudio.exe`

ColumnPad is currently shipped as a portable single-file Windows app. Download the `.exe`, place it somewhere you want to keep it, and open it like a normal desktop app.

You do not need Visual Studio, Git, or the .NET SDK to use the released app.

Your writing stays local. On startup, ColumnPad only makes a brief best-effort request to the public GitHub releases page to see whether a newer stable version exists; no document content is sent.

Note: ColumnPad is not code-signed yet, so Windows SmartScreen may warn the first time you open it. Only download builds from this repository.

## Screenshot

![ColumnPad current desktop UI](docs/columnpad-screenshot.png)

## What ColumnPad Helps With

- Drafting notes across several columns without losing structure.
- Keeping separate workspace tabs for different projects or writing sessions.
- Planning prompts, release notes, checklists, comparisons, and app ideas.
- Opening and saving clean `.txt` and `.md` documents.
- Saving full ColumnPad workspaces so columns, titles, settings, images, and content reopen together.
- Building repeatable planning flows with the built-in Workflow Builder.

## Main Features

- Multi-column writing with clean resize behavior.
- Workspace tabs for separate writing sessions.
- Single-text mode and column mode switching.
- Clean `.txt` and `.md` open, save, and export.
- Native `.columnpad.json` workspace save/load.
- Multi-workspace session save/load.
- Auto-recovery and crash restore.
- A quiet GitHub release check that shows an update button only when a newer stable version is available.
- Line numbers, word wrap, spell check, proofing-language selection, and lined-paper mode.
- Default, light, and dark theme modes with preference saving.
- Bullet/checklist paste helpers and checklist gutter support.
- In-column pictures with drag-and-drop placement, proportional resizing, front/behind-text layering, and native layout persistence.
- Built-in Workflow Builder with starter templates, node colours, JSON workflow import/export, and readable workflow `.txt`/`.md` exports.

## Current Release

ColumnPad v2.3.0 focuses on dependable pictures, editing, layouts, and workflow data:

- Pictures now stay inside their own column and can be moved, resized proportionally, or placed in front of or behind text.
- Fixed blank white picture panels and unstable picture resizing.
- Improved column sizing so default columns fill the available space while saved custom widths remain intact.
- Kept checklist state aligned through typing, paste, delete, replace, and undo operations.
- Strengthened layout, recovery, and workflow validation so damaged or incomplete data is handled more safely.
- Improved Workflow Builder change tracking and unsaved-change protection.

Full notes: [docs/releases/v2.3.0.md](docs/releases/v2.3.0.md)

## User Requirements

- Windows 10 or Windows 11.
- No account required.
- No internet required after download.
- No .NET SDK required for the released EXE.

## Project Notes

This repository contains the source code, tests, release notes, screenshots, and maintenance docs for ColumnPad.

- `src/ColumnPadStudio/` - WPF app shell, UI, services, resources, and Workflow Builder.
- `src/ColumnPadStudio.Domain/` - reusable text, list, and workspace rules.
- `tests/` - smoke and domain checks.
- `docs/` - release notes, structure notes, screenshots, workflow maps, and QA checklists.
- `tools/` - helper scripts used during app maintenance.

More structure detail: [docs/REPOSITORY_STRUCTURE.md](docs/REPOSITORY_STRUCTURE.md)

Development and release decisions follow the ColumnPad app-building standard:
[docs/APP_BUILDING_STANDARD.md](docs/APP_BUILDING_STANDARD.md)

## Packaging Notes

- Current releases ship as a portable single `.exe`.
- The app is not code-signed yet.
- A full installer, Start menu shortcuts, uninstall support, and automatic updates can be added later.

## License

MIT. See [LICENSE](LICENSE).
