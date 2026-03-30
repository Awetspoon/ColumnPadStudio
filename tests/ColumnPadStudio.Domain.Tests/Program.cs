using System.Text.Json;
using ColumnPadStudio.Application.Services;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;
using ColumnPadStudio.Domain.Models;

var failures = new List<string>();

Assert(TextMetrics.GetLineCount("a\r\nb\r\nc") == 3, "Line count test failed.");
Assert(MarkerFormatter.BuildGutter(MarkerMode.Bullets, "a\nb").Contains('\u2022'), "Bullet gutter test failed.");
Assert(MarkerFormatter.BuildGutter(MarkerMode.Numbers, "a\nb", showLineNumbers: false) == Environment.NewLine, "Hidden number gutter should preserve line spacing without showing numbers.");

var inlineChecklistMetrics = ChecklistMetrics.GetMetrics(
    MarkerMode.Bullets,
    "\u2611 done\n- [ ] todo\n\u2610 later");
Assert(inlineChecklistMetrics.Total == 3 && inlineChecklistMetrics.Done == 1, "Inline checklist metrics test failed.");

var firstLineOnly = TextSelectionLogic.GetSelectedLineRange("a\nb\nc", 0, 2);
Assert(firstLineOnly == (0, 0), "Selection range should exclude the next line when the selection ends at its start.");

var twoLineSelection = TextSelectionLogic.GetSelectedLineRange("a\nb\nc", 0, 3);
Assert(twoLineSelection == (0, 1), "Selection range should include both touched lines.");

var searchHit = WorkspaceSearchLogic.FindNext(new[] { "alpha\nbeta", "gamma", "match here" }, "match", 1, 0);
Assert(searchHit is { ColumnIndex: 2, Start: 0, LineNumber: 1 }, "FindNext should continue through later columns before wrapping.");

var wrappedHit = WorkspaceSearchLogic.FindNext(new[] { "before match", "none" }, "match", 0, 12);
Assert(wrappedHit is { ColumnIndex: 0, Start: 7, LineNumber: 1 }, "FindNext should wrap only within the starting column.");

var replaceResult = WorkspaceSearchLogic.ReplaceAll("aaaa", "aa", "b");
Assert(replaceResult.Count == 2 && replaceResult.Text == "bb", "ReplaceAll should use non-overlapping replacements.");

