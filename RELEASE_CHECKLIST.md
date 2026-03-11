# Release Checklist

Use this checklist before creating a GitHub release.

## 1. Clean build artifacts

```powershell
dotnet clean .\ColumnPadStudio.sln
```

## 2. Build solution in Release

```powershell
dotnet build .\ColumnPadStudio.sln -c Release
```

Expected result: `0 Error(s)`.

## 3. Run smoke tests

```powershell
dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release
```

Expected result: `Smoke tests passed`.

## 4. Publish release executable

```powershell
dotnet publish .\src\ColumnPadStudio\ColumnPadStudio.csproj -p:PublishProfile=FolderProfile
```

Expected output location:
- `.\src\ColumnPadStudio\publish\ColumnPadStudio.exe`

## 5. Manual UI sanity checks

1. Launch `ColumnPadStudio.exe`.
2. Add/remove columns and verify no crash.
3. Switch theme to `Dark Mode` and confirm toolbar/menu/tab text is readable.
4. Use `+ Tab` and `- Tab` and verify workspace tab behavior.
5. Save and re-open a `.columnpad.json` layout.

## 6. Final release metadata

1. Update `CHANGELOG.md` for the version being released.
2. Tag the release version.
3. Attach `ColumnPadStudio.exe` from `.\src\ColumnPadStudio\publish\`.

