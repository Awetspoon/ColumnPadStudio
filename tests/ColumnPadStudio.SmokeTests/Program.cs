using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.ViewModels;
using ColumnPadStudio.Services;
using ColumnPadStudio.Controls;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using ColumnPadStudio.Models;
using ColumnPadStudio.Workflows;
using System.Windows.Controls;
using System.Windows.Threading;
using ColumnPadStudio;
using ColumnPadStudio.SmokeTests;

var tests = new SmokeTestContext();

void Check(bool condition, string message) => tests.Check(condition, message);

var vm = new MainViewModel();
var defaultWidthPreferences = new AppPreferences();

Check(
    !defaultWidthPreferences.FitColumnsToWindow
        && defaultWidthPreferences.DefaultColumnWidthPx == AppPreferences.StandardColumnWidthPx
        && defaultWidthPreferences.DefaultColumnWidthPx == 320,
    "Column sizing should default to a fixed 320px strip, with Fit Columns to Window available only when selected.");

Check(ColumnTextColorService.Normalize("blue") == ColumnTextColorService.Blue, "Text-colour presets should normalize case-insensitively.");
Check(
    ColumnTextColorService.TryNormalizeCustomHex("245a9a", out var normalizedTextColor)
        && normalizedTextColor == "#245A9A",
    "Custom text colours should normalize to a stable #RRGGBB value.");
Check(
    ColumnTextColorService.Normalize("not-a-colour") == ColumnTextColorService.ThemeDefault,
    "Invalid text colours should fall back to the theme colour.");
Check(
    ColumnTextColorService.CreateCustomBrush("#245A9A")?.Color == Color.FromRgb(0x24, 0x5A, 0x9A),
    "Custom text colours should create the expected editor brush.");

Check(vm.ThemePreset == "Default Mode", "Default theme should be 'Default Mode'.");
Check(vm.IsDefaultThemeSelected && !vm.IsLightThemeSelected && !vm.IsDarkThemeSelected, "Default theme menu state should match the default preset.");
Check(
    vm.EditorFontSummary == $"{vm.EditorFontFamily} {vm.EditorFontStyleName} {vm.EditorFontSize:0}",
    "Editor font summary should reflect the active global editor font settings.");
Check(
    vm.EditorLanguages.Select(language => language.Tag).SequenceEqual(["en-US", "en-GB", "fr-FR", "de-DE", "es-ES", "it-IT", "pt-BR", "pt-PT", "nl-NL", "sv-SE", "da-DK", "nb-NO"]),
    "Proofing language list should keep the current supported app range.");
Check(vm.Columns.Count == 3, "Default layout should start with 3 columns.");
Check(vm.Columns.All(column => column.WidthPx is null), "New layouts should keep the normal display width implicit instead of saving redundant width values.");
Check(
    vm.GutterWidthPx == MainViewModel.MinimumGutterWidthPx
        && vm.GutterWidthPx == MainViewModel.DefaultGutterWidthPx
        && vm.Columns.All(column => column.LineNumberColumnWidth.IsAbsolute && Math.Abs(column.LineNumberColumnWidth.Value - MainViewModel.DefaultGutterWidthPx) < 0.001),
    "New workspaces should start with the smallest shared gutter width.");
Check(vm.StatusText.Contains("Selected:"), "Status text should identify the selected column.");
Check(!vm.IsDirty, "New layout should start clean.");
var paperSettingsVm = new MainViewModel();
Check(paperSettingsVm.SelectedPaperStyle == PaperStyle.Ruled, "New workspaces should default to ruled paper.");
Check(paperSettingsVm.IsPaperOffSelected, "Paper should start switched off.");
Check(
    Enum.GetValues<PaperStyle>().SequenceEqual([PaperStyle.Ruled, PaperStyle.SoftRuled, PaperStyle.StrongRuled]),
    "Paper choices should be limited to aligned ruled-paper variants.");
paperSettingsVm.UsePaperStyle(PaperStyle.SoftRuled);
Check(paperSettingsVm.LinedPaperEnabled && paperSettingsVm.IsSoftRuledPaperSelected, "Choosing a ruled-paper variant should enable that style.");
paperSettingsVm.SelectedPaperStyle = (PaperStyle)999;
Check(paperSettingsVm.SelectedPaperStyle == PaperStyle.Ruled, "Unknown paper styles should fall back to ruled paper.");
paperSettingsVm.LinedPaperEnabled = false;
Check(paperSettingsVm.IsPaperOffSelected && !paperSettingsVm.IsRuledPaperSelected, "Switching paper off should clear the active style check.");
vm.ActiveColumnId = vm.Columns[1].Id;
Check(!vm.IsDirty, "Selecting another column should not mark otherwise unchanged content dirty.");
vm.ActiveColumnId = vm.Columns[0].Id;
vm.Columns[0].Title = "  Column\r\nOne\tName  ";
Check(vm.Columns[0].Title == "Column One Name", "Column titles should be normalized to a clean single-line label.");
var cleanedWorkspace = new WorkspaceSession("  Workspace\r\nAlpha\tDraft  ", new MainViewModel());
Check(cleanedWorkspace.Name == "Workspace Alpha Draft", "Workspace names should be normalized to a clean single-line label.");
Check(!cleanedWorkspace.HasSessionChanges, "A newly created workspace should begin with clean session metadata.");
cleanedWorkspace.Name = "Workspace Renamed";
Check(cleanedWorkspace.HasSessionChanges && cleanedWorkspace.IsDirty, "Renaming a workspace should participate in the workspace dirty state.");
cleanedWorkspace.MarkSessionClean();
Check(!cleanedWorkspace.HasSessionChanges, "Saving a workspace session should establish a clean metadata state.");
cleanedWorkspace.LastMultiColumnCount = 5;
Check(cleanedWorkspace.HasSessionChanges, "Changing the remembered multi-column mode should mark session metadata dirty.");
var cleanedWorkflow = new WorkflowDefinition { Name = "  Workflow\r\nAlpha  ", Category = "  Research\tPlan  " };
Check(cleanedWorkflow.Name == "Workflow Alpha" && cleanedWorkflow.Category == "Research Plan", "Workflow names and categories should be normalized to clean single-line labels.");
var cleanedWorkflowNode = new WorkflowDiagramNode { Kind = WorkflowNodeKind.Decision, Title = "  Choose\r\nPath  " };
Check(cleanedWorkflowNode.Title == "Choose Path", "Workflow node titles should be normalized to clean single-line labels.");

ThemeAndControlSmokeTests.Run(tests);

vm.SetColumnCount(0);
Check(vm.Columns.Count == 1, "SetColumnCount should clamp to a minimum of 1 column.");
Check(!vm.RemoveActiveColumn(), "RemoveActiveColumn should refuse to delete the last remaining column.");

vm.SetColumnCount(3);
var removedId = vm.Columns[1].Id;
vm.ActiveColumnId = removedId;
Check(vm.RemoveActiveColumn(), "RemoveActiveColumn should delete the active column when more than one column exists.");
Check(vm.Columns.Count == 2, "RemoveActiveColumn should reduce the column count by one.");
Check(!vm.Columns.Any(c => c.Id == removedId), "RemoveActiveColumn should remove the selected column rather than the rightmost column.");

vm.SetColumnCount(4);
var movableId = vm.Columns[1].Id;
vm.ActiveColumnId = movableId;
Check(vm.CanMoveActiveColumnLeft, "Middle selected column should be able to swap left.");
Check(vm.CanMoveActiveColumnRight, "Middle selected column should be able to swap right.");
var rightNeighborTitle = vm.Columns[2].Title;
Check(vm.MoveActiveColumnRight(), "MoveActiveColumnRight should swap the selected column one slot to the right.");
Check(vm.Columns[2].Id == movableId, "MoveActiveColumnRight should place the selected column one slot to the right.");
Check(vm.StatusText == $"Swapped {vm.Columns[2].Title} with {rightNeighborTitle}.", "MoveActiveColumnRight should report which columns were swapped.");
var leftNeighborTitle = vm.Columns[1].Title;
Check(vm.MoveActiveColumnLeft(), "MoveActiveColumnLeft should swap the selected column one slot to the left.");
Check(vm.Columns[1].Id == movableId, "MoveActiveColumnLeft should place the selected column one slot to the left.");
Check(vm.StatusText == $"Swapped {vm.Columns[1].Title} with {leftNeighborTitle}.", "MoveActiveColumnLeft should report which columns were swapped.");
vm.ActiveColumnId = vm.Columns[0].Id;
Check(!vm.CanMoveActiveColumnLeft, "First selected column should not advertise a left swap.");
Check(!vm.MoveActiveColumnLeft(), "MoveActiveColumnLeft should refuse to swap the first column further left.");
Check(vm.StatusText.Contains("first column"), "MoveActiveColumnLeft should explain when the selected column is already first.");
vm.Columns[0].LineMarkerMode = LineMarkerMode.Checklist;
vm.Columns[0].SetCheckedChecklistLineIndexes([0]);
vm.Columns[0].EditorTextColor = ColumnTextColorService.Blue;
vm.Columns[0].Images.Add(new ColumnImageViewModel(
    "C:\\images\\diagram.png",
    "diagram.png",
    420,
    800,
    600,
    18,
    26,
    ColumnImageLayer.BehindText));
