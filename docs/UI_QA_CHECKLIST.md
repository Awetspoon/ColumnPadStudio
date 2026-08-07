# UI QA Checklist

Use this before calling a build visually ready. The goal is to catch the rough edges that make a desktop app feel patched together.

## Startup
- Launch from a clean build.
- Confirm the main window opens without recovery prompts unless recovery data exists.
- Confirm the first workspace has three columns and the selected column is visually clear.
- Confirm the status bar text is readable and does not clip at the default window size.

## Themes
- Switch to Default Mode, Light Mode, and Dark Mode.
- In each theme, check menus, the writing-options strip, tabs, dialogs, column headers, editor text, gutters, and workflow builder panels.
- Open right-click menus on column headers, editor text, line gutters, workspace tabs, and workflow nodes.
- Hover every nested menu item and confirm the text remains readable, including submenu headers while the submenu is open.
- Check that menu hover states use the app theme colour rather than a mismatched Windows-default highlight.
- Select text, move focus to a menu, and confirm both active and inactive selections remain readable.
- Use `Tab` to move through buttons, drop-downs, tabs, checkboxes, lists, and text fields; confirm each focused control uses the same neutral theme border.
- Close and reopen the app and confirm the last selected theme is restored.

## Editing
- Type plain text into multiple columns.
- Paste text from Notepad, a browser, Word/Docs, and markdown-like text.
- Check that line numbers stay aligned after paste, delete, undo, and resize.
- Switch gutter modes between numbers, bullets, and checklist.
- Toggle checklist rows from the gutter and from the context menu.
- Apply preset and custom text colours to separate columns, then switch themes and reopen the saved layout.
- Try Ruled, Soft Ruled, and Strong Ruled paper at several font sizes; confirm the editor and number gutter stay on the same row spacing.
- Use `Esc` to clear selected text without disturbing other columns.

## Columns
- Add, remove, duplicate, and swap columns.
- Try deleting a column with text and confirm the warning is clear.
- Try clearing all columns and confirm the destructive warning appears.
- Confirm Standard opens new columns at 320 px without shrinking existing columns.
- Set a Custom default between 220 and 5000 px, add columns, and confirm new columns use it while individually resized columns keep their widths.
- Add enough Standard or Custom columns to exceed the window and confirm the bottom scrollbar moves the main workspace left and right.
- Select Fit Columns to Window and confirm columns share the available width equally; restore Standard or Custom and confirm saved pixel widths return.
- Drag column right edges and confirm locked columns cannot be resized.
- Reset a locked selected column and then reset all columns; confirm they return to the current default width and unlock.
- Turn Snap All Columns Together on and off; confirm only the global gap changes, widths stay unchanged, and no individual snap setting exists.
- Change the column gap and confirm existing, newly added, and loaded snapped columns follow it without shrinking.
- Paste enough text to overflow one column and confirm only that column gets its own vertical scrollbar.
- Switch between Single Text Mode and Column Mode.

## Pictures
- Drop the same picture into more than one column and confirm every copy renders inside its own column.
- Move and resize pictures at different column widths; confirm resizing stays proportional and does not jitter.
- Switch a picture between in-front-of-text and behind-text placement.
- Save, close, and reopen the layout; confirm picture source, size, position, and layer are preserved.

## Files
- Open a `.txt` file and confirm it opens as a single text document.
- Confirm first save of an opened `.txt` document or JSON text export asks for Save As.
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