Assert(PasteTransformLogic.NormalizeLineBreaks("a\r\r\nb\u2028c\rd") == "a\nb\nc\nd", "Paste normalization should fix mixed line separators.");
Assert(PasteTransformLogic.CollapseMalformedDoubleSpacing("one\n\ntwo\n\nthree") == "one\ntwo\nthree", "Paste normalization should collapse clearly malformed doubled spacing.");
Assert(PasteTransformLogic.PrepareForEditor("task\n- already bullet", PastePreset.Bullets) == $"- task{Environment.NewLine}- already bullet", "Bullet preset should add markdown bullets without duplicating existing bullet lines.");
Assert(PasteTransformLogic.PrepareForEditor("\u2611 done\nnote", PastePreset.Checklist) == $"- [x] done{Environment.NewLine}- [ ] note", "Checklist preset should preserve checked lines and normalize unchecked lines.");
Assert(PasteTransformLogic.ApplyPresetToSelectedLines("one\ntwo\nthree", 0, 7, PastePreset.Bullets) == "- one\n- two\nthree", "Selection bullet transform should only affect the selected line range.");
Assert(PasteTransformLogic.ApplyPresetToSelectedLines("one\ntwo\nthree", 0, 7, PastePreset.Checklist) == "- [ ] one\n- [ ] two\nthree", "Selection checklist transform should respect line-based selection rules.");
Assert(ColumnWidthLogic.ClampStoredWidth(null) is null, "Stored width clamp should preserve automatic width.");
Assert(ColumnWidthLogic.GetDesiredWorkspaceWidth(new double?[] { null }, 900, 8) == 900, "A single flexible column should fill the available writing width.");
Assert(ColumnWidthLogic.GetDesiredWorkspaceWidth(new double?[] { null, null, null, null }, 1000, 8) > 1000, "Multiple flexible columns should trigger horizontal scrolling before the editors get squeezed too narrow.");
var resizedPair = ColumnWidthLogic.ApplySplitterDelta(240, 240, -500);
Assert(resizedPair is { Left: 120, Right: 360 }, "Splitter math should clamp adjacent widths to the locked minimum.");
var defaultWorkspace = WorkspaceDocumentLogic.CreateDefaultWorkspace();
Assert(defaultWorkspace.Columns.Count == 3, "Default workspace should start with three columns.");
Assert(defaultWorkspace.Columns[0].Title == "Column 1" && defaultWorkspace.Columns[1].Title == "Column 2" && defaultWorkspace.Columns[2].Title == "Column 3", "Default workspace column titles should follow the locked naming.");
Assert(defaultWorkspace.ActiveColumnIndex == 0 && defaultWorkspace.Defaults.ShowLineNumbers && defaultWorkspace.Defaults.WordWrap && defaultWorkspace.Defaults.SpellCheckEnabled && !defaultWorkspace.Defaults.LinedPaper, "Default workspace settings should match the locked startup defaults.");
Assert(defaultWorkspace.Defaults.FontSize == 13 && defaultWorkspace.Defaults.LanguageTag == "en-US" && defaultWorkspace.Defaults.ThemePreset == ThemePreset.Default && defaultWorkspace.Defaults.LineStyle == EditorLineStyle.StandardRuled, "Default workspace typography, language, theme, and line style should match the locked startup defaults.");
var normalizedWorkspace = WorkspaceDocumentLogic.Normalize(new ColumnPadStudio.Domain.Models.WorkspaceDocument
{
    LastMultiColumnCount = 0,
    Defaults = new ColumnPadStudio.Domain.Models.EditorDefaults { FontSize = 400, LanguageTag = "", FontFamily = "" },
    Columns = Enumerable.Range(1, WorkspaceRules.MaxColumns + 4).Select(index => new ColumnPadStudio.Domain.Models.ColumnDocument { Title = $"Column {index}" }).ToList()
});
Assert(normalizedWorkspace.Columns.Count == WorkspaceRules.MaxColumns, "Workspace normalization should clamp the column count to the locked maximum.");
Assert(normalizedWorkspace.Defaults.FontSize == WorkspaceRules.MaxFontSize && normalizedWorkspace.Defaults.LanguageTag == "en-US" && normalizedWorkspace.LastMultiColumnCount == 3, "Workspace normalization should restore locked defaults and clamps when incoming values are invalid.");
Assert(WorkflowRules.ClampNodeWidth(20) == WorkflowRules.MinNodeWidth && WorkflowRules.ClampNodeWidth(720) == WorkflowRules.MaxNodeWidth, "Workflow node width clamp should honor the locked range.");
var oversizedNode = new ColumnPadStudio.Domain.Models.WorkflowNode { Width = 999 };
Assert(oversizedNode.Width == WorkflowRules.MaxNodeWidth, "Workflow nodes should clamp oversized widths on assignment.");
var coloredNode = new ColumnPadStudio.Domain.Models.WorkflowNode { Color = WorkflowNodeColor.Purple, Title = "Color check" };
var coloredNodeRoundTrip = JsonSerializer.Deserialize<ColumnPadStudio.Domain.Models.WorkflowNode>(JsonSerializer.Serialize(coloredNode));
Assert(coloredNodeRoundTrip is { Color: WorkflowNodeColor.Purple }, "Workflow node colors should round-trip through JSON serialization.");
var chartNodes = new List<ColumnPadStudio.Domain.Models.WorkflowNode>
{
    new() { Kind = WorkflowNodeKind.Start, Title = "Start" },
    new() { Kind = WorkflowNodeKind.Step, Title = "Step" },
    new() { Kind = WorkflowNodeKind.End, Title = "End" }
};
WorkflowChartLayoutLogic.ApplyAutoLayout(chartNodes, WorkflowChartStyle.VerticalTimeline);
Assert(chartNodes[0].Y < chartNodes[1].Y && chartNodes[1].Y < chartNodes[2].Y, "Vertical timeline layout should stack nodes from top to bottom.");
WorkflowChartLayoutLogic.ApplyAutoLayout(chartNodes, WorkflowChartStyle.HorizontalTimeline);
Assert(chartNodes[0].X < chartNodes[1].X && chartNodes[1].X < chartNodes[2].X, "Horizontal timeline layout should place nodes from left to right.");
var radialNodes = new List<ColumnPadStudio.Domain.Models.WorkflowNode>
{
    new() { Kind = WorkflowNodeKind.Start, Title = "Core" },
    new() { Kind = WorkflowNodeKind.Step, Title = "A" },
    new() { Kind = WorkflowNodeKind.Step, Title = "B" },
    new() { Kind = WorkflowNodeKind.Step, Title = "C" }
};
WorkflowChartLayoutLogic.ApplyAutoLayout(radialNodes, WorkflowChartStyle.RadialMap);
Assert(radialNodes[0].X != radialNodes[1].X || radialNodes[0].Y != radialNodes[1].Y, "Radial map layout should spread nodes around the center node.");
var kanbanGuides = WorkflowChartLayoutLogic.BuildGuides(chartNodes, WorkflowChartStyle.Kanban);
Assert(kanbanGuides.Count == 5, "Kanban workflow guides should expose the full lane set.");
Assert(WorkspaceFileClassifier.Classify("notes.txt", "just a raw text file") == WorkspaceFileKind.RawText, "Plain .txt files should stay raw text when no export headers exist.");
Assert(WorkspaceFileClassifier.Classify("export.txt", "===== Alpha =====\n\nOne") == WorkspaceFileKind.TextExport, "Text files with an exact export header should classify as text export.");
Assert(WorkspaceFileClassifier.Classify("notes.md", "plain markdown") == WorkspaceFileKind.RawMarkdown, "Markdown files should stay raw markdown when they do not begin with a real export heading.");
Assert(WorkspaceFileClassifier.Classify("export.md", "## Alpha\n\nOne") == WorkspaceFileKind.MarkdownExport, "Markdown files that start with a real heading should classify as markdown export.");
Assert(WorkspaceFileClassifier.Classify("layout.json", "{\"name\":\"One\",\"columns\":[]}") == WorkspaceFileKind.Layout, "JSON without a workspaces array should classify as a layout.");
Assert(WorkspaceFileClassifier.Classify("session.columnpad.json", "{\"workspaces\":[]}") == WorkspaceFileKind.Session, ".columnpad.json files should still use the JSON content signature.");

