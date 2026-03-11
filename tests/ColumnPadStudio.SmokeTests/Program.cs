using ColumnPadStudio.ViewModels;
using ColumnPadStudio.Services;
using ColumnPadStudio.Controls;
using System.Reflection;
using System.IO;
using System.Windows;
using System.Text.Json;

var failures = new List<string>();
var checks = 0;

void Check(bool condition, string message)
{
    checks++;
    if (!condition)
        failures.Add(message);
}

var vm = new MainViewModel();

Check(vm.ThemePreset == "Default Mode", "Default theme should be 'Default Mode'.");
Check(vm.Columns.Count == 3, "Default layout should start with 3 columns.");
Check(vm.StatusText.Contains("Selected:"), "Status text should identify the selected column.");
Check(!vm.IsDirty, "New layout should start clean.");

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

vm.ThemePreset = "High Contrast";
Check(vm.ThemePreset == "Dark Mode", "Legacy theme 'High Contrast' should normalize to 'Dark Mode'.");
Check(vm.LockActiveWidthActionLabel == "_Freeze Selected Column Width", "Unlocked selected column should advertise the freeze-width action.");
vm.ToggleLockActiveWidth();
Check(vm.LockActiveWidthActionLabel == "_Allow Selected Column Width to Resize", "Locked selected column should advertise the allow-resize action.");
vm.ToggleLockActiveWidth();
vm.Columns[0].PastePreset = PasteListPreset.Checklist;
vm.Columns[0].WidthPx = 444;
vm.Columns[0].EditorFontFamily = "Consolas";
vm.Columns[0].EditorFontSize = 17;
vm.Columns[0].EditorFontStyle = FontStyles.Italic;
vm.Columns[0].EditorFontWeight = FontWeights.Bold;
vm.Columns[0].UseDefaultFont = false;
vm.SpellCheckEnabled = false;
vm.EditorLanguageTag = "fr-FR";
vm.ActiveColumnId = vm.Columns[1].Id;
Check(vm.IsDirty, "Changing the layout should mark the workspace dirty.");

var json = vm.ToLayoutJson();
var loaded = new MainViewModel();
loaded.LoadFromJson(json, "smoke");

Check(loaded.Columns.Count == vm.Columns.Count, "JSON round-trip should preserve column count.");
Check(loaded.ThemePreset == vm.ThemePreset, "JSON round-trip should preserve theme preset.");
Check(loaded.Columns[0].PastePreset == PasteListPreset.Checklist, "JSON round-trip should preserve paste preset.");
Check(loaded.Columns[0].WidthPx == 444, "JSON round-trip should preserve per-column width.");
Check(!loaded.Columns[0].UseDefaultFont, "JSON round-trip should preserve per-column default-font toggle.");
Check(loaded.Columns[0].EditorFontFamily == "Consolas", "JSON round-trip should preserve per-column font family.");
Check(Math.Abs(loaded.Columns[0].EditorFontSize - 17) < 0.001, "JSON round-trip should preserve per-column font size.");
Check(loaded.Columns[0].EditorFontStyle == FontStyles.Italic, "JSON round-trip should preserve per-column font style.");
Check(loaded.Columns[0].EditorFontWeight == FontWeights.Bold, "JSON round-trip should preserve per-column font weight.");
Check(loaded.ActiveColumnId == loaded.Columns[1].Id, "JSON round-trip should restore the active column.");
Check(!loaded.SpellCheckEnabled, "JSON round-trip should preserve spellcheck setting.");
Check(loaded.EditorLanguageTag == "fr-FR", "JSON round-trip should preserve editor language setting.");
Check(loaded.GetActive()?.Title == vm.Columns[1].Title, "Restored active column should match the saved column.");
Check(!loaded.IsDirty, "Loaded layout should start clean.");

var preserveTheme = new MainViewModel();
preserveTheme.ThemePreset = "Dark Mode";
preserveTheme.LoadFromJson(json, "smoke", preserveCurrentTheme: true);
Check(preserveTheme.ThemePreset == "Dark Mode", "Manual layout open should preserve the current theme.");

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

var beforeInvalidLoadCount = loaded.Columns.Count;
loaded.LoadFromJson("{ not valid json", "smoke");
Check(loaded.StatusText == "Invalid layout file.", "Invalid JSON should report an invalid layout status.");
Check(loaded.Columns.Count == beforeInvalidLoadCount, "Invalid JSON should not mutate existing column state.");