vm.DuplicateActive();
Check(vm.Columns[^1].LineMarkerMode == LineMarkerMode.Checklist, "DuplicateActive should preserve the duplicated column gutter marker mode.");
Check(vm.Columns[^1].IsChecklistLineChecked(0), "DuplicateActive should preserve duplicated column checklist metadata.");
Check(vm.Columns[^1].Images.Count == 1, "DuplicateActive should preserve duplicated column image attachments.");
Check(Math.Abs(vm.Columns[^1].Images[0].Width - 420) < 0.001, "DuplicateActive should preserve duplicated image display width.");
Check(Math.Abs(vm.Columns[^1].Images[0].Left - 18) < 0.001 && Math.Abs(vm.Columns[^1].Images[0].Top - 26) < 0.001, "DuplicateActive should preserve image position.");
Check(vm.Columns[^1].Images[0].Layer == ColumnImageLayer.BehindText, "DuplicateActive should preserve image text layer.");
Check(vm.Columns[^1].EditorTextColor == ColumnTextColorService.Blue, "DuplicateActive should preserve the column text colour.");

vm.ThemePreset = "High Contrast";
Check(vm.ThemePreset == "Dark Mode", "Legacy theme 'High Contrast' should normalize to 'Dark Mode'.");
Check(vm.IsDarkThemeSelected && !vm.IsDefaultThemeSelected && !vm.IsLightThemeSelected, "Theme menu state should follow normalized dark mode.");
Check(vm.LockActiveWidthActionLabel == "_Freeze Selected Column Width", "Unlocked selected column should advertise the freeze-width action.");
vm.ToggleLockActiveWidth();
Check(vm.LockActiveWidthActionLabel == "_Allow Selected Column Width to Resize", "Locked selected column should advertise the allow-resize action.");
vm.ToggleLockActiveWidth();
vm.Columns[0].PastePreset = PasteListPreset.Checklist;
vm.Columns[0].LineMarkerMode = LineMarkerMode.Checklist;
vm.Columns[0].Text = "task one\ntask two";
vm.Columns[0].SetCheckedChecklistLineIndexes([1]);
vm.Columns[0].WidthPx = 444;
vm.Columns[0].EditorFontFamily = "Consolas";
vm.Columns[0].EditorFontSize = 17;
vm.Columns[0].EditorFontStyle = FontStyles.Italic;
vm.Columns[0].EditorFontWeight = FontWeights.Bold;
vm.Columns[0].UseDefaultFont = false;
vm.Columns[0].EditorTextColor = "#2A6F97";
vm.SpellCheckEnabled = false;
vm.EditorLanguageTag = "fr-FR";
Check(vm.ProofingLanguageDisplayName.Contains("French", StringComparison.OrdinalIgnoreCase), "Proofing display name should describe the selected language.");
Check(vm.StatusText.Contains("Proofing language:", StringComparison.Ordinal), "Changing proofing language should explain what changed.");
vm.GutterWidthPx = 36;
vm.UsePaperStyle(PaperStyle.StrongRuled);
vm.ActiveColumnId = vm.Columns[1].Id;
Check(vm.IsDirty, "Changing the layout should mark the workspace dirty.");

var json = vm.ToLayoutJson();
var savedLayoutRoot = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse saved layout JSON.");
Check(savedLayoutRoot["FileType"]?.GetValue<string>() == "ColumnPadLayout", "Saved layouts should include an explicit ColumnPad file type.");
Check(savedLayoutRoot["Version"]?.GetValue<int>() == 19, "Saved layouts should use the shared-gutter layout schema version.");
Check(savedLayoutRoot["PaperStyle"]?.GetValue<string>() == "StrongRuled", "Saved layouts should store the selected ruled-paper style.");
Check(savedLayoutRoot["GutterWidthPx"]?.GetValue<int>() == 36, "Saved layouts should store the shared gutter width once per workspace.");
var loaded = new MainViewModel();
loaded.LoadFromJson(json, "smoke");

Check(loaded.Columns.Count == vm.Columns.Count, "JSON round-trip should preserve column count.");
Check(loaded.ThemePreset == vm.ThemePreset, "JSON round-trip should preserve theme preset.");
Check(loaded.Columns[0].PastePreset == PasteListPreset.Checklist, "JSON round-trip should preserve paste preset.");
Check(loaded.Columns[0].LineMarkerMode == LineMarkerMode.Checklist, "JSON round-trip should preserve gutter marker mode.");
Check(loaded.Columns[0].Text == "task one\ntask two", "JSON round-trip should keep checklist text clean without inline markers.");
Check(loaded.Columns[0].IsChecklistLineChecked(1), "JSON round-trip should preserve checked checklist line indexes.");
Check(loaded.Columns[0].WidthPx == 444, "JSON round-trip should preserve per-column width.");
Check(loaded.Columns[0].Images.Count == 1, "JSON round-trip should preserve column image attachments.");
Check(loaded.Columns[0].Images[0].OriginalFileName == "diagram.png", "JSON round-trip should preserve image display names.");
Check(Math.Abs(loaded.Columns[0].Images[0].Width - 420) < 0.001, "JSON round-trip should preserve image display width.");
Check(Math.Abs(loaded.Columns[0].Images[0].Left - 18) < 0.001 && Math.Abs(loaded.Columns[0].Images[0].Top - 26) < 0.001, "JSON round-trip should preserve image position.");
Check(loaded.Columns[0].Images[0].Layer == ColumnImageLayer.BehindText, "JSON round-trip should preserve image text layer.");
Check(!loaded.Columns[0].UseDefaultFont, "JSON round-trip should preserve per-column default-font toggle.");
Check(loaded.Columns[0].EditorFontFamily == "Consolas", "JSON round-trip should preserve per-column font family.");
Check(Math.Abs(loaded.Columns[0].EditorFontSize - 17) < 0.001, "JSON round-trip should preserve per-column font size.");
Check(loaded.Columns[0].EditorFontStyle == FontStyles.Italic, "JSON round-trip should preserve per-column font style.");
Check(loaded.Columns[0].EditorFontWeight == FontWeights.Bold, "JSON round-trip should preserve per-column font weight.");
Check(loaded.Columns[0].EditorTextColor == "#2A6F97", "JSON round-trip should preserve a custom column text colour.");
Check(loaded.Columns[0].HasCustomEditorTextColor, "A loaded custom text colour should restore its editor brush.");
Check(loaded.ActiveColumnId == loaded.Columns[1].Id, "JSON round-trip should restore the active column.");
Check(!loaded.SpellCheckEnabled, "JSON round-trip should preserve spellcheck setting.");
Check(loaded.EditorLanguageTag == "fr-FR", "JSON round-trip should preserve editor language setting.");
Check(loaded.LinedPaperEnabled && loaded.SelectedPaperStyle == PaperStyle.StrongRuled, "JSON round-trip should preserve the enabled ruled-paper style.");
Check(loaded.GutterWidthPx == 36 && loaded.Columns.All(column => Math.Abs(column.LineNumberColumnWidth.Value - 36) < 0.001), "JSON round-trip should restore one shared gutter width for every column.");
Check(loaded.GetActive()?.Title == vm.Columns[1].Title, "Restored active column should match the saved column.");
Check(!loaded.IsDirty, "Loaded layout should start clean.");

var legacyFontNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse legacy font layout.");
legacyFontNode["Version"] = 13;
legacyFontNode["EditorFontFamily"] = "Consolas";
legacyFontNode["EditorFontStyle"] = "Bold Italic";
var legacyFontColumns = legacyFontNode["Columns"]?.AsArray() ?? throw new InvalidOperationException("Could not find legacy font columns.");
var legacyFontFirstColumn = legacyFontColumns[0]?.AsObject() ?? throw new InvalidOperationException("Could not find the first legacy font column.");
legacyFontFirstColumn.Remove("FontStyle");
legacyFontFirstColumn.Remove("FontWeight");
var legacyFontLoaded = new MainViewModel
{
    EditorFontFamily = "Consolas",
    EditorFontStyleName = "Regular"
};
Check(legacyFontLoaded.LoadFromJson(legacyFontNode.ToJsonString(), "legacy-font"), "Older layouts without per-column font faces should still load.");
Check(legacyFontLoaded.EditorFontStyleName == "Bold Italic", "A loaded layout should apply its saved global font face.");
Check(
    legacyFontLoaded.Columns[0].EditorFontStyle == FontStyles.Italic
        && legacyFontLoaded.Columns[0].EditorFontWeight == FontWeights.Bold,
    "A column without saved font-face fields should inherit the global font style and weight from that layout, not the pre-load app state.");

var undefinedEnumNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse undefined-enum layout.");
var undefinedEnumColumns = undefinedEnumNode["Columns"]?.AsArray() ?? throw new InvalidOperationException("Could not find undefined-enum columns.");
var undefinedEnumFirstColumn = undefinedEnumColumns[0]?.AsObject() ?? throw new InvalidOperationException("Could not find the first undefined-enum column.");
var undefinedEnumImages = undefinedEnumFirstColumn["Images"]?.AsArray() ?? throw new InvalidOperationException("Could not find undefined-enum images.");
var undefinedEnumFirstImage = undefinedEnumImages[0]?.AsObject() ?? throw new InvalidOperationException("Could not find the first undefined-enum image.");
undefinedEnumFirstColumn["PastePreset"] = "999";
undefinedEnumFirstColumn["LineMarkerMode"] = "999";
undefinedEnumFirstImage["Layer"] = "999";
var undefinedEnumLoaded = new MainViewModel();
Check(undefinedEnumLoaded.LoadFromJson(undefinedEnumNode.ToJsonString(), "undefined-enums"), "Undefined numeric enum values should not invalidate an otherwise healthy layout.");
Check(undefinedEnumLoaded.Columns[0].PastePreset == PasteListPreset.None, "An undefined numeric paste preset should safely fall back to none.");
Check(undefinedEnumLoaded.Columns[0].LineMarkerMode == LineMarkerMode.Numbers, "An undefined numeric line-marker mode should safely fall back to numbers.");
Check(undefinedEnumLoaded.Columns[0].Images[0].Layer == ColumnImageLayer.InFrontOfText, "An undefined numeric picture layer should safely fall back in front of text.");

var legacyEnumNamesNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse legacy enum-name layout.");
var legacyEnumNameColumns = legacyEnumNamesNode["Columns"]?.AsArray() ?? throw new InvalidOperationException("Could not find legacy enum-name columns.");
var legacyEnumNameFirstColumn = legacyEnumNameColumns[0]?.AsObject() ?? throw new InvalidOperationException("Could not find the first legacy enum-name column.");
var legacyEnumNameImages = legacyEnumNameFirstColumn["Images"]?.AsArray() ?? throw new InvalidOperationException("Could not find legacy enum-name images.");
var legacyEnumNameFirstImage = legacyEnumNameImages[0]?.AsObject() ?? throw new InvalidOperationException("Could not find the first legacy enum-name image.");
legacyEnumNameFirstColumn["PastePreset"] = "checklist";
legacyEnumNameFirstColumn["LineMarkerMode"] = "bullets";
legacyEnumNameFirstImage["Layer"] = "behindtext";
var legacyEnumNamesLoaded = new MainViewModel();
Check(legacyEnumNamesLoaded.LoadFromJson(legacyEnumNamesNode.ToJsonString(), "legacy-enum-names"), "Valid legacy enum names should remain loadable case-insensitively.");
Check(legacyEnumNamesLoaded.Columns[0].PastePreset == PasteListPreset.Checklist, "The legacy checklist paste-preset name should remain supported.");
Check(legacyEnumNamesLoaded.Columns[0].LineMarkerMode == LineMarkerMode.Bullets, "The legacy bullets line-marker name should remain supported.");
Check(legacyEnumNamesLoaded.Columns[0].Images[0].Layer == ColumnImageLayer.BehindText, "The legacy behind-text picture-layer name should remain supported.");

var preserveTheme = new MainViewModel();
preserveTheme.ThemePreset = "Dark Mode";
preserveTheme.LoadFromJson(json, "smoke", preserveCurrentTheme: true);
Check(preserveTheme.ThemePreset == "Dark Mode", "Manual layout open should preserve the current theme.");

var recoveredThemeVm = new MainViewModel();
recoveredThemeVm.ThemePreset = "Dark Mode";
Check(
    recoveredThemeVm.LoadRecoverySnapshot(
        new WorkspaceRecoveryWorkspace("Recovered", json, null, SaveFileKind.Layout, true, false),
        preserveCurrentTheme: true),
    "Recovery snapshots should still load when preserving the current theme.");
Check(recoveredThemeVm.ThemePreset == "Dark Mode", "Recovery restore should preserve the current app theme.");

await InfrastructureSmokeTests.RunAsync(tests);

var workflowDefinition = WorkflowSmokeTests.Run(tests, json);


var legacyNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse round-trip JSON for legacy normalization test.");
legacyNode["Version"] = 11;
var legacyColumns = legacyNode["Columns"]?.AsArray() ?? throw new InvalidOperationException("Could not find columns array for legacy normalization test.");
var legacyFirstColumn = legacyColumns[0]?.AsObject() ?? throw new InvalidOperationException("Could not find first column for legacy normalization test.");
legacyFirstColumn.Remove("EditorTextColor");
legacyFirstColumn["Text"] = "line one\\r\\nline two\\nline three";
var legacyLoaded = new MainViewModel();
Check(legacyLoaded.LoadFromJson(legacyNode.ToJsonString(), "legacy"), "Legacy-escaped layout JSON should still load.");
Check(legacyLoaded.Columns[0].Text == "line one\nline two\nline three", "Legacy-escaped newline sequences should be decoded into real line breaks during load.");
Check(legacyLoaded.Columns[0].EditorTextColor == ColumnTextColorService.ThemeDefault, "Older layouts without colour data should use the theme text colour.");
legacyFirstColumn["Text"] = "pitch idea -> break it down -> lock it -> structure tree -> .sln -> build in sections";
Check(legacyLoaded.LoadFromJson(legacyNode.ToJsonString(), "legacy"), "Legacy inline layout JSON should still load.");
Check(legacyLoaded.Columns[0].Text == "pitch idea -> break it down -> lock it -> structure tree -> .sln -> build in sections", "Legacy inline text should remain byte-for-byte intact during load.");
legacyFirstColumn["Text"] = "- [ ] first task\n- [x] done task";
legacyFirstColumn["LineMarkerMode"] = null;
legacyFirstColumn["CheckedChecklistLineIndexes"] = null;
Check(legacyLoaded.LoadFromJson(legacyNode.ToJsonString(), "legacy"), "Legacy inline checklist-marker layouts should still load.");
Check(legacyLoaded.Columns[0].LineMarkerMode == LineMarkerMode.Checklist, "Legacy checklist-marker text should migrate to checklist gutter mode.");
Check(legacyLoaded.Columns[0].Text == "first task\ndone task", "Legacy checklist-marker text should decode to clean plain text.");
Check(legacyLoaded.Columns[0].IsChecklistLineChecked(1), "Legacy checklist-marker migration should restore checked rows in gutter metadata.");
var versionFourteenNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse version-14 compatibility layout.");
versionFourteenNode["Version"] = 14;
var versionFourteenColumns = versionFourteenNode["Columns"]?.AsArray() ?? throw new InvalidOperationException("Could not find version-14 columns.");
var versionFourteenFirstColumn = versionFourteenColumns[0]?.AsObject() ?? throw new InvalidOperationException("Could not find the version-14 first column.");
var validSingleLineText = string.Join(' ', Enumerable.Repeat("structured", 12));
versionFourteenFirstColumn["Text"] = validSingleLineText;
versionFourteenFirstColumn.Remove("EditorTextColor");
var versionFourteenLoaded = new MainViewModel();
Check(versionFourteenLoaded.LoadFromJson(versionFourteenNode.ToJsonString(), "version-14"), "Version-14 layouts should remain loadable after adding text colour.");
Check(versionFourteenLoaded.Columns[0].Text == validSingleLineText, "Version-14 text should not be passed through older inline-text migration again.");
Check(versionFourteenLoaded.Columns[0].EditorTextColor == ColumnTextColorService.ThemeDefault, "Version-14 layouts should gain the theme text colour.");
var currentLiteralEscapeNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse current layout for literal-escape preservation.");
var currentLiteralEscapeColumns = currentLiteralEscapeNode["Columns"]?.AsArray() ?? throw new InvalidOperationException("Could not find current layout columns.");
currentLiteralEscapeColumns[0]!.AsObject()["Text"] = @"regex \r\n and code \n stay literal";
var currentLiteralEscapeLoaded = new MainViewModel();
Check(currentLiteralEscapeLoaded.LoadFromJson(currentLiteralEscapeNode.ToJsonString(), "current-literal"), "Current layouts containing literal escape text should load.");
Check(currentLiteralEscapeLoaded.Columns[0].Text == @"regex \r\n and code \n stay literal", "Current layout text should never decode literal backslash escape sequences.");
var futureLayoutNode = JsonNode.Parse(json)!.AsObject();
futureLayoutNode["Version"] = 999;
var futureLayoutTarget = new MainViewModel();
var futureLayoutBefore = futureLayoutTarget.ToLayoutJson();
Check(!futureLayoutTarget.LoadFromJson(futureLayoutNode.ToJsonString(), "future"), "Layouts from unsupported future schema versions should be rejected.");
Check(futureLayoutTarget.ToLayoutJson() == futureLayoutBefore, "Rejecting a future layout should not alter the current workspace.");
var versionFifteenNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse version-15 compatibility layout.");
versionFifteenNode["Version"] = 15;
versionFifteenNode.Remove("PaperStyle");
var versionFifteenLoaded = new MainViewModel();
Check(versionFifteenLoaded.LoadFromJson(versionFifteenNode.ToJsonString(), "version-15"), "Version-15 layouts should remain loadable after adding paper styles.");
Check(versionFifteenLoaded.LinedPaperEnabled && versionFifteenLoaded.SelectedPaperStyle == PaperStyle.Ruled, "Older lined-paper layouts should open as ruled paper.");
var versionEighteenNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse version-18 compatibility layout.");
versionEighteenNode["Version"] = 18;
versionEighteenNode.Remove("GutterWidthPx");
var versionEighteenLoaded = new MainViewModel();
Check(versionEighteenLoaded.LoadFromJson(versionEighteenNode.ToJsonString(), "version-18"), "Version-18 layouts should remain loadable after adding a shared gutter width.");
Check(versionEighteenLoaded.GutterWidthPx == MainViewModel.MinimumGutterWidthPx, "Layouts without a saved gutter width should use the smallest default.");
var legacyGutterNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse legacy shared-gutter layout.");
legacyGutterNode["Version"] = 18;
legacyGutterNode["GutterWidthPx"] = 64;
var legacyGutterLoaded = new MainViewModel();
Check(legacyGutterLoaded.LoadFromJson(legacyGutterNode.ToJsonString(), "legacy-gutter"), "Layouts with an earlier saved gutter width should still load.");
Check(legacyGutterLoaded.GutterWidthPx == 64 && legacyGutterLoaded.Columns.All(column => Math.Abs(column.LineNumberColumnWidth.Value - 64) < 0.001), "A saved shared gutter width should restore across all columns.");
var invalidPaperStyleNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse invalid paper-style layout.");
invalidPaperStyleNode["PaperStyle"] = "Unknown";
var invalidPaperStyleLoaded = new MainViewModel();
Check(invalidPaperStyleLoaded.LoadFromJson(invalidPaperStyleNode.ToJsonString(), "invalid-paper-style"), "An unknown paper style should not invalidate an otherwise healthy layout.");
Check(invalidPaperStyleLoaded.SelectedPaperStyle == PaperStyle.Ruled, "An unknown saved paper style should safely fall back to ruled paper.");
foreach (var retiredPaperStyle in new[] { "Grid", "Dots" })
{
    var retiredPaperStyleNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException($"Could not parse the retired {retiredPaperStyle} paper-style layout.");
    retiredPaperStyleNode["PaperStyle"] = retiredPaperStyle;
    var retiredPaperStyleLoaded = new MainViewModel();
    Check(
        retiredPaperStyleLoaded.LoadFromJson(retiredPaperStyleNode.ToJsonString(), $"retired-{retiredPaperStyle}-paper-style")
            && retiredPaperStyleLoaded.SelectedPaperStyle == PaperStyle.Ruled,
        $"Saved {retiredPaperStyle} paper should open as the original ruled paper.");
}
var rawDocument = new MainViewModel();
rawDocument.LoadTextDocument("alpha\n beta", "notes.txt", "C:\\temp\\notes.txt", SaveFileKind.TextDocument);
Check(rawDocument.Columns.Count == 1, "Raw text open should create a single column.");
Check(rawDocument.Columns[0].Text == "alpha\n beta", "Raw text open should preserve text exactly.");
Check(rawDocument.CurrentFileKind == SaveFileKind.TextDocument, "Raw text open should track the file as a text document.");
Check(rawDocument.RequiresSaveAsBeforeOverwrite, "Opened source files should require Save As before direct overwrite.");
Check(!rawDocument.CanSaveCurrentFileDirectly, "Opened source files should not allow direct Save before Save As.");
rawDocument.AddColumn();
Check(rawDocument.CurrentFileKind == SaveFileKind.Layout, "Adding a column to a raw text document should promote it to a layout.");
Check(string.IsNullOrWhiteSpace(rawDocument.CurrentFilePath), "Promoting a raw text document should detach it from the original file path.");
Check(!rawDocument.RequiresSaveAsBeforeOverwrite, "Promoted layouts should no longer require Save As once detached.");