var importedTextExport = WorkspaceImportService.ImportWorkspace(
    "columns.txt",
    "===== Alpha =====\n\nLine one\n===== =====\n\nLine two",
    WorkspaceFileKind.TextExport);
Assert(importedTextExport.Columns.Count == 2, "Text export import should create one column per export header.");
Assert(importedTextExport.Columns[0].Title == "Alpha" && importedTextExport.Columns[0].Text == "Line one", "Text export import should skip the first blank line after a header once.");
Assert(importedTextExport.Columns[1].Title == "Column 2" && importedTextExport.Columns[1].Text == "Line two", "Blank export titles should fall back to numbered column names.");

var importedMarkdownExport = WorkspaceImportService.ImportWorkspace(
    "columns.md",
    "## Alpha\n\nLine one\n## Beta\n\nLine two",
    WorkspaceFileKind.MarkdownExport);
Assert(importedMarkdownExport.Columns.Count == 2, "Markdown export import should create one column per heading.");
Assert(importedMarkdownExport.Columns[0].Title == "Alpha" && importedMarkdownExport.Columns[1].Title == "Beta", "Markdown export import should use the heading text as the column title.");

var legacyChecklistLayout = """
{
  "version": 12,
  "name": "Legacy",
  "defaults": {
    "themePreset": "High Contrast"
  },
  "columns": [
    {
      "title": "Column 1",
      "text": "- [x] done\n- [ ] next"
    }
  ]
}
""";
var migratedChecklistWorkspace = WorkspaceImportService.ImportWorkspace("legacy.columnpad.layout.json", legacyChecklistLayout, WorkspaceFileKind.Layout);
Assert(migratedChecklistWorkspace.Version == LegacyLayoutMigrationLogic.CurrentLayoutVersion, "Older layouts should upgrade to the current layout version.");
Assert(migratedChecklistWorkspace.Defaults.ThemePreset == ThemePreset.Dark, "Legacy theme names should normalize to the locked theme presets.");
Assert(migratedChecklistWorkspace.Columns[0].MarkerMode == MarkerMode.Checklist, "Inline legacy checklist content should migrate to checklist mode.");
Assert(migratedChecklistWorkspace.Columns[0].Text == "done\nnext", "Legacy checklist prefixes should be removed from stored text.");
Assert(migratedChecklistWorkspace.Columns[0].CheckedLines.SetEquals([0]), "Legacy checklist migration should preserve checked line indexes.");

