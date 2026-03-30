# Changelog

## v2.0.0 - Clean Rebuild

ColumnPad has been rebuilt into a cleaner .NET 8 WPF app structure with a locked spec-driven feature set.

Major updates:

- rebuilt the app into separate `App`, `Application`, `Domain`, and `Infrastructure` projects
- restored real multi-workspace plain-text editing with side-by-side columns
- implemented checklist, bullets, and line-number marker logic with real saved behavior
- rebuilt save, import, export, recovery, and older JSON migration paths
- cleaned the shell UI, settings flow, and workspace management behavior
- rebuilt the workflow builder so it feels like a real part of ColumnPad instead of a placeholder tool
- added workflow node colors, drag editing, rename flow, starter templates, and chart styles
- refreshed the repo documentation and preview screenshot
- produced a single-file Windows publish output for release packaging

Notes:

- repository contents are source-first; the one-file Windows executable belongs in GitHub Releases, not in the repo tree
- the rebuild keeps ColumnPad as a plain-text workspace tool rather than turning it into rich text or web-style editor chrome