var styledRawDocument = new MainViewModel();
styledRawDocument.LoadTextDocument("styled text", "styled.txt", "C:\\temp\\styled.txt", SaveFileKind.TextDocument);
styledRawDocument.PrepareForRichContent();
styledRawDocument.Columns[0].EditorTextColor = ColumnTextColorService.Red;
Check(styledRawDocument.CurrentFileKind == SaveFileKind.Layout, "Applying column formatting should promote a raw text document to a layout.");
Check(string.IsNullOrWhiteSpace(styledRawDocument.CurrentFilePath), "Promoting formatted text should detach it from the original raw file.");

var resizedRawDocument = new MainViewModel();
resizedRawDocument.LoadTextDocument("width-sensitive text", "width.txt", "C:\\temp\\width.txt", SaveFileKind.TextDocument);
resizedRawDocument.Columns[0].WidthPx = 420;
Check(resizedRawDocument.CurrentFileKind == SaveFileKind.Layout && resizedRawDocument.CurrentFilePath is null, "Changing persistent per-column layout data should promote a raw document before it can be lost.");

var gutterRawDocument = new MainViewModel();
gutterRawDocument.LoadTextDocument("gutter-sensitive text", "gutter.txt", "C:\\temp\\gutter.txt", SaveFileKind.TextDocument);
gutterRawDocument.GutterWidthPx = 64;
Check(gutterRawDocument.CurrentFileKind == SaveFileKind.Layout && gutterRawDocument.CurrentFilePath is null, "Changing the gutter width should promote a raw document before its layout setting can be lost.");

var styledExport = new MainViewModel();
styledExport.LoadFromExportText("ColumnPad Export\nFormat: Text\n\n===== Notes =====\n\ntext\n", "notes.txt", "C:\\temp\\notes.txt");
styledExport.PrepareForRichContent();
styledExport.Columns[0].EditorTextColor = ColumnTextColorService.Blue;
Check(styledExport.CurrentFileKind == SaveFileKind.Layout, "Applying rich formatting should promote a text export to a native layout.");
Check(string.IsNullOrWhiteSpace(styledExport.CurrentFilePath), "Promoting a rich text export should detach it from the lossy export path.");

var nativeLayoutPath = Path.Combine(Path.GetTempPath(), $"columnpad-native-{Guid.NewGuid():N}.columnpad.json");
try
{
    File.WriteAllText(nativeLayoutPath, json);
    var nativeLayout = new MainViewModel();
    Check(nativeLayout.LoadFromJson(File.ReadAllText(nativeLayoutPath), "native.columnpad.json", nativeLayoutPath), "Native layout JSON should load from disk.");
    Check(nativeLayout.CurrentFileKind == SaveFileKind.Layout, "Native layout JSON should track as a layout file.");
    Check(!nativeLayout.RequiresSaveAsBeforeOverwrite, "Native layout JSON should allow direct Save after opening.");
    Check(nativeLayout.CanSaveCurrentFileDirectly, "Native layout JSON should expose direct Save after opening.");
}
finally
{
    if (File.Exists(nativeLayoutPath))
        File.Delete(nativeLayoutPath);
}

var beforeInvalidLoadCount = loaded.Columns.Count;
loaded.LoadFromJson("{ not valid json", "smoke");
Check(loaded.StatusText == "Invalid layout file.", "Invalid JSON should report an invalid layout status.");
Check(loaded.Columns.Count == beforeInvalidLoadCount, "Invalid JSON should not mutate existing column state.");
var damagedLayout = JsonNode.Parse(json)!.AsObject();
damagedLayout["Columns"]!.AsArray()[0] = "not a column object";
Check(!loaded.LoadFromJson(damagedLayout.ToJsonString(), "damaged"), "Layout loading should reject malformed column entries.");
Check(loaded.Columns.Count == beforeInvalidLoadCount, "Rejecting a damaged layout should leave the current workspace untouched.");

var metrics = new ColumnViewModel
{
    Text = "alpha\n\u2610 first\n\u2611 second\n- [ ] third\n- [x] fourth"
};
Check(metrics.ChecklistTotal == 4, "ChecklistTotal should count symbol and markdown checklist items.");
Check(metrics.ChecklistDone == 2, "ChecklistDone should count checked symbol and markdown items.");

var carriageReturnMetrics = new ColumnViewModel { Text = "first\rsecond\r\nthird\nfourth" };
Check(carriageReturnMetrics.LineCount == 4, "Column metrics should count LF, CRLF, and standalone CR line breaks consistently.");