var legacyBulletLayout = """
{
  "version": 11,
  "name": "Legacy Bullets",
  "columns": [
    {
      "title": "Column 1",
      "text": "- alpha\n- beta"
    }
  ]
}
""";
var migratedBulletWorkspace = WorkspaceImportService.ImportWorkspace("legacy-bullets.columnpad.layout.json", legacyBulletLayout, WorkspaceFileKind.Layout);
Assert(migratedBulletWorkspace.Columns[0].MarkerMode == MarkerMode.Bullets, "Inline legacy bullet content should migrate to bullet mode.");
Assert(migratedBulletWorkspace.Columns[0].Text == "alpha\nbeta", "Legacy bullet prefixes should be removed from stored text.");

var legacyEscapedNewlineLayout = """
{
  "version": 9,
  "name": "Legacy Escapes",
  "columns": [
    {
      "title": "Column 1",
      "text": "alpha\\n beta\\r\\n gamma\\rdelta"
    }
  ]
}
""";
var migratedEscapedWorkspace = WorkspaceImportService.ImportWorkspace("legacy-escapes.columnpad.layout.json", legacyEscapedNewlineLayout, WorkspaceFileKind.Layout);
Assert(migratedEscapedWorkspace.Columns[0].Text == "alpha\n beta\n gamma\ndelta", "Escaped newline sequences should become real line breaks for older single-line layouts.");

var legacyArrowChainLayout = """
{
  "version": 8,
  "name": "Legacy Flow",
  "columns": [
    {
      "title": "Column 1",
      "width": 360,
      "fontSize": 13,
      "text": "Capture all of the context carefully before moving forward -> Write the first clean step with enough detail to stand on its own -> Review the second step and make sure the wording is plain and direct -> Finish by saving the final approved version for the workspace"
    }
  ]
}
""";
var migratedArrowWorkspace = WorkspaceImportService.ImportWorkspace("legacy-flow.columnpad.layout.json", legacyArrowChainLayout, WorkspaceFileKind.Layout);
Assert(migratedArrowWorkspace.Columns[0].Text == "Capture all of the context carefully before moving forward\nWrite the first clean step with enough detail to stand on its own\nReview the second step and make sure the wording is plain and direct\nFinish by saving the final approved version for the workspace", "Older arrow-chain single-line content should split into one step per line.");

var currentVersionLayout = """
{
  "version": 14,
  "name": "Current",
  "defaults": {
    "lineStyle": 1
  },
  "columns": [
    {
      "title": "Column 1",
      "text": "keep\\nthis\\nas typed"
    }
  ]
}
""";
var currentVersionWorkspace = WorkspaceImportService.ImportWorkspace("current.columnpad.layout.json", currentVersionLayout, WorkspaceFileKind.Layout);
Assert(currentVersionWorkspace.Columns[0].Text == "keep\\nthis\\nas typed", "Current layouts should not run legacy escaped-newline migration.");
Assert(currentVersionWorkspace.Defaults.LineStyle == EditorLineStyle.LegacyRuled, "Current layouts should preserve explicit line style values.");

