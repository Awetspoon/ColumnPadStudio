# UI QA Checklist

Use this before calling a build visually ready. The goal is to catch the rough edges that make a desktop app feel patched together.

## Startup
- Launch from a clean build.
- Confirm the main window opens without recovery prompts unless recovery data exists.
- Confirm the first workspace has three columns and the selected column is visually clear.
- Confirm the status bar text is readable and does not clip at the default window size.

## Themes
- Switch to Default Mode, Light Mode, and Dark Mode.
- In each theme, check menus, toolbar controls, tabs, dialogs, column headers, editor text, gutters, and workflow builder panels.
- Open right-click menus on column headers, editor text, line gutters, workspace tabs, and workflow nodes.
- Hover every nested menu item and confirm the text remains readable, including submenu headers while the submenu is open.
- Check that menu hover states use the app theme colour rather than a mismatched Windows-default highlight.
- Close and reopen the app and confirm the last selected theme is restored.

## Editing
- Type plain text into multiple columns.
- Paste text from Notepad, a browser, Word/Docs, and markdown-like text.
- Check that line numbers stay aligned after paste, delete, undo, and resize.
- Switch gutter modes between numbers, bullets, and checklist.
- Toggle checklist rows from the gutter and from the context menu.
- Use `Esc` to clear selected text without disturbing other columns.

## Columns
- Add, remove, duplicate, and swap columns.
- Try deleting a column with text and confirm the warning is clear.
- Try clearing all columns and confirm the destructive warning appears.
- Drag splitters and confirm locked columns cannot be resized.
- Switch between Single Text Mode and Column Mode.

## Files
- Open a `.txt` file and confirm it opens as a single text document.
- Confirm first save of an opened `.txt` or `.md` asks for Save As.
- Open a native `.columnpad.json` layout and confirm direct Save is available.
- Save, close, and reopen a layout.
- Open multiple workspace tabs, save a session, close, and reopen it.
- Confirm auto-recovery appears after a simulated dirty shutdown.

## Workflow Builder
- Open the builder from the menu and shortcut.
- Create a workflow, use a template, save, import, export, and delete.
- Add, duplicate, remove, drag, and nudge nodes.
- Add and remove links.
- Select a link and confirm selection is visually noticeable.
- Right-click a workflow node and confirm colour choices are readable in all themes.
- Hover the workflow node colour submenu and confirm the open submenu header remains readable.

## Release Smoke
- Run domain tests.
- Run smoke tests.
- Publish the single-file executable.
- Launch the published executable from a clean folder.
- Confirm no local temp/build files are required for the published app to run.