var indentedChecklistMetrics = new ColumnViewModel
{
    Text = "  - [ ] nested task\n    \u2611 done task"
};
Check(indentedChecklistMetrics.ChecklistTotal == 2, "ChecklistTotal should include indented checklist markers.");
Check(indentedChecklistMetrics.ChecklistDone == 1, "ChecklistDone should include indented checked markers.");

var checklistModeMetrics = new ColumnViewModel
{
    Text = "first\n\nsecond\nthird",
    LineMarkerMode = LineMarkerMode.Checklist
};
checklistModeMetrics.SetCheckedChecklistLineIndexes([2]);
Check(checklistModeMetrics.ChecklistTotal == 3, "Checklist gutter mode should count only non-empty checklist lines.");
Check(checklistModeMetrics.ChecklistDone == 1, "Checklist gutter mode should count checked lines from gutter metadata.");
checklistModeMetrics.Text = "first\ninserted\n\nsecond\nthird";
Check(checklistModeMetrics.GetCheckedChecklistLineIndexes().SequenceEqual([3]), "Checklist remapping should move checks with inserted lines.");
checklistModeMetrics.Text = "first\ninserted\n\nthird";
Check(checklistModeMetrics.GetCheckedChecklistLineIndexes().Count == 0, "Checklist remapping should remove a check when its line is deleted.");

var checklistEnterAtStart = new ColumnViewModel { Text = "task", LineMarkerMode = LineMarkerMode.Checklist };
checklistEnterAtStart.SetCheckedChecklistLineIndexes([0]);
checklistEnterAtStart.Text = "\ntask";
Check(checklistEnterAtStart.GetCheckedChecklistLineIndexes().SequenceEqual([1]), "Checklist remapping should keep a check with text shifted down by Enter at line start.");

var singleModeVm = new MainViewModel();
singleModeVm.SetColumnCount(3);
var retainedColumn = singleModeVm.Columns[1];
retainedColumn.Text = "keep me";
retainedColumn.LineMarkerMode = LineMarkerMode.Checklist;
retainedColumn.SetCheckedChecklistLineIndexes([0]);
retainedColumn.Images.Add(new ColumnImageViewModel("C:\\images\\retained.png", left: 34, top: 45));
Check(singleModeVm.KeepOnlyColumn(retainedColumn.Id), "Single text conversion should accept an existing column.");
Check(singleModeVm.Columns.Count == 1 && ReferenceEquals(singleModeVm.Columns[0], retainedColumn), "Single text conversion should retain the selected column object rather than copy selected fields.");
Check(singleModeVm.Columns[0].Images.Count == 1 && singleModeVm.Columns[0].IsChecklistLineChecked(0), "Single text conversion should retain pictures and checklist metadata.");

singleModeVm.ClearAll();
Check(singleModeVm.Columns[0].Images.Count == 0, "Clear All should remove column pictures.");
Check(singleModeVm.Columns[0].GetCheckedChecklistLineIndexes().Count == 0, "Clear All should remove checklist metadata.");

var imageSubscriptionColumn = new ColumnViewModel();
var removedImage = new ColumnImageViewModel("C:\\images\\removed.png");
var imageChangeNotifications = 0;
imageSubscriptionColumn.PropertyChanged += (_, e) =>
{
    if (e.PropertyName == nameof(ColumnViewModel.Images))
        imageChangeNotifications++;
};
imageSubscriptionColumn.Images.Add(removedImage);
imageSubscriptionColumn.ClearImages();
imageChangeNotifications = 0;
removedImage.Width = 512;
Check(imageChangeNotifications == 0, "Removed pictures should no longer notify their former column when they change.");

var richContentVm = new MainViewModel();
richContentVm.LoadTextDocument("plain text", "plain.txt", "C:\\notes\\plain.txt", SaveFileKind.TextDocument);
richContentVm.PrepareForRichContent();
richContentVm.Columns[0].Images.Add(new ColumnImageViewModel("C:\\images\\rich.png"));
Check(richContentVm.CurrentFileKind == SaveFileKind.Layout && richContentVm.CurrentFilePath is null, "Adding rich content to a text document should promote it to a native layout instead of silently losing the picture.");
Check(richContentVm.IsDirty, "Promoting a text document for a picture should mark it dirty.");

var lineToggleVm = new MainViewModel();
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value - MainViewModel.MinimumGutterWidthPx) < 0.001), "Line-number gutter should default to the smallest visible width.");
lineToggleVm.GutterWidthPx = 36;
Check(lineToggleVm.IsDirty, "Changing the gutter width should mark a layout dirty.");
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value - 36) < 0.001), "Changing the shared gutter width should update every existing column.");
lineToggleVm.GutterWidthPx = MainViewModel.MaximumGutterWidthPx + 1;
Check(lineToggleVm.GutterWidthPx == MainViewModel.MaximumGutterWidthPx, "Gutter width should clamp to its maximum supported value.");
lineToggleVm.GutterWidthPx = MainViewModel.MinimumGutterWidthPx - 1;
Check(lineToggleVm.GutterWidthPx == MainViewModel.MinimumGutterWidthPx, "Gutter width should clamp to its minimum supported value.");
lineToggleVm.GutterWidthPx = 36;
lineToggleVm.ShowLineNumbers = false;
Check(lineToggleVm.Columns.All(c => c.ShowLineNumbersVisibility == Visibility.Collapsed), "Turning line numbers off should collapse line-number visibility for all columns.");
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value) < 0.001), "Turning line numbers off should collapse gutter width for all columns.");
lineToggleVm.ShowLineNumbers = true;
Check(lineToggleVm.Columns.All(c => c.ShowLineNumbersVisibility == Visibility.Visible), "Turning line numbers back on should restore line-number visibility for all columns.");
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value - 36) < 0.001), "Turning line numbers back on should restore the chosen shared gutter width.");
lineToggleVm.Columns[0].WidthPx = 410;
lineToggleVm.Columns[1].WidthPx = 430;
lineToggleVm.AddColumn();
Check(
    lineToggleVm.Columns[0].WidthPx == 410
        && lineToggleVm.Columns[1].WidthPx == 430
        && lineToggleVm.Columns[^1].WidthPx is null
        && Math.Abs(lineToggleVm.Columns[^1].LineNumberColumnWidth.Value - 36) < 0.001,
    "Adding a column should preserve existing widths while the new column inherits the preferred default and workspace gutter width.");

var resetWidthVm = new MainViewModel();
const int preferredColumnWidthPx = 438;
var resetRebuildCount = 0;
resetWidthVm.RequestRebuildColumns += (_, __) => resetRebuildCount++;
resetWidthVm.Columns[0].WidthPx = 480;
resetWidthVm.Columns[0].IsWidthLocked = true;
resetWidthVm.ResetActiveColumnWidth(preferredColumnWidthPx);
Check(
    resetWidthVm.Columns[0].WidthPx is null
        && !resetWidthVm.Columns[0].IsWidthLocked
        && resetWidthVm.StatusText == $"Reset {resetWidthVm.Columns[0].Title} to the default {preferredColumnWidthPx}px width."
        && resetRebuildCount == 1,
    "Resetting one column should restore the preferred default width, unlock it, report that default, and rebuild the strip.");
foreach (var column in resetWidthVm.Columns)
{
    column.WidthPx = 480;
    column.IsWidthLocked = true;
}
resetWidthVm.ResetAllColumnWidths(preferredColumnWidthPx);
Check(
    resetWidthVm.Columns.All(column => column.WidthPx is null && !column.IsWidthLocked)
        && resetWidthVm.StatusText == $"Reset all columns to the default {preferredColumnWidthPx}px width."
        && resetRebuildCount == 2,
    "Resetting all columns should restore the preferred default width, unlock every column, report that default, and rebuild the strip.");

var malformedWidthNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse layout for width validation.");
var malformedWidthColumns = malformedWidthNode["Columns"]?.AsArray() ?? throw new InvalidOperationException("Could not find columns for width validation.");
malformedWidthColumns[0]!.AsObject()["WidthPx"] = 999_999;
var clampedWidthLoaded = new MainViewModel();
Check(clampedWidthLoaded.LoadFromJson(malformedWidthNode.ToJsonString(), "oversized-width"), "Layouts with oversized stored widths should still load safely.");
Check(clampedWidthLoaded.Columns[0].WidthPx == (int)WorkspaceConstraints.MaximumColumnWidth, "Oversized stored widths should clamp to the supported maximum.");
malformedWidthColumns[0]!.AsObject()["WidthPx"] = 0;
var flexibleWidthLoaded = new MainViewModel();
Check(flexibleWidthLoaded.LoadFromJson(malformedWidthNode.ToJsonString(), "zero-width"), "Layouts with a zero stored width should still load safely.");
Check(flexibleWidthLoaded.Columns[0].WidthPx is null, "A zero stored width should restore the normal display width instead of a broken fixed width.");