var legacyWrappedLayout = """
{
  "Version": 13,
  "ShowLineNumbers": false,
  "WordWrap": true,
  "EditorFontFamily": "Consolas",
  "EditorFontStyle": "Regular",
  "EditorFontSize": 13,
  "ThemePreset": "Default Mode",
  "SpellCheckEnabled": true,
  "EditorLanguageTag": "en-US",
  "LinedPaperEnabled": true,
  "ActiveIndex": 1,
  "Columns": [
    {
      "Title": "Alpha",
      "Text": "one",
      "WidthPx": 221,
      "IsWidthLocked": false,
      "PastePreset": "None",
      "LineMarkerMode": "Bullets",
      "CheckedChecklistLineIndexes": [],
      "FontFamily": "Consolas",
      "FontSize": 13,
      "FontStyle": "Normal",
      "FontWeight": "Normal",
      "UseDefaultFont": true
    },
    {
      "Title": "Beta",
      "Text": "done\nnext",
      "WidthPx": 322,
      "IsWidthLocked": true,
      "PastePreset": "Checklist",
      "LineMarkerMode": "Checklist",
      "CheckedChecklistLineIndexes": [0],
      "FontFamily": "Consolas",
      "FontSize": 13,
      "FontStyle": "Normal",
      "FontWeight": "Bold",
      "UseDefaultFont": false
    }
  ]
}
""";
var importedLegacyWrappedLayout = WorkspaceImportService.ImportWorkspace("legacy-layout.json", legacyWrappedLayout, WorkspaceFileKind.Layout);
Assert(!importedLegacyWrappedLayout.Defaults.ShowLineNumbers && importedLegacyWrappedLayout.Defaults.LinedPaper, "Older layout fields should map into current workspace defaults.");
Assert(importedLegacyWrappedLayout.Defaults.LineStyle == EditorLineStyle.LegacyRuled, "Older lined-paper layouts should default to the legacy ruled style.");
Assert(importedLegacyWrappedLayout.ActiveColumnIndex == 1, "Older layouts should preserve the active column index.");
Assert(importedLegacyWrappedLayout.Columns[0].Width == 221 && importedLegacyWrappedLayout.Columns[0].MarkerMode == MarkerMode.Bullets, "Older layout column width and marker mode fields should import cleanly.");
Assert(importedLegacyWrappedLayout.Columns[1].IsWidthLocked && importedLegacyWrappedLayout.Columns[1].PastePreset == PastePreset.Checklist, "Older layout column flags should import cleanly.");
Assert(importedLegacyWrappedLayout.Columns[1].CheckedLines.SetEquals([0]) && !importedLegacyWrappedLayout.Columns[1].UseDefaultFont && importedLegacyWrappedLayout.Columns[1].FontWeightName == "Bold", "Older layout checklist and custom font fields should import cleanly.");

