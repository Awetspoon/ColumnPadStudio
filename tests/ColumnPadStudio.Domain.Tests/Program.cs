using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Domain.Workspaces;

var failures = new List<string>();
var checks = 0;

void Check(bool condition, string message)
{
    checks++;
    if (!condition)
        failures.Add(message);
}

var bullet = ListMarkerRules.ParseLineMarker("\u2022 task");
Check(bullet.Kind == ListMarkerKind.Bullet, "Unicode bullet should parse as bullet marker.");
Check(ListMarkerRules.ShouldAutoContinue(bullet), "Unicode bullet should auto-continue.");

var markdownBullet = ListMarkerRules.ParseLineMarker("- task");
Check(markdownBullet.Kind == ListMarkerKind.Bullet, "Markdown bullet should parse as bullet marker.");
Check(!ListMarkerRules.ShouldAutoContinue(markdownBullet), "Markdown bullet should not auto-continue.");

var nestedUnchecked = ListMarkerRules.ParseLineMarker("    - [ ] nested");
Check(nestedUnchecked.Kind == ListMarkerKind.ChecklistUnchecked, "Indented markdown checklist unchecked marker should parse.");

var nestedChecked = ListMarkerRules.ParseLineMarker("  \u2611 done");
Check(nestedChecked.Kind == ListMarkerKind.ChecklistChecked, "Indented unicode checklist checked marker should parse.");

var removed = ListMarkerRules.RemoveMarker("  \u2022 alpha", ListMarkerRules.ParseLineMarker("  \u2022 alpha"));
Check(removed == "  alpha", "RemoveMarker should keep indentation while removing marker prefix.");

var upserted = ListMarkerRules.UpsertMarker("    alpha", ListMarkerRules.ChecklistUncheckedPrefix);
Check(upserted == "    \u2610 alpha", "UpsertMarker should preserve leading indentation.");

Check(ListMarkerRules.HasOrderedListPrefix("1. step one"), "Ordered-list parser should recognize dot-numbered prefixes.");
Check(ListMarkerRules.HasOrderedListPrefix("  12) step two"), "Ordered-list parser should recognize parenthesis-numbered prefixes.");
Check(!ListMarkerRules.HasOrderedListPrefix("1.step one"), "Ordered-list parser should require whitespace after marker.");

var metrics = ChecklistMetricsCalculator.Compute("\u2610 one\n\u2611 two\n- [ ] three\n- [x] four");
Check(metrics.Total == 4, "ChecklistMetrics should count all supported checklist styles.");
Check(metrics.Done == 2, "ChecklistMetrics should count checked items across styles.");

Check(WorkspaceConstraints.ClampColumnCount(-1) == WorkspaceConstraints.MinColumns, "WorkspaceConstraints should clamp low column counts.");
Check(WorkspaceConstraints.ClampColumnCount(100000) == WorkspaceConstraints.MaxColumns, "WorkspaceConstraints should clamp high column counts.");
Check(WorkspaceConstraints.ClampColumnCount(3) == 3, "WorkspaceConstraints should keep valid counts unchanged.");
var textExport = "===== Alpha =====\n\none\n\n===== Beta =====\n\n.\n";
Check(WorkspaceImportRules.LooksLikeTextExport(textExport), "Text-export detection should recognize section headers.");
Check(!WorkspaceImportRules.LooksLikeTextExport("plain note\nline two"), "Text-export detection should reject plain text.");

var parsedTextExport = WorkspaceImportRules.ParseTextExportColumns(textExport);
Check(parsedTextExport.Count == 2, "Text-export parser should return one column per section header.");
Check(parsedTextExport[0].Title == "Alpha" && parsedTextExport[0].Text == "one", "Text-export parser should preserve first section content.");
Check(parsedTextExport[1].Title == "Beta" && parsedTextExport[1].Text == ".", "Text-export parser should preserve second section content.");

var markdownExport = "## Red\n\nleft\n\n## Blue\n\nright\n";
Check(WorkspaceImportRules.LooksLikeMarkdownExport(markdownExport), "Markdown-export detection should recognize heading-based exports.");
Check(!WorkspaceImportRules.LooksLikeMarkdownExport("intro paragraph\n## later heading"), "Markdown-export detection should reject inline heading text exports.");

var parsedMarkdownExport = WorkspaceImportRules.ParseMarkdownExportColumns(markdownExport);
Check(parsedMarkdownExport.Count == 2, "Markdown-export parser should return one column per heading.");
Check(parsedMarkdownExport[0].Title == "Red" && parsedMarkdownExport[0].Text == "left", "Markdown-export parser should preserve first heading section.");
Check(parsedMarkdownExport[1].Title == "Blue" && parsedMarkdownExport[1].Text == "right", "Markdown-export parser should preserve second heading section.");

var sessionJson = "{\"Version\":1,\"ActiveWorkspaceIndex\":0,\"Workspaces\":[{\"Name\":\"A\",\"LayoutJson\":\"{}\"}]}";
Check(WorkspaceImportRules.IsWorkspaceSessionJson(sessionJson), "Workspace-session detection should recognize Workspaces arrays.");
Check(!WorkspaceImportRules.IsWorkspaceSessionJson("{\"Version\":1,\"Columns\":[]}"), "Workspace-session detection should reject single-layout JSON.");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Domain tests failed: {failures.Count} of {checks} checks.");
    foreach (var failure in failures)
        Console.Error.WriteLine($" - {failure}");
    return 1;
}

Console.WriteLine($"Domain tests passed ({checks} checks).");
return 0;


