using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Domain.Text;
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

Check(DisplayTextRules.CleanSingleLineLabel("  Column\r\nOne\tName  ", "Column") == "Column One Name", "Display text rules should normalize pasted line breaks and tabs in labels.");
Check(DisplayTextRules.CleanSingleLineLabel(" \r\n\t ", "Fallback") == "Fallback", "Display text rules should fall back when labels are blank after cleanup.");

Check(WorkspaceConstraints.ClampColumnCount(-1) == WorkspaceConstraints.MinColumns, "WorkspaceConstraints should clamp low column counts.");
Check(WorkspaceConstraints.ClampColumnCount(100000) == WorkspaceConstraints.MaxColumns, "WorkspaceConstraints should clamp high column counts.");
Check(WorkspaceConstraints.ClampColumnCount(3) == 3, "WorkspaceConstraints should keep valid counts unchanged.");
Check(WorkspaceConstraints.MaxColumns == 9999, "WorkspaceConstraints should retain the original high column ceiling.");
Check(WorkspaceConstraints.ClampColumnWidth(120) == WorkspaceConstraints.MinimumColumnWidth, "WorkspaceConstraints should enforce the visual minimum column width.");
Check(WorkspaceConstraints.ClampColumnWidth(9000) == WorkspaceConstraints.MaximumColumnWidth, "WorkspaceConstraints should enforce the maximum column width.");
Check(WorkspaceConstraints.ClampColumnWidth(double.NaN) == WorkspaceConstraints.DefaultColumnWidth, "WorkspaceConstraints should replace invalid column widths with the default.");
Check(!WorkspaceColumnLayout.UsesFixedColumnStrip(1, false), "A single column should always use the available viewport width.");
Check(WorkspaceColumnLayout.UsesFixedColumnStrip(2, false), "Multiple columns should use fixed widths when Fit to window is off.");
Check(!WorkspaceColumnLayout.UsesFixedColumnStrip(2, true), "Fit to window should give multiple columns equal flexible widths.");
Check(WorkspaceColumnLayout.ResolveColumnWidth(null, 444) == 444, "An unsized column should resolve against the preferred default width.");
Check(WorkspaceColumnLayout.ResolveColumnWidth(0, 444) == 444, "A legacy zero width should resolve against the preferred default width.");
Check(WorkspaceColumnLayout.ResolveColumnWidth(null, 9000) == WorkspaceConstraints.MaximumColumnWidth, "An invalid preferred default width should be clamped safely.");
Check(WorkspaceColumnLayout.ResolveColumnWidth(555, 444) == 555, "An explicit column width should override the preferred default.");
Check(
    WorkspaceColumnLayout.CalculateHostWidth([5000], 1200, 4, true, false, 444) == 1200,
    "A single column should fill its viewport even when it has a stored custom width.");
Check(
    WorkspaceColumnLayout.CalculateHostWidth([null, null, null], 1200, 4, true, false, 320) == 1200,
    "A fixed-width column strip should still fill unused viewport space.");
Check(
    WorkspaceColumnLayout.CalculateHostWidth([null, null, null, null], 1200, 4, true, false, 320) == 1292,
    "Four default columns plus their gaps should overflow a 1200px viewport and enable horizontal scrolling.");
Check(
    WorkspaceColumnLayout.CalculateHostWidth([444, null], 700, 4, true, false, 320) == 768,
    "Host width should preserve explicit widths while defaulting unsized neighbours.");
Check(
    WorkspaceColumnLayout.CalculateHostWidth([444, null], 700, 4, false, false, 320) == 764,
    "Turning snapping off should remove the gap without changing fixed column widths.");
Check(
    WorkspaceColumnLayout.CalculateHostWidth([444, 555, 666], 1200, 4, true, true, 480) == 1200,
    "Fit mode should ignore stored and preferred widths while there is enough viewport space.");
Check(
    WorkspaceColumnLayout.CalculateHostWidth([444, 555, 666, 777, 888, 999], 1200, 4, true, true, 480) == 1340,
    "Fit mode should preserve every column's safe minimum width plus snapped gaps when space is tight.");
Check(
    WorkspaceColumnLayout.CalculateHostWidth([444, 555, 666, 777, 888, 999], 1200, 4, false, true, 480) == 1320,
    "Unsnapped Fit mode should preserve safe minimum widths without adding gaps.");
Check(
    WorkspaceColumnLayout.CalculateHostWidth([null, null], 500, 4, true, false, 444) == 892,
    "Fixed mode should apply the preferred custom width to every unsized column.");
var textExport = $"{WorkspaceImportRules.TextExportMarker}\n{WorkspaceImportRules.TextExportFormatLine}\n\n===== Alpha =====\n\none\n\n===== Beta =====\n\n.\n";
Check(WorkspaceImportRules.LooksLikeTextExport(textExport), "Text-export detection should recognize marked ColumnPad exports.");
Check(!WorkspaceImportRules.LooksLikeTextExport("plain note\nline two"), "Text-export detection should reject plain text.");
Check(!WorkspaceImportRules.LooksLikeTextExport("===== Alpha =====\n\nplain note"), "Text-export detection should reject unmarked divider-like text.");

var parsedTextExport = WorkspaceImportRules.ParseTextExportColumns(textExport);
Check(parsedTextExport.Count == 2, "Text-export parser should return one column per section header.");
Check(parsedTextExport[0].Title == "Alpha" && parsedTextExport[0].Text == "one", "Text-export parser should preserve first section content.");
Check(parsedTextExport[1].Title == "Beta" && parsedTextExport[1].Text == ".", "Text-export parser should preserve second section content.");

var jsonExport = """
{
  "FileType": "ColumnPadTextExport",
  "Version": 1,
  "Columns": [
    { "Title": "Red", "Text": "left" },
    { "Title": "Blue", "Text": "right" }
  ]
}
""";
Check(WorkspaceImportRules.IsJsonExport(jsonExport), "JSON-export detection should recognize marked ColumnPad text exports.");
Check(!WorkspaceImportRules.IsJsonExport("{\"FileType\":\"Other\",\"Columns\":[]}"), "JSON-export detection should reject unrelated JSON.");

var parsedJsonExport = WorkspaceImportRules.ParseJsonExportColumns(jsonExport);
Check(parsedJsonExport.Count == 2, "JSON-export parser should return one column per JSON entry.");
Check(parsedJsonExport[0].Title == "Red" && parsedJsonExport[0].Text == "left", "JSON-export parser should preserve first column content.");
Check(parsedJsonExport[1].Title == "Blue" && parsedJsonExport[1].Text == "right", "JSON-export parser should preserve second column content.");

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