var metrics = new ColumnViewModel
{
    Text = "alpha\n\u2610 first\n\u2611 second\n- [ ] third\n- [x] fourth"
};
Check(metrics.ChecklistTotal == 4, "ChecklistTotal should count symbol and markdown checklist items.");
Check(metrics.ChecklistDone == 2, "ChecklistDone should count checked symbol and markdown items.");

var indentedChecklistMetrics = new ColumnViewModel
{
    Text = "  - [ ] nested task\n    \u2611 done task"
};
Check(indentedChecklistMetrics.ChecklistTotal == 2, "ChecklistTotal should include indented checklist markers.");
Check(indentedChecklistMetrics.ChecklistDone == 1, "ChecklistDone should include indented checked markers.");

var lineToggleVm = new MainViewModel();
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value - 56) < 0.001), "Line-number gutter should default to visible width.");
lineToggleVm.ShowLineNumbers = false;
Check(lineToggleVm.Columns.All(c => c.ShowLineNumbersVisibility == Visibility.Collapsed), "Turning line numbers off should collapse line-number visibility for all columns.");
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value) < 0.001), "Turning line numbers off should collapse gutter width for all columns.");
lineToggleVm.ShowLineNumbers = true;
Check(lineToggleVm.Columns.All(c => c.ShowLineNumbersVisibility == Visibility.Visible), "Turning line numbers back on should restore line-number visibility for all columns.");
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value - 56) < 0.001), "Turning line numbers back on should restore gutter width for all columns.");

var liveStatusVm = new MainViewModel();
liveStatusVm.Columns[0].Title = "Inbox";
Check(liveStatusVm.StatusText.Contains("Selected: Inbox"), "Status text should refresh when the active column is renamed.");
liveStatusVm.Columns[0].Text = "\u2610 one\n\u2611 two";
Check(liveStatusVm.StatusText.Contains("Done: 1/2"), "Status text should refresh when active-column checklist progress changes.");

var exportedText = "===== Alpha =====\n\none\n\n===== Beta =====\n\n.\n";
var importedFromText = new MainViewModel();
importedFromText.LoadFromExportText(exportedText, "export.txt");
Check(importedFromText.Columns.Count == 2, "Text import should create one column per export section.");
Check(importedFromText.Columns[0].Title == "Alpha", "Text import should preserve first column title.");
Check(importedFromText.Columns[0].Text == "one", "Text import should preserve first column body.");
Check(importedFromText.Columns[1].Title == "Beta", "Text import should preserve second column title.");
Check(importedFromText.Columns[1].Text == ".", "Text import should preserve second column body.");
Check(!importedFromText.IsDirty, "Imported text exports should start clean.");