var liveStatusVm = new MainViewModel();
liveStatusVm.Columns[0].Title = "Inbox";
Check(liveStatusVm.StatusText.Contains("Selected: Inbox"), "Status text should refresh when the active column is renamed.");
liveStatusVm.Columns[0].Text = "\u2610 one\n\u2611 two";
Check(liveStatusVm.StatusText.Contains("Done: 1/2"), "Status text should refresh when active-column checklist progress changes.");

var cleanExportVm = new MainViewModel();
cleanExportVm.SetColumnCount(2);
cleanExportVm.Columns[0].Title = "Alpha\r\nPlan";
cleanExportVm.Columns[0].Text = "one\r\ntwo\n\n";
cleanExportVm.Columns[1].Title = "Beta";
cleanExportVm.Columns[1].Text = "three";
var cleanTextExport = cleanExportVm.BuildExportText().Replace("\r\n", "\n", StringComparison.Ordinal);
Check(cleanTextExport == "ColumnPad Export\nFormat: Text\nVersion: 2\n\n===== Alpha Plan =====\n\none\ntwo\n\n===== Beta =====\n\nthree\n", "Text export should use a versioned marker and readable sections without trailing blank blocks.");
Check(!cleanTextExport.Contains("\\n", StringComparison.Ordinal), "Text export should write real line breaks, not escaped JSON-style line breaks.");
var cleanJsonExport = cleanExportVm.BuildExportJson();
var cleanJsonExportRoot = JsonNode.Parse(cleanJsonExport)?.AsObject() ?? throw new InvalidOperationException("Could not parse readable JSON export.");
var cleanJsonExportColumns = cleanJsonExportRoot["Columns"]?.AsArray() ?? throw new InvalidOperationException("Readable JSON export should include columns.");
Check(
    cleanJsonExportRoot.Count == 3
        && cleanJsonExportRoot["FileType"]?.GetValue<string>() == "ColumnPadTextExport"
        && cleanJsonExportRoot["Version"]?.GetValue<int>() == 1
        && cleanJsonExportColumns.Count == 2
        && cleanJsonExportColumns[0]?.AsObject().Count == 2,
    "JSON export should be a concise, readable title-and-text format without layout data.");
Check(
    cleanJsonExportColumns[0]?["Title"]?.GetValue<string>() == "Alpha Plan"
        && cleanJsonExportColumns[0]?["Text"]?.GetValue<string>() == "one\r\ntwo\n\n"
        && cleanJsonExportColumns[1]?["Title"]?.GetValue<string>() == "Beta",
    "JSON export should preserve normalized titles and original text exactly.");

var collisionExportVm = new MainViewModel();
collisionExportVm.SetColumnCount(2);
collisionExportVm.Columns[0].Title = "Text boundaries";
collisionExportVm.Columns[0].Text = "before\n===== this is body text =====\n\\leading slash";
collisionExportVm.Columns[1].Title = "JSON boundaries";
collisionExportVm.Columns[1].Text = "before\n## this is a body heading\n\\## literal slash heading";
var collisionTextRoundTrip = new MainViewModel();
collisionTextRoundTrip.LoadFromExportText(collisionExportVm.BuildExportText(), "collision.txt");
Check(collisionTextRoundTrip.Columns.Count == 2, "Versioned text export should not split body lines that resemble column headers.");
Check(collisionTextRoundTrip.Columns[0].Text == collisionExportVm.Columns[0].Text, "Versioned text export should preserve header-like and backslash-prefixed body lines.");
var collisionJsonRoundTrip = new MainViewModel();
collisionJsonRoundTrip.LoadFromExportJson(collisionExportVm.BuildExportJson(), "collision.json");
Check(collisionJsonRoundTrip.Columns.Count == 2, "JSON export should preserve separate columns without text markers.");
Check(collisionJsonRoundTrip.Columns[1].Text == collisionExportVm.Columns[1].Text, "JSON export should preserve headings, backslashes, and multiline text exactly.");

var exportedText = "ColumnPad Export\nFormat: Text\n\n===== Alpha =====\n\none\n\n===== Beta =====\n\n.\n";
var importedFromText = new MainViewModel();
importedFromText.LoadFromExportText(exportedText, "export.txt");
Check(importedFromText.Columns.Count == 2, "Text import should create one column per export section.");
Check(importedFromText.Columns[0].Title == "Alpha", "Text import should preserve first column title.");
Check(importedFromText.Columns[0].Text == "one", "Text import should preserve first column body.");
Check(importedFromText.Columns[1].Title == "Beta", "Text import should preserve second column title.");
Check(importedFromText.Columns[1].Text == ".", "Text import should preserve second column body.");
Check(!importedFromText.IsDirty, "Imported text exports should start clean.");

var oversizedExport = "ColumnPad Export\nFormat: Text\n\n" + string.Join(
    "\n\n",
    Enumerable.Range(1, WorkspaceConstraints.MaxColumns + 1).Select(index => $"===== Column {index} =====\n\nvalue"));
var oversizedImport = new MainViewModel();
var oversizedImportRejected = false;
try
{
    oversizedImport.LoadFromExportText(oversizedExport, "oversized.txt");
}
catch (InvalidDataException)
{
    oversizedImportRejected = true;
}
Check(oversizedImportRejected, "Imports above the supported column limit should be rejected before changing the workspace.");
Check(oversizedImport.Columns.Count == 3, "A rejected oversized import should leave the existing workspace intact.");