var legacyWrappedLayoutTwo = """
{
  "Version": 13,
  "ShowLineNumbers": true,
  "WordWrap": false,
  "EditorFontFamily": "Consolas",
  "EditorFontStyle": "Regular",
  "EditorFontSize": 13,
  "ThemePreset": "Light Mode",
  "SpellCheckEnabled": false,
  "EditorLanguageTag": "en-US",
  "LinedPaperEnabled": false,
  "ActiveIndex": 0,
  "Columns": [
    {
      "Title": "Gamma",
      "Text": "plain text",
      "WidthPx": 300,
      "IsWidthLocked": false,
      "PastePreset": "None",
      "LineMarkerMode": "Numbers",
      "CheckedChecklistLineIndexes": [],
      "FontFamily": "Consolas",
      "FontSize": 13,
      "FontStyle": "Normal",
      "FontWeight": "Normal",
      "UseDefaultFont": true
    }
  ]
}
""";
var legacySessionJson = $$"""
{
  "Version": 1,
  "ActiveWorkspaceIndex": 1,
  "Workspaces": [
    {
      "Name": "Legacy One",
      "LayoutJson": {{JsonSerializer.Serialize(legacyWrappedLayout)}},
      "LastMultiColumnCount": 4
    },
    {
      "Name": "Legacy Two",
      "LayoutJson": {{JsonSerializer.Serialize(legacyWrappedLayoutTwo)}},
      "LastMultiColumnCount": 5
    }
  ]
}
""";
var importedLegacySession = WorkspaceImportService.ImportSession(legacySessionJson);
Assert(importedLegacySession.Workspaces.Count == 2, "Older session wrappers should import every embedded workspace layout.");
Assert(importedLegacySession.ActiveWorkspaceIndex == 1, "Older session wrappers should preserve the active workspace index.");
Assert(importedLegacySession.Workspaces[0].Name == "Legacy One" && importedLegacySession.Workspaces[0].LastMultiColumnCount == 4, "Older session wrappers should preserve workspace names and multi-column counts.");
Assert(importedLegacySession.Workspaces[0].Columns.Count == 2 && importedLegacySession.Workspaces[0].Columns[1].Title == "Beta", "Older session wrappers should preserve embedded column data.");
Assert(importedLegacySession.Workspaces[1].Name == "Legacy Two" && importedLegacySession.Workspaces[1].Defaults.ThemePreset == ThemePreset.Light, "Older session wrappers should preserve later workspaces and normalize legacy theme names.");
Assert(!importedLegacySession.Workspaces[1].Defaults.WordWrap && !importedLegacySession.Workspaces[1].Defaults.SpellCheckEnabled, "Older session wrappers should preserve embedded workspace defaults.");

var blankRecoverySession = new WorkspaceSessionDocument
{
    Workspaces = new List<WorkspaceDocument>
    {
        WorkspaceDocumentLogic.CreateDefaultWorkspace("Workspace 1")
    }
};
Assert(!RecoverySessionLogic.ShouldPersistSnapshot(blankRecoverySession), "Untouched startup workspaces should not be kept as recovery backups.");

var editedRecoveryWorkspace = WorkspaceDocumentLogic.CreateDefaultWorkspace("Recovered File");
editedRecoveryWorkspace.Columns[0].Text = "edited text";
var editedRecoverySession = new WorkspaceSessionDocument
{
    Workspaces = new List<WorkspaceDocument> { editedRecoveryWorkspace }
};
Assert(RecoverySessionLogic.ShouldPersistSnapshot(editedRecoverySession), "Edited workspaces should stay eligible for recovery backups.");

var recoveryPrompt = RecoverySessionLogic.BuildRestorePrompt(new RecoverySnapshot
{
    Session = editedRecoverySession,
    SavedAtLocal = new DateTime(2026, 3, 19, 1, 38, 0),
    WorkspaceStates = new List<RecoveryWorkspaceState>
    {
        new()
        {
            WorkspaceId = editedRecoveryWorkspace.Id,
            FilePath = @"C:\Users\Marcus\Desktop\ColumnPadStudio\layout.columnpad.json",
            FileKind = WorkspaceFileKind.Layout,
            SaveAsRequired = false
        }
    }
});
Assert(recoveryPrompt.Contains("ColumnPad found an unsaved backup for layout.columnpad.json.", StringComparison.Ordinal) &&
       recoveryPrompt.Contains("Last unsaved backup: 19 March 2026 at 01:38.", StringComparison.Ordinal) &&
       recoveryPrompt.Contains("Restore it now?", StringComparison.Ordinal),
       "Recovery prompt wording should use clear unsaved-backup wording and keep the original file name visible.");

if (failures.Count > 0)
{
    Console.Error.WriteLine(string.Join(Environment.NewLine, failures));
    return 1;
}

Console.WriteLine("ColumnPadStudio.Domain.Tests passed.");
return 0;

void Assert(bool condition, string message)
{
    if (!condition)
    {
        failures.Add(message);
    }
}
