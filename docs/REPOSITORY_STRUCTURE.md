# Repository Structure

This repository follows a `src/tests/docs/tools` layout to keep application code, validation code, and operational assets separated.

## Top-level layout

```text
.
|-- src/
|   `-- ColumnPadStudio/
|-- tests/
|   `-- ColumnPadStudio.SmokeTests/
|-- docs/
|-- tools/
|-- ColumnPadStudio.sln
|-- README.md
|-- CHANGELOG.md
`-- RELEASE_CHECKLIST.md
```

## Conventions

- `src/` contains production projects only.
- `tests/` contains runnable smoke/integration tests and unit tests.
- `docs/` contains documentation and screenshots.
- `tools/` contains maintenance and branding scripts.
- Solution file stays at repo root for IDE and CI consistency.

## Build entry points

- Build solution: `dotnet build .\ColumnPadStudio.sln -c Release`
- Run app: `dotnet run --project .\src\ColumnPadStudio\ColumnPadStudio.csproj -c Release`
- Run smoke tests: `dotnet run --project .\tests\ColumnPadStudio.SmokeTests\ColumnPadStudio.SmokeTests.csproj -c Release`