var tempRoot = Path.Combine(Path.GetTempPath(), $"ColumnPadStudioSmoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);
try
{
    var firstImageSource = Path.Combine(tempRoot, "first.png");
    var secondImageSource = Path.Combine(tempRoot, "second.png");
    var imageEncoder = new PngBitmapEncoder();
    imageEncoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(
        1,
        1,
        96,
        96,
        PixelFormats.Bgra32,
        null,
        new byte[] { 0x20, 0x60, 0xA0, 0xFF },
        4)));
    using (var imageStream = File.Create(firstImageSource))
        imageEncoder.Save(imageStream);
    File.Copy(firstImageSource, secondImageSource);

    var firstImageImport = ColumnImageFileService.ImportImage(firstImageSource);
    var secondImageImport = ColumnImageFileService.ImportImage(secondImageSource);
    Check(firstImageImport.AssetId == secondImageImport.AssetId, "Identical picture content should receive one stable asset identity.");
    Check(firstImageImport.Content.SequenceEqual(secondImageImport.Content), "Identical picture imports should preserve identical embedded content.");
    Check(string.IsNullOrEmpty(firstImageImport.FilePath), "New picture imports should not create unmanaged permanent image copies.");
    Check(firstImageImport.OriginalFileName == "first.png" && secondImageImport.OriginalFileName == "second.png", "Reused picture assets should preserve each imported display name.");

    var portablePictureVm = new MainViewModel();
    portablePictureVm.SetColumnCount(1);
    portablePictureVm.Columns[0].Images.Add(new ColumnImageViewModel(
        firstImageImport.FilePath,
        firstImageImport.OriginalFileName,
        firstImageImport.DisplayWidth,
        firstImageImport.PixelWidth,
        firstImageImport.PixelHeight,
        imageContent: firstImageImport.Content));
    var portablePictureJson = portablePictureVm.ToLayoutJson();
    var portablePictureRoot = JsonNode.Parse(portablePictureJson)!.AsObject();
    var portablePictureContent = portablePictureRoot["Columns"]![0]!["Images"]![0]!["Content"]?.GetValue<string>();
    Check(!string.IsNullOrWhiteSpace(portablePictureContent), "Native layouts should embed bounded picture content for portability.");
    var portablePictureLoaded = new MainViewModel();
    Check(portablePictureLoaded.LoadFromJson(portablePictureJson, "portable-picture"), "A portable-picture layout should load after its original managed file is removed.");
    Check(portablePictureLoaded.Columns[0].Images[0].CanDisplayImage, "Embedded picture content should display without the original local path.");

    var oversizedImagePath = Path.Combine(tempRoot, "oversized.png");
    using (var oversizedImageStream = File.Create(oversizedImagePath))
        oversizedImageStream.SetLength(ColumnImageFileService.MaxImageFileBytes + 1L);
    var oversizedImageRejected = false;
    try
    {
        _ = ColumnImageFileService.ImportImage(oversizedImagePath);
    }
    catch (InvalidDataException)
    {
        oversizedImageRejected = true;
    }
    Check(oversizedImageRejected, "Picture import should reject files above the bounded image size before decoding them.");

    var tempTextPath = Path.Combine(tempRoot, "loaded.txt");
    File.WriteAllText(tempTextPath, exportedText);

    var saveLoadedText = new MainViewModel();
    saveLoadedText.LoadTextDocument("updated", "loaded.txt", tempTextPath, SaveFileKind.TextDocument);
    saveLoadedText.Columns[0].Text = "changed again";

    Check(saveLoadedText.IsDirty, "Editing a loaded text document should mark it dirty.");
    Check(!saveLoadedText.SaveCurrentFile(), "SaveCurrentFile should require Save As on first save after opening a source file.");
    Check(File.ReadAllText(tempTextPath) == exportedText, "Requiring Save As should prevent overwriting the original opened source file.");

    var savedCopyPath = Path.Combine(tempRoot, "loaded-copy.txt");
    saveLoadedText.SaveToPath(savedCopyPath, SaveFileKind.TextDocument);
    Check(!saveLoadedText.RequiresSaveAsBeforeOverwrite, "Save As should clear the Save As requirement.");
    Check(saveLoadedText.SaveCurrentFile(), "SaveCurrentFile should work after a Save As path is chosen.");
    Check(!saveLoadedText.IsDirty, "Saving should clear the dirty flag.");
    Check(File.ReadAllText(savedCopyPath) == "changed again", "Saving after Save As should write to the new file path.");

    var recoveryRoot = Path.Combine(tempRoot, "recovery");
    var recoveryWorkspaces = new[]
    {
        new WorkspaceRecoveryWorkspace("Workspace A", vm.ToLayoutJson(), tempTextPath, SaveFileKind.TextDocument, true, true, 5, true),
        new WorkspaceRecoveryWorkspace("Workspace B", loaded.ToLayoutJson(), null, SaveFileKind.Layout, false, false)
    };

    WorkspaceRecoveryStore.Save(recoveryWorkspaces, 1, recoveryRoot);
    Check(WorkspaceRecoveryStore.TryLoad(out var recoverySnapshot, recoveryRoot), "Recovery store should load a saved manifest.");
    Check(recoverySnapshot.Workspaces.Count == 2, "Recovery store should restore every saved workspace.");
    Check(recoverySnapshot.ActiveWorkspaceIndex == 1, "Recovery store should preserve the active workspace index.");
    Check(recoverySnapshot.Workspaces[0].CurrentFileKind == SaveFileKind.TextDocument, "Recovery store should preserve file kinds per workspace.");
    Check(recoverySnapshot.Workspaces[0].CurrentFilePath == tempTextPath, "Recovery store should preserve file paths per workspace.");
    Check(recoverySnapshot.Workspaces[0].IsDirty, "Recovery store should preserve dirty state per workspace.");
    Check(recoverySnapshot.Workspaces[0].RequiresSaveAsBeforeOverwrite, "Recovery store should preserve Save As requirements per workspace.");
    Check(recoverySnapshot.Workspaces[0].LastMultiColumnCount == 5, "Recovery store should preserve the remembered multi-column count.");
    Check(recoverySnapshot.Workspaces[0].HasSessionChanges, "Recovery store should preserve unsaved workspace metadata.");

    var recoveredWorkspaceVm = new MainViewModel();
    Check(recoveredWorkspaceVm.LoadRecoverySnapshot(recoverySnapshot.Workspaces[0]), "Recovery load should accept a saved workspace snapshot.");
    Check(recoveredWorkspaceVm.CurrentFileKind == SaveFileKind.TextDocument, "Recovered workspace should restore its file kind.");
    Check(recoveredWorkspaceVm.CurrentFilePath == tempTextPath, "Recovered workspace should restore its file path.");
    Check(recoveredWorkspaceVm.RequiresSaveAsBeforeOverwrite, "Recovered workspace should restore Save As requirements.");
    Check(recoveredWorkspaceVm.IsDirty, "Recovered dirty workspace should still be dirty.");
    Check(recoveredWorkspaceVm.Columns.Count == vm.Columns.Count, "Recovered workspace should restore its layout content.");

    var legacyMarkdownRecoveryRoot = Path.Combine(tempRoot, "legacy-markdown-recovery");
    WorkspaceRecoveryStore.Save([recoveryWorkspaces[0]], 0, legacyMarkdownRecoveryRoot);
    var legacyGenerationName = File.ReadAllText(Path.Combine(legacyMarkdownRecoveryRoot, "current-generation.txt")).Trim();
    var legacyManifestPath = Path.Combine(legacyMarkdownRecoveryRoot, legacyGenerationName, "manifest.json");
    var legacyManifest = JsonNode.Parse(File.ReadAllText(legacyManifestPath))?.AsObject() ?? throw new InvalidOperationException("Could not parse legacy recovery manifest.");
    var legacyWorkspaceEntry = legacyManifest["Workspaces"]?[0]?.AsObject() ?? throw new InvalidOperationException("Could not find legacy recovery workspace.");
    legacyWorkspaceEntry["CurrentFileKind"] = "MarkdownDocument";
    legacyWorkspaceEntry["CurrentFilePath"] = "C:\\temp\\legacy.md";
    legacyWorkspaceEntry["RequiresSaveAsBeforeOverwrite"] = true;
    File.WriteAllText(legacyManifestPath, legacyManifest.ToJsonString());
    Check(WorkspaceRecoveryStore.TryLoad(out var migratedMarkdownRecovery, legacyMarkdownRecoveryRoot), "Recovery should load workspaces created before Markdown file support was removed.");
    Check(
        migratedMarkdownRecovery.Workspaces[0].CurrentFileKind == SaveFileKind.Layout
            && migratedMarkdownRecovery.Workspaces[0].CurrentFilePath is null
            && !migratedMarkdownRecovery.Workspaces[0].RequiresSaveAsBeforeOverwrite,
        "Recovered Markdown workspaces should detach from the retired file type and become native layouts.");

    WorkspaceRecoveryStore.Save([recoveryWorkspaces[0]], 0, recoveryRoot);
    Check(WorkspaceRecoveryStore.TryLoad(out var trimmedRecoverySnapshot, recoveryRoot), "Recovery store should still load after shrinking the workspace list.");
    Check(trimmedRecoverySnapshot.Workspaces.Count == 1, "Recovery store should drop stale workspaces when fewer tabs are saved.");
    var recoveryGenerations = Directory.GetDirectories(recoveryRoot, "generation-*");
    Check(recoveryGenerations.Length == 2, "Recovery store should retain the current and previous complete generations only.");

    var currentGenerationName = File.ReadAllText(Path.Combine(recoveryRoot, "current-generation.txt")).Trim();
    File.WriteAllText(Path.Combine(recoveryRoot, currentGenerationName, "manifest.json"), "{ damaged");
    Check(WorkspaceRecoveryStore.TryLoad(out var fallbackRecoverySnapshot, recoveryRoot), "Recovery store should fall back when the newest generation is damaged.");
    Check(fallbackRecoverySnapshot.Workspaces.Count == 2, "Recovery fallback should restore the previous complete generation rather than a mixed snapshot.");

    Check(WorkspaceRecoveryStore.TryClear(recoveryRoot), "Recovery cleanup should report a successful directory removal.");
    Check(!Directory.Exists(recoveryRoot), "Recovery clear should remove the recovery directory.");
    Check(WorkspaceRecoveryStore.TryClear(recoveryRoot), "Recovery cleanup should be harmless when no recovery directory exists.");
}
finally
{
    if (Directory.Exists(tempRoot))
        Directory.Delete(tempRoot, true);
}

var exportedJson = """
{
  "FileType": "ColumnPadTextExport",
  "Version": 1,
  "Columns": [
    { "Title": "Red", "Text": "left" },
    { "Title": "Blue", "Text": "right" }
  ]
}
""";
var importedFromJson = new MainViewModel();
importedFromJson.LoadFromExportJson(exportedJson, "export.json", "C:\\temp\\export.json");
Check(importedFromJson.Columns.Count == 2, "JSON import should create one column per exported entry.");
Check(importedFromJson.Columns[0].Title == "Red", "JSON import should preserve first column title.");
Check(importedFromJson.Columns[0].Text == "left", "JSON import should preserve first column body.");
Check(importedFromJson.Columns[1].Title == "Blue", "JSON import should preserve second column title.");
Check(importedFromJson.Columns[1].Text == "right", "JSON import should preserve second column body.");
Check(importedFromJson.CurrentFileKind == SaveFileKind.JsonExport && importedFromJson.RequiresSaveAsBeforeOverwrite, "Imported JSON exports should require Save As before they can overwrite the source file.");
Check(!importedFromJson.IsDirty, "Imported JSON exports should start clean.");
importedFromJson.GutterWidthPx = 36;
Check(importedFromJson.CurrentFileKind == SaveFileKind.Layout && importedFromJson.CurrentFilePath is null, "Adding settings that a concise JSON export cannot represent should promote the workspace to a native layout.");

var singleLayoutJson = vm.ToLayoutJson();
var workspaceSessionJson = JsonSerializer.Serialize(new
{
    Version = 1,
    ActiveWorkspaceIndex = 0,
    Workspaces = new[]
    {
        new
        {
            Name = "Workspace 1",
            LayoutJson = singleLayoutJson,
            LastMultiColumnCount = 3
        }
    }
});
Check(WorkspaceSessionFileService.IsWorkspaceSessionJson(workspaceSessionJson), "Session detection should recognize workspace session JSON files.");
Check(!WorkspaceSessionFileService.IsWorkspaceSessionJson(singleLayoutJson), "Session detection should not treat single-layout JSON as a workspace session file.");