var tempRoot = Path.Combine(Path.GetTempPath(), $"ColumnPadStudioSmoke-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);
try
{
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
        new WorkspaceRecoveryWorkspace("Workspace A", vm.ToLayoutJson(), tempTextPath, SaveFileKind.TextDocument, true, true),
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

    var recoveredWorkspaceVm = new MainViewModel();
    Check(recoveredWorkspaceVm.LoadRecoverySnapshot(recoverySnapshot.Workspaces[0]), "Recovery load should accept a saved workspace snapshot.");
    Check(recoveredWorkspaceVm.CurrentFileKind == SaveFileKind.TextDocument, "Recovered workspace should restore its file kind.");
    Check(recoveredWorkspaceVm.CurrentFilePath == tempTextPath, "Recovered workspace should restore its file path.");
    Check(recoveredWorkspaceVm.RequiresSaveAsBeforeOverwrite, "Recovered workspace should restore Save As requirements.");
    Check(recoveredWorkspaceVm.IsDirty, "Recovered dirty workspace should still be dirty.");
    Check(recoveredWorkspaceVm.Columns.Count == vm.Columns.Count, "Recovered workspace should restore its layout content.");

    WorkspaceRecoveryStore.Save([recoveryWorkspaces[0]], 0, recoveryRoot);
    Check(WorkspaceRecoveryStore.TryLoad(out var trimmedRecoverySnapshot, recoveryRoot), "Recovery store should still load after shrinking the workspace list.");
    Check(trimmedRecoverySnapshot.Workspaces.Count == 1, "Recovery store should drop stale workspaces when fewer tabs are saved.");
    Check(!File.Exists(Path.Combine(recoveryRoot, "workspace-2.columnpad.json")), "Recovery store should delete stale per-workspace files.");

    WorkspaceRecoveryStore.Clear(recoveryRoot);
    Check(!Directory.Exists(recoveryRoot), "Recovery clear should remove the recovery directory.");
}
finally
{
    if (Directory.Exists(tempRoot))
        Directory.Delete(tempRoot, true);
}

var exportedMarkdown = "## Red\n\nleft\n\n## Blue\n\nright\n";
var importedFromMarkdown = new MainViewModel();
importedFromMarkdown.LoadFromExportMarkdown(exportedMarkdown, "export.md");
Check(importedFromMarkdown.Columns.Count == 2, "Markdown import should create one column per heading.");
Check(importedFromMarkdown.Columns[0].Title == "Red", "Markdown import should preserve first heading title.");
Check(importedFromMarkdown.Columns[0].Text == "left", "Markdown import should preserve first heading body.");
Check(importedFromMarkdown.Columns[1].Title == "Blue", "Markdown import should preserve second heading title.");
Check(importedFromMarkdown.Columns[1].Text == "right", "Markdown import should preserve second heading body.");
Check(!importedFromMarkdown.IsDirty, "Imported markdown exports should start clean.");

var looksLikeTextExport = typeof(ColumnPadStudio.MainWindow).GetMethod("LooksLikeTextExport", BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("Could not find text-export detection helper.");
Check((bool)(looksLikeTextExport.Invoke(null, [exportedText]) ?? false), "Open-file detection should recognize exported text layouts.");
Check(!(bool)(looksLikeTextExport.Invoke(null, ["plain note\nline two"]) ?? true), "Open-file detection should not misclassify plain text documents as exports.");

var looksLikeMarkdownExport = typeof(ColumnPadStudio.MainWindow).GetMethod("LooksLikeMarkdownExport", BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("Could not find markdown-export detection helper.");
Check((bool)(looksLikeMarkdownExport.Invoke(null, [exportedMarkdown]) ?? false), "Open-file detection should recognize exported markdown layouts.");
Check(!(bool)(looksLikeMarkdownExport.Invoke(null, ["intro paragraph\n## later heading"]) ?? true), "Open-file detection should require exported markdown heading structure.");

var isWorkspaceSessionJson = typeof(ColumnPadStudio.MainWindow).GetMethod("IsWorkspaceSessionJson", BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("Could not find workspace-session detection helper.");
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
Check((bool)(isWorkspaceSessionJson.Invoke(null, [workspaceSessionJson]) ?? false), "Session detection should recognize workspace session JSON files.");
Check(!(bool)(isWorkspaceSessionJson.Invoke(null, [singleLayoutJson]) ?? true), "Session detection should not treat single-layout JSON as a workspace session file.");



var transformBullets = typeof(ColumnEditorControl).GetMethod("TransformBullets", BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("Could not find bullet transform helper.");
var bulletFormatting = (List<string>?)transformBullets.Invoke(null, [new List<string> { "alpha", "", "beta" }])
    ?? throw new InvalidOperationException("Bullet transform returned null.");
Check(string.Join("\n", bulletFormatting) == "\u2022 alpha\n\n\u2022 beta", "Applying bullets to mixed content should leave blank separator lines blank.");

var transformChecklist = typeof(ColumnEditorControl).GetMethod("TransformChecklist", BindingFlags.Static | BindingFlags.NonPublic)
    ?? throw new InvalidOperationException("Could not find checklist transform helper.");
var checklistFormatting = (List<string>?)transformChecklist.Invoke(null, [new List<string> { "- [ ] one", "", "- [x] two" }])
    ?? throw new InvalidOperationException("Checklist transform returned null.");
Check(string.Join("\n", checklistFormatting) == "one\n\ntwo", "Toggling checklist formatting off should leave blank separator lines untouched.");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Smoke tests failed: {failures.Count} of {checks} checks.");
    foreach (var failure in failures)
        Console.Error.WriteLine($" - {failure}");
    return 1;
}

Console.WriteLine($"Smoke tests passed ({checks} checks).");
return 0;




