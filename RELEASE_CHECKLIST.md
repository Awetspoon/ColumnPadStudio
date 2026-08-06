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

Note: the publish profile pins the self-contained runtime pack to the cached .NET 8 patch version used for release builds. If this version is changed, restore the matching `win-x64` runtime packs before publishing.

## 6. Manual UI sanity checks
Run the fuller UI checklist in `docs\UI_QA_CHECKLIST.md` and check the app-building standard in `docs\APP_BUILDING_STANDARD.md`, then at minimum confirm:

1. Launch `ColumnPadStudio.exe`.
2. Open a saved layout or text document.
3. Confirm the selected theme still persists after closing and reopening the app.
4. Add/remove columns and verify scroll behavior still works.
5. Switch between single text mode and column mode.
6. Open the Workflow Builder and confirm preview/editing still works.
7. Save and reopen a `.columnpad.json` layout.
8. Verify recovery prompt wording is sensible if recovery data exists.
9. Right-click a column header, editor, line gutter, workspace tab, and workflow node; hover nested menu items and confirm hover colour plus text contrast are readable in light, dark, and default themes.

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
