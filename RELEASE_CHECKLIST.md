# Release Checklist

Use this checklist before creating a GitHub release.

## 0. Review scope and privacy
Before staging any files:

1. Review the complete diff and confirm every change belongs to the release.
2. Check that no credentials, personal data, local paths, private conversations, debug output, or temporary files are included.
3. Confirm `.gitignore` excludes local configuration, secrets, build output, IDE state, and release artifacts.
4. Review screenshots and release notes for private information and stale UI.
5. Run `git diff --check` to catch whitespace errors.

## 1. Clean build outputs
```powershell
dotnet clean .\ColumnPadStudio.sln
```

## 2. Build solution in Release
```powershell
dotnet build .\ColumnPadStudio.sln -c Release
```

Expected result:
- `0 Warning(s)`
- `0 Error(s)`

## 3. Run domain tests
```powershell
dotnet run --project .\tests\ColumnPadStudio.Domain.Tests\ColumnPadStudio.Domain.Tests.csproj -c Release --no-build
```

Expected result:
- `Domain tests passed`

## 4. Run smoke tests
```powershell
dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release --no-build
```

Expected result:
- `Smoke tests passed`

## 5. Publish the single-file Windows executable
```powershell
dotnet publish .\src\ColumnPadStudio\ColumnPadStudio.csproj -p:PublishProfile=FolderProfile
```

Expected output:
- `src\ColumnPadStudio\publish\ColumnPadStudio.exe`
- No `.pdb`, `.dll`, `.json`, or loose runtime files should remain beside the EXE for the public release asset.

The solution targets .NET 10 LTS. The self-contained publish resolves the latest available .NET 10 patch so the public EXE carries current runtime fixes.

## 6. Manual UI sanity checks
Run the fuller UI checklist in `docs\UI_QA_CHECKLIST.md` and check the app-building standard in `docs\APP_BUILDING_STANDARD.md`, then at minimum confirm:

1. Launch `ColumnPadStudio.exe`.
2. Open a saved layout or text document.
3. Confirm the selected theme still persists after closing and reopening the app.
4. Under Columns > Column Width, select Standard and confirm new columns open at 320 px without shrinking existing columns.
5. Select Custom, enter a value from 220-5000 px, add another column, and confirm the new column uses that default while individually resized columns keep their widths.
6. Add enough fixed-width columns to exceed the window and confirm the bottom scrollbar moves the main workspace left and right.
7. Select Fit Columns to Window and confirm columns share the available width equally; switch back to Standard or Custom and confirm saved pixel widths return.
8. Freeze a resized column, then use Reset Selected and Reset All; confirm the affected columns return to the current default width and become unlocked.
9. Turn Snap All Columns Together on and off; confirm only the global gap changes and no column width changes.
10. Change the column gap and confirm existing plus newly added snapped columns follow the setting without shrinking.
11. Paste enough text into one column to overflow it and confirm only that column receives its own vertical scrollbar.
12. Apply per-column text colours and confirm they survive theme changes and layout reload.
13. Change the font size and confirm Ruled, Soft Ruled, and Strong Ruled paper stay aligned with text rows in every theme.
14. Select text, move focus, and confirm active/inactive selection plus keyboard-focus borders remain readable in every theme.
15. Switch between single text mode and column mode.
16. Open the Workflow Builder and confirm preview/editing still works.
17. Save and reopen a `.columnpad.json` layout with pictures and column formatting after moving the original picture file.
18. Verify recovery prompt wording is sensible if recovery data exists.
19. Right-click a column header, editor, line gutter, workspace tab, and workflow node; hover nested menu items and confirm hover colour plus text contrast are readable in light, dark, and default themes.

## 7. Release metadata
1. Update `CHANGELOG.md`.
2. Confirm `README.md`, `docs\REPOSITORY_STRUCTURE.md`, and `docs\APP_BUILDING_STANDARD.md` still match the app.
3. Tag the version.
4. Attach `ColumnPadStudio.exe` to the GitHub release.
5. Refresh the repo screenshot if the UI changed meaningfully.

## 8. Download verification
1. Download the published `ColumnPadStudio.exe` from GitHub Releases to a clean folder.
2. Launch the downloaded file, not the local publish copy.
3. Confirm startup, typing, saving, and workflow launch all work.

## 9. Signing and trust
Unsigned Windows executables may trigger SmartScreen warnings. Before broad public distribution:

1. Obtain a real code-signing certificate.
2. Sign the published `ColumnPadStudio.exe`.
3. Verify the signature on a clean Windows machine.
4. Keep the unsigned build artifact out of the final public release if a signed artifact is available.

## 10. Installer decision
The current release output is a portable `.exe`. Before presenting the app as a fully polished public product, decide whether the release should also include an installer.

Use an installer when you need:
- Start menu shortcuts.
- Add/remove programs uninstall support.
- A fixed install location.
- Update or repair flow.

Keep the portable `.exe` when you want the simplest possible download.