Check(FileWorkflowService.ClassifyOpenFile(".txt", exportedText) == OpenFileLoadKind.TextExport, "File workflow service should classify exported text as text-export load kind.");
Check(FileWorkflowService.ClassifyOpenFile(".txt", "plain note") == OpenFileLoadKind.TextDocument, "File workflow service should classify plain text as text-document load kind.");
Check(FileWorkflowService.ClassifyOpenFile(".txt", "===== Alpha =====\n\nplain note") == OpenFileLoadKind.TextDocument, "File workflow service should not auto-split normal text files that contain divider-like lines.");
Check(FileWorkflowService.ClassifyOpenFile(".json", exportedJson) == OpenFileLoadKind.JsonExport, "File workflow service should classify concise ColumnPad JSON exports before layout detection.");
Check(FileWorkflowService.ClassifyOpenFile(".md", "# note") == OpenFileLoadKind.Unsupported, "File workflow service should reject retired Markdown file extensions.");
Check(!FileWorkflowService.SupportedOpenFileFilter.Contains("*.md", StringComparison.OrdinalIgnoreCase), "The Open dialog should no longer advertise Markdown files.");
Check(FileWorkflowService.ClassifyOpenFile(".json", workspaceSessionJson) == OpenFileLoadKind.WorkspaceSession, "File workflow service should classify workspace-session JSON correctly.");
Check(FileWorkflowService.ClassifyOpenFile(".json", singleLayoutJson) == OpenFileLoadKind.LayoutJson, "File workflow service should classify single-layout JSON as layout load kind.");
var workflowJson = JsonSerializer.Serialize(workflowDefinition);
Check(FileWorkflowService.ClassifyOpenFile(".workflow.json", workflowJson) == OpenFileLoadKind.WorkflowJson, "File workflow service should classify workflow JSON so File Open can route it to Workflow Builder.");

var saveDialogDefinition = FileWorkflowService.BuildSaveDialog(SaveFileKind.TextDocument, "C:\\temp\\notes.txt", requiresSaveAsBeforeOverwrite: true);
Check(saveDialogDefinition.FileName == "notes-copy.txt", "File workflow service should suggest copy-suffixed names when Save As is required.");
Check(saveDialogDefinition.DefaultExt == ".txt", "File workflow service should return the expected default extension for text documents.");

var layoutDialogDefinition = FileWorkflowService.BuildSaveDialog(SaveFileKind.Layout, "C:\\temp\\layout.columnpad.json", requiresSaveAsBeforeOverwrite: false);
Check(layoutDialogDefinition.FileName == "layout.columnpad.json", "File workflow service should preserve existing layout filename when direct save is allowed.");

var textExportDialogDefinition = FileWorkflowService.BuildSaveDialog(SaveFileKind.TextExport, currentFilePath: null, requiresSaveAsBeforeOverwrite: false);
Check(textExportDialogDefinition.FileName == "ColumnPad_export.txt", "File workflow service should provide a standard text export filename.");

var jsonExportDialogDefinition = FileWorkflowService.BuildSaveDialog(SaveFileKind.JsonExport, currentFilePath: null, requiresSaveAsBeforeOverwrite: false);
Check(jsonExportDialogDefinition.FileName == "ColumnPad_export.json" && jsonExportDialogDefinition.DefaultExt == ".json", "File workflow service should provide a standard concise JSON export filename.");

var workspaceSessionDialogDefinition = FileWorkflowService.BuildWorkspaceSessionSaveDialog("C:\\temp\\session.columnpad.json");
Check(workspaceSessionDialogDefinition.FileName == "session.columnpad.json", "File workflow service should use preferred workspace-session filename when available.");

Check(WorkspaceSessionFileService.TryParseSession(workspaceSessionJson, out var parsedSession), "Session service should parse valid workspace session JSON.");
Check(parsedSession.Workspaces.Count == 1 && parsedSession.Workspaces[0].Name == "Workspace 1", "Session service should preserve workspace entries when parsing.");
var roundTripSessionJson = WorkspaceSessionFileService.SerializeSession(parsedSession.Workspaces.ToList(), parsedSession.ActiveWorkspaceIndex);
Check(WorkspaceSessionFileService.IsWorkspaceSessionJson(roundTripSessionJson), "Session service should emit valid workspace session JSON.");
var roundTripSessionRoot = JsonNode.Parse(roundTripSessionJson)!.AsObject();
var roundTripSessionWorkspaces = roundTripSessionRoot["Workspaces"]!.AsArray();
var roundTripSessionWorkspace = roundTripSessionWorkspaces[0]!.AsObject();
Check(roundTripSessionRoot["FileType"]?.GetValue<string>() == "ColumnPadWorkspaceSession", "Session service should label saved session JSON for external readers.");
Check(roundTripSessionRoot["Version"]?.GetValue<int>() == 2, "Session service should save the cleaned workspace-session schema version.");
Check(roundTripSessionWorkspace["Layout"] is JsonObject, "Session service should save workspace layout as nested JSON instead of escaped JSON text.");
Check(roundTripSessionWorkspace["LayoutJson"] is null, "Session service should not emit legacy escaped LayoutJson when saving.");
Check(WorkspaceSessionFileService.TryParseSession(roundTripSessionJson, out var parsedCleanSession), "Session service should parse its cleaned session JSON.");
Check(parsedCleanSession.Workspaces[0].LayoutJson.Contains("\"Columns\"", StringComparison.Ordinal), "Cleaned session JSON should preserve the nested layout content.");
var futureSessionRoot = JsonNode.Parse(roundTripSessionJson)!.AsObject();
futureSessionRoot["Version"] = 999;
Check(!WorkspaceSessionFileService.IsWorkspaceSessionJson(futureSessionRoot.ToJsonString()), "Session detection should reject unsupported future versions.");
Check(!WorkspaceSessionFileService.TryParseSession(futureSessionRoot.ToJsonString(), out _), "Session parsing should reject unsupported future versions without loading tabs.");

var tempSessionPath = Path.Combine(Path.GetTempPath(), $"ColumnPadSession-{Guid.NewGuid():N}.columnpad.json");
File.WriteAllText(tempSessionPath, workspaceSessionJson);
try
{
    Check(WorkspaceSessionFileService.IsExistingWorkspaceSessionFile(tempSessionPath), "Session service should detect existing session files on disk.");
    var singleSessionCandidate = new List<WorkspaceSessionSaveCandidate>
    {
        new WorkspaceSessionSaveCandidate(tempSessionPath, SaveFileKind.Layout, false)
    };
    Check(WorkspaceSessionFileService.GetDirectWorkspaceSessionPath(singleSessionCandidate) == tempSessionPath, "Session service should keep a single direct session path when it is a clean layout.");
    Check(WorkspaceSessionFileService.ShouldSaveWorkspaceSession(singleSessionCandidate), "Session service should save workspace sessions when the current file is already a session file.");

    var blockedSessionCandidate = new List<WorkspaceSessionSaveCandidate>
    {
        new WorkspaceSessionSaveCandidate(tempSessionPath, SaveFileKind.Layout, true)
    };
    Check(WorkspaceSessionFileService.GetDirectWorkspaceSessionPath(blockedSessionCandidate) is null, "Session service should block direct path reuse when Save As is required.");

    var multiWorkspaceCandidates = new List<WorkspaceSessionSaveCandidate>
    {
        new WorkspaceSessionSaveCandidate(null, SaveFileKind.Layout, false),
        new WorkspaceSessionSaveCandidate(null, SaveFileKind.Layout, false)
    };
    Check(WorkspaceSessionFileService.ShouldSaveWorkspaceSession(multiWorkspaceCandidates), "Session service should force workspace-session save when multiple workspaces are open.");
    var lifecycleNames = new[] { "Workspace 1", "workspace 2", "Focus" };
    Check(WorkspaceLifecycleService.NextWorkspaceName(lifecycleNames) == "Workspace 3", "Workspace lifecycle service should generate the next unique workspace name case-insensitively.");
    Check(!WorkspaceLifecycleService.CanCloseWorkspace(1), "Workspace lifecycle service should prevent closing the last remaining workspace.");
    Check(WorkspaceLifecycleService.CanCloseWorkspace(2), "Workspace lifecycle service should allow closing when more than one workspace exists.");
    Check(WorkspaceLifecycleService.NextActiveWorkspaceIndexAfterClose(3, 3) == 2, "Workspace lifecycle service should clamp next active index after closing the last tab.");
    Check(WorkspaceLifecycleService.NextActiveWorkspaceIndexAfterClose(0, 3) == 0, "Workspace lifecycle service should preserve first index when closing the first tab.");
}
finally
{
    if (File.Exists(tempSessionPath))
        File.Delete(tempSessionPath);
}



EditorServiceSmokeTests.Run(tests);
return tests.Complete();















