# ColumnPad App-Building Standard

This document turns the general app-building order into a ColumnPad-specific maintenance standard. Use it when adding features, cleaning code, preparing a release, or checking whether a change has been bolted on instead of properly fitted into the app.

The goal is not to add every possible system. ColumnPad is a local Windows desktop app, so each item should be applied in a way that matches that purpose.

## Product Definition

| Step | ColumnPad standard |
| --- | --- |
| 1. Idea | ColumnPad is an offline Windows writing app for multi-column notes, plans, prompts, checklists, and workflows. |
| 2. Purpose | It helps users structure writing side by side without needing an online account, browser app, or database service. |
| 3. Scope | Keep the app focused on local writing, workspaces, file save/load, exports, proofing, images, and workflow planning. Do not add cloud accounts, API-key features, or unrelated online systems unless the product scope is deliberately changed later. |
| 4. Requirements | Must run locally on Windows, preserve user work, support clean save/load/export, remain understandable, and ship as a usable Windows build. |
| 5. User Flow | Open app, choose or create a workspace, write in columns, adjust settings, optionally use workflow tools, then save/export/close safely. |
| 6. Screen Map | Current screens are the main editor window, workflow builder, prompt/rename dialogs, file dialogs, and system error/recovery prompts. |
| 7. Feature List | Keep the feature list visible in `README.md` and update it when real user-facing features are added or removed. |

## Structure And UI

| Step | ColumnPad standard |
| --- | --- |
| 8. Structure | Keep app code in `src/ColumnPadStudio`, pure rules in `src/ColumnPadStudio.Domain`, tests in `tests`, and maintenance notes in `docs`. |
| 9. Skeleton | The app skeleton is the WPF shell, workspace tab strip, editor surface, resources, services, view models, and workflow builder. New features should attach to the right existing area. |
| 10. Layout | Main editor layout belongs in `MainWindow.xaml`; repeated column layout belongs in `ColumnEditorControl.xaml`; workflow layout belongs in `WorkflowBuilderWindow.xaml`. |
| 11. Components | Reuse shared controls, resource dictionaries, menus, dialogs, and view-model partials instead of creating one-off duplicate UI. |
| 12. Frontend | Keep visual styling consistent across default, light, and dark modes. Toolbar controls, menus, right-click dropdowns, tabs, and dialogs should feel like the same app. |
| 13. Backend / App Core | Keep saving, loading, theme, recovery, workflow, image, and text-rule work in focused services or domain helpers. |
| 14. Local Database / Local Storage | ColumnPad uses local files and local app data folders only: layouts, sessions, workflow files, preferences, images, recovery, and crash logs. |
| 15. Data | Data includes column text, titles, widths, line modes, checklist metadata, images, workspaces, workflows, settings, and file references. |
| 16. State | Runtime state belongs in view models such as `MainViewModel`, `ColumnViewModel`, `WorkspaceSession`, and `WorkflowBuilderViewModel`. |

## Behaviour And Safety

| Step | ColumnPad standard |
| --- | --- |
| 17. Logic | Put reusable rules in services or the domain project. Avoid hiding rules inside button click handlers when they need tests. |
| 18. Behaviour | Every button, menu item, shortcut, and right-click action should have one clear owner and one clear effect. |
| 19. Wiring | After edits, check commands, bindings, event handlers, file paths, resource dictionaries, context menus, and service calls. |
| 20. Settings | User settings include theme, fonts, proofing language, spell check, wrap, line numbers, lined paper, and saved app preferences. Do not duplicate global settings in per-column right-click menus. |
| 21. Validation | Validate names, file paths, JSON, image files, workflow links, column counts, and destructive actions before accepting them. |
| 22. Error Handling | File, image, import, export, recovery, and workflow errors should show clear messages instead of crashing. |
| 23. Security | Keep secrets out of the repo, ignore local config/private keys, write only to expected local storage, and do not introduce API-key features by accident. |
| 24. Permissions | ColumnPad is single-user and local, so no role system is needed. Permissions means respecting Windows file access and blocking unsafe or unavailable file operations gracefully. |
| 25. Logging | Unexpected app crashes are written to local crash logs. Keep normal app logging minimal unless a bug needs targeted diagnostics. |
| 26. Crash Handling | Keep dispatcher, app-domain, and unobserved-task crash handling active, and keep recovery snapshots separate from normal saved files. |

## Quality

| Step | ColumnPad standard |
| --- | --- |
| 27. Testing | Run domain tests for pure logic and smoke tests for app-facing wiring, persistence, resources, and workflow behaviour. |
| 28. Audit | Before larger releases, check for dead code, duplicated systems, broken bindings, messy UI, fake behaviour, outdated docs, and unused files. |
| 29. Performance | Avoid slow editor rendering, laggy selection, wasteful file work, and heavy startup logic. Keep repeated column controls lightweight. |
| 30. Accessibility | Keep text readable, contrast strong, focus states visible, keyboard shortcuts useful, and controls spaced clearly. |
| 31. Refactor | Refactor messy areas without changing the product purpose. Split large files only when the split makes ownership clearer. |
| 32. Clean | Remove duplicates, generated files, unused placeholders, misleading docs, broken wiring, and clutter before release. |
| 33. Polish | Polish means consistent spacing, clear labels, readable menus, professional dialogs, and no bolted-on panels. |
| 34. Documentation | Keep `README.md`, `docs/REPOSITORY_STRUCTURE.md`, `docs/UI_QA_CHECKLIST.md`, release notes, and this standard current. |

## Data Protection And Release

| Step | ColumnPad standard |
| --- | --- |
| 35. Backup | Auto-recovery protects unsaved work. Full user backup is the user's saved `.columnpad.json` or workspace-session file. |
| 36. Restore | Recovery restore and layout/session open paths must preserve workspaces, current file state, and layout data safely. |
| 37. Build | Build from the solution in Release mode before trusting the app. |
| 38. Versioning | Use release tags, changelog entries, release docs, and executable version metadata together. |
| 39. Release Notes | Release notes should list what changed, what was fixed, and any important known limits. |
| 40. Deploy / Install | Current deployment is a portable single Windows `.exe`; an installer is a later product decision. |
| 41. Monitor | After release, watch GitHub issues, user feedback, crash reports, and repeated UI pain points. |
| 42. Maintain | Keep dependencies, docs, tests, and release flow healthy. Fix regressions before adding bigger features. |
| 43. Upgrade | New upgrades should strengthen the current app rather than turning it into a different product. |
| 44. Migration | Keep legacy layout/session/workflow migration paths when changing saved file schemas. Add tests for old data shapes. |

## Required Checks Before Release

Run these alongside `RELEASE_CHECKLIST.md`:

1. Confirm the change fits ColumnPad's idea, purpose, and scope.
2. Confirm the feature belongs in the existing screen map and structure.
3. Confirm the UI is consistent in default, light, and dark modes.
4. Confirm save/load/export/recovery behaviour is not weakened.
5. Confirm there is no duplicate source of truth.
6. Confirm relevant tests and manual UI checks pass.
7. Confirm docs and release notes match the actual app.
