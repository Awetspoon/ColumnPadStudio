using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.ViewModels;
using ColumnPadStudio.Services;
using ColumnPadStudio.Controls;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Text.Json;
using System.Text.Json.Nodes;
using ColumnPadStudio.Models;
using ColumnPadStudio.Workflows;
using System.Windows.Controls;
using System.Windows.Threading;
using ColumnPadStudio;
using ColumnPadStudio.SmokeTests;

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
Check(vm.IsDefaultThemeSelected && !vm.IsLightThemeSelected && !vm.IsDarkThemeSelected, "Default theme menu state should match the default preset.");
Check(
    vm.EditorFontSummary == $"{vm.EditorFontFamily} {vm.EditorFontStyleName} {vm.EditorFontSize:0}",
    "Editor font summary should reflect the active global editor font settings.");
Check(
    vm.EditorLanguages.Select(language => language.Tag).SequenceEqual(["en-US", "en-GB", "fr-FR", "de-DE", "es-ES", "it-IT", "pt-BR", "pt-PT", "nl-NL", "sv-SE", "da-DK", "nb-NO"]),
    "Proofing language list should keep the current supported app range.");
Check(vm.Columns.Count == 3, "Default layout should start with 3 columns.");
Check(vm.StatusText.Contains("Selected:"), "Status text should identify the selected column.");
Check(!vm.IsDirty, "New layout should start clean.");
vm.Columns[0].Title = "  Column\r\nOne\tName  ";
Check(vm.Columns[0].Title == "Column One Name", "Column titles should be normalized to a clean single-line label.");
var cleanedWorkspace = new WorkspaceSession("  Workspace\r\nAlpha\tDraft  ", vm);
Check(cleanedWorkspace.Name == "Workspace Alpha Draft", "Workspace names should be normalized to a clean single-line label.");
var cleanedWorkflow = new WorkflowDefinition { Name = "  Workflow\r\nAlpha  ", Category = "  Research\tPlan  " };
Check(cleanedWorkflow.Name == "Workflow Alpha" && cleanedWorkflow.Category == "Research Plan", "Workflow names and categories should be normalized to clean single-line labels.");
var cleanedWorkflowNode = new WorkflowDiagramNode { Kind = WorkflowNodeKind.Decision, Title = "  Choose\r\nPath  " };
Check(cleanedWorkflowNode.Title == "Choose Path", "Workflow node titles should be normalized to clean single-line labels.");

Exception? resourceLoadException = null;
Thread resourceLoadThread = new(() =>
{
    try
    {
        _ = new Application();
        var resources = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/ColumnPadStudio;component/Resources/AppResources.xaml", UriKind.Absolute)
        };

        Check(resources.MergedDictionaries.Count == 3, "App resources should stay split into theme brushes, control styles, and menu styles.");
        Check(resources["ControlPopupHighlightBrush"] is not null, "Theme brush resources should load from the app resource index.");
        Check(resources[typeof(MenuItem)] is Style, "Shared menu item style should load from the app resource index.");
        Check(resources["EmbeddedMenuPanelItemStyle"] is Style, "Embedded menu panel style should load from the app resource index.");
        Check(resources[typeof(Button)] is Style, "Shared button style should load from the app resource index.");
        Check(resources[typeof(TextBox)] is Style, "Shared textbox style should load from the app resource index.");

        Application.Current.Resources.MergedDictionaries.Add(resources);

        var styledButton = new Button { Content = "Template check" };
        styledButton.Style = (Style)resources[typeof(Button)];
        styledButton.ApplyTemplate();
        Check(styledButton.Template is not null, "Shared button style should apply without missing resource errors.");

        var styledTextBox = new TextBox { Text = "Template check" };
        styledTextBox.Style = (Style)resources[typeof(TextBox)];
        styledTextBox.ApplyTemplate();
        Check(styledTextBox.Template is not null, "Shared textbox style should apply without missing resource errors.");

        var workflowBuilderWindow = new WorkflowBuilderWindow();
        workflowBuilderWindow.ApplyTemplate();
        Check(workflowBuilderWindow.ViewModel is not null, "Workflow Builder window should initialize its view model.");
        Check(workflowBuilderWindow.Owner is null, "Workflow Builder should stay independent from the main window so minimizing ColumnPad does not minimize it.");
        Check(workflowBuilderWindow.ShowInTaskbar, "Workflow Builder should have its own taskbar entry.");
        Check(workflowBuilderWindow.WindowStartupLocation == WindowStartupLocation.CenterScreen, "Workflow Builder should open as an independent window, not as an owned child.");
        Check(workflowBuilderWindow.FindName("ExportWorkflowButton") is Button, "Workflow Builder should expose one grouped export action instead of separate export buttons.");
        workflowBuilderWindow.Close();

        var nestedMenu = new MenuItem { Header = "Column colour" };
        nestedMenu.Style = (Style)resources[typeof(MenuItem)];
        nestedMenu.Items.Add(new MenuItem { Header = "Blue" });
        nestedMenu.Items.Add(new MenuItem { Header = "Green" });

        var contextMenu = new ContextMenu();
        contextMenu.Items.Add(nestedMenu);
        contextMenu.ApplyTemplate();
        nestedMenu.ApplyTemplate();
        nestedMenu.IsSubmenuOpen = true;
        contextMenu.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        Check(nestedMenu.Template is not null, "Nested context menu items should apply the shared app menu template.");
        nestedMenu.IsSubmenuOpen = false;
    }
    catch (Exception ex)
    {
        resourceLoadException = ex;
    }
});
resourceLoadThread.SetApartmentState(ApartmentState.STA);
resourceLoadThread.Start();
resourceLoadThread.Join();
Check(resourceLoadException is null, $"App resource dictionaries should load without XAML errors: {resourceLoadException?.Message}");

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
vm.SpellCheckEnabled = false;
vm.EditorLanguageTag = "fr-FR";
Check(vm.ProofingLanguageDisplayName.Contains("French", StringComparison.OrdinalIgnoreCase), "Proofing display name should describe the selected language.");
Check(vm.StatusText.Contains("Proofing language:", StringComparison.Ordinal), "Changing proofing language should explain what changed.");
vm.ActiveColumnId = vm.Columns[1].Id;
Check(vm.IsDirty, "Changing the layout should mark the workspace dirty.");

var json = vm.ToLayoutJson();
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
Check(loaded.ActiveColumnId == loaded.Columns[1].Id, "JSON round-trip should restore the active column.");
Check(!loaded.SpellCheckEnabled, "JSON round-trip should preserve spellcheck setting.");
Check(loaded.EditorLanguageTag == "fr-FR", "JSON round-trip should preserve editor language setting.");
Check(loaded.GetActive()?.Title == vm.Columns[1].Title, "Restored active column should match the saved column.");
Check(!loaded.IsDirty, "Loaded layout should start clean.");

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

var preferencesPath = Path.Combine(Path.GetTempPath(), $"columnpad-preferences-{Guid.NewGuid():N}.json");
AppPreferencesService.Save(new AppPreferences("Dark Mode"), preferencesPath);
var loadedPreferences = AppPreferencesService.Load(preferencesPath);
Check(loadedPreferences.ThemePreset == "Dark Mode", "Saved app preferences should round-trip the selected theme.");
File.WriteAllText(preferencesPath, "{not valid json");
Check(AppPreferencesService.Load(preferencesPath).ThemePreset == "Default Mode", "Invalid app preferences should fall back to the default theme.");
File.Delete(preferencesPath);

Check(
    AppStoragePaths.CrashLogsDirectory == Path.Combine(AppStoragePaths.RootDirectory, "CrashLogs"),
    "App storage paths should expose the crash-log directory as a single source of truth.");

Check(
    typeof(MainWindow).Assembly.GetName().Name == "ColumnPadStudio",
    "The application assembly should publish with the stable ColumnPadStudio executable name.");

const string latestReleaseJson = """
    {
      "tag_name": "v2.4.0",
      "html_url": "https://github.com/Awetspoon/ColumnPadStudio/releases/tag/v2.4.0"
    }
    """;
using (var updateHttpClient = new HttpClient(new StaticJsonResponseHandler(latestReleaseJson)))
{
    var updateService = new GitHubReleaseUpdateService(updateHttpClient);
    var latestRelease = await updateService.GetLatestReleaseAsync();

    Check(latestRelease?.Version == new Version(2, 4, 0, 0), "GitHub update checks should parse release tags into comparable versions.");
    Check(latestRelease?.DisplayVersion == "v2.4.0", "GitHub update checks should keep a clean version label for the notification.");
    Check(latestRelease?.ReleasePage.AbsoluteUri == "https://github.com/Awetspoon/ColumnPadStudio/releases/tag/v2.4.0", "GitHub update checks should preserve the official HTTPS release page.");
    Check(
        latestRelease is not null && GitHubReleaseUpdateService.IsNewerRelease(latestRelease.Version, new Version(2, 3, 0, 0)),
        "GitHub update checks should detect a newer stable release.");
    Check(
        latestRelease is not null && !GitHubReleaseUpdateService.IsNewerRelease(latestRelease.Version, new Version(2, 4, 0, 0)),
        "GitHub update checks should not notify for the installed release.");
}

const string untrustedReleasePageJson = """
    {
      "tag_name": "v2.4.0",
      "html_url": "https://example.com/not-columnpad"
    }
    """;
using (var updateHttpClient = new HttpClient(new StaticJsonResponseHandler(untrustedReleasePageJson)))
{
    var updateService = new GitHubReleaseUpdateService(updateHttpClient);
    var latestRelease = await updateService.GetLatestReleaseAsync();
    Check(
        latestRelease?.ReleasePage == GitHubReleaseUpdateService.ReleasesPageUri,
        "Update links should fall back to the trusted ColumnPadStudio GitHub releases page.");
}

using (var updateHttpClient = new HttpClient(
           new StaticJsonResponseHandler("{}", System.Net.HttpStatusCode.NotFound)))
{
    var updateService = new GitHubReleaseUpdateService(updateHttpClient);
    Check(
        await updateService.GetLatestReleaseAsync() is null,
        "Update checks should quietly handle a repository with no published release.");
}

Check(
    GitHubReleaseUpdateService.TryParseReleaseVersion("v2.5.0-beta.1", out var parsedReleaseVersion) &&
    parsedReleaseVersion == new Version(2, 5, 0, 0),
    "Release version parsing should ignore semantic-version labels when comparing versions.");
Check(
    !GitHubReleaseUpdateService.TryParseReleaseVersion("latest", out _),
    "Release version parsing should reject tags that do not contain a numeric version.");

var atomicRoot = Path.Combine(Path.GetTempPath(), $"columnpad-atomic-{Guid.NewGuid():N}");
try
{
    var atomicPath = Path.Combine(atomicRoot, "nested", "note.txt");
    AtomicFileWriter.WriteText(atomicPath, "first");
    Check(File.ReadAllText(atomicPath) == "first", "Atomic writer should create missing target directories.");
    AtomicFileWriter.WriteText(atomicPath, "second");
    Check(File.ReadAllText(atomicPath) == "second", "Atomic writer should replace existing files cleanly.");
    Check(Directory.GetFiles(Path.GetDirectoryName(atomicPath)!, "*.tmp").Length == 0, "Atomic writer should clean up temporary files after a successful write.");
}
finally
{
    if (Directory.Exists(atomicRoot))
        Directory.Delete(atomicRoot, recursive: true);
}

var workflowTemp = Path.Combine(Path.GetTempPath(), $"columnpad-workflows-{Guid.NewGuid():N}");
var workflowService = new WorkflowService(workflowTemp);
var emptyWorkflowVm = new WorkflowBuilderViewModel(workflowService);
emptyWorkflowVm.Load();
Check(emptyWorkflowVm.Workflows.Count == 1, "Workflow Builder should create one workflow when no saved workflows exist.");
Check(WorkflowTemplateCatalog.Templates.Count >= 10, "Workflow starter catalog should provide multiple practical starters.");
var workflowTemplateIds = WorkflowTemplateCatalog.Templates.Select(template => template.Id).ToList();
Check(workflowTemplateIds.Count == workflowTemplateIds.Distinct(StringComparer.OrdinalIgnoreCase).Count(), "Workflow starter catalog should not contain duplicate IDs.");
Check(WorkflowTemplateCatalog.Templates.All(template => template.Nodes.Count > 0), "Workflow starter catalog should not contain empty starter diagrams.");
Check(WorkflowTemplateCatalog.Templates.All(template => template.Connections.Count > 0), "Workflow starter catalog should wire starter nodes together.");
var essayStarter = WorkflowTemplateCatalog.Templates.FirstOrDefault(template => template.Id == "essay-plan");
Check(essayStarter is not null, "Workflow starter catalog should include an essay planning starter.");
if (essayStarter is not null)
{
    var essayWorkflow = essayStarter.CreateWorkflowInstance("Essay Plan Copy");
    Check(essayWorkflow.Name == "Essay Plan Copy", "Workflow starter instances should allow a custom workflow name.");
    Check(essayWorkflow.Nodes.Count >= 5, "Workflow starter instances should create a useful editable diagram.");
    Check(essayWorkflow.Links.Count > 0, "Workflow starter instances should create connections between starter nodes.");
    var thesisNode = essayWorkflow.Nodes.FirstOrDefault(node => node.Title == "Define thesis");
    Check(!string.IsNullOrWhiteSpace(thesisNode?.Goal), "Workflow starter nodes should include a real goal, not just a box title.");
    Check(thesisNode?.ChecklistItems.Count >= 2, "Workflow starter nodes should include useful checklist data.");
}

var workflowBuilderVm = new WorkflowBuilderViewModel(workflowService);
workflowBuilderVm.AddWorkflow();
workflowBuilderVm.AddNode(WorkflowNodeKind.Decision);
Check(workflowBuilderVm.SelectedNode?.Kind == WorkflowNodeKind.Decision, "Workflow builder palette should add the requested node kind.");
workflowBuilderVm.SelectedNode!.X = 1260;
workflowBuilderVm.SelectedNode.Width = 220;
Check(workflowBuilderVm.DiagramCanvasWidth >= 1576, "Workflow builder canvas should expand to include far-right nodes.");
workflowBuilderVm.SelectedNode.Y = 780;
workflowBuilderVm.SelectedNode.Height = 120;
Check(workflowBuilderVm.DiagramCanvasHeight >= 996, "Workflow builder canvas should expand to include lower nodes.");

var workflowDefinition = new WorkflowDefinition { Name = "Colour test" };
workflowDefinition.Id = "  workflow id with spaces  ";
Check(workflowDefinition.Id == "workflow id with spaces", "Workflow IDs should trim outer whitespace without applying display-label cleanup.");
workflowDefinition.Nodes.Add(new WorkflowDiagramNode
{
    Id = " start ",
    Kind = WorkflowNodeKind.Start,
    Title = "Start",
    Color = WorkflowNodeColor.Rose,
    Goal = "Round-trip goal",
    Instructions = "Round-trip instructions",
    ExpectedOutput = "Round-trip output",
    ChecklistItems = new ObservableCollection<WorkflowChecklistItem>
    {
        new() { Text = "First check" },
        new() { Text = "Done check", IsDone = true }
    }
});
workflowDefinition.Nodes.Add(new WorkflowDiagramNode { Id = "end", Kind = WorkflowNodeKind.End, Title = "End", Color = WorkflowNodeColor.Green });
Check(workflowDefinition.Nodes[0].Id == "start", "Workflow node IDs should use identity cleanup, not display-label cleanup.");
workflowDefinition.Links.Add(new WorkflowDiagramLink { FromNodeId = "start", ToNodeId = "end" });
workflowService.Save(workflowDefinition);
Check(!string.IsNullOrWhiteSpace(workflowDefinition.FilePath), "Workflow save should assign a file path.");
Check(workflowService.TryLoad(workflowDefinition.FilePath!, out var loadedWorkflow), "Workflow service should reload saved workflow JSON.");
Check(loadedWorkflow.SchemaVersion >= 3, "Workflow service should normalize saved workflows to the current schema.");
Check(loadedWorkflow.Nodes[0].Color == WorkflowNodeColor.Rose, "Workflow node colour should persist through JSON save/load.");
Check(loadedWorkflow.Nodes[0].Goal == "Round-trip goal", "Workflow node goal should persist through JSON save/load.");
Check(loadedWorkflow.Nodes[0].Instructions == "Round-trip instructions", "Workflow node instructions should persist through JSON save/load.");
Check(loadedWorkflow.Nodes[0].ExpectedOutput == "Round-trip output", "Workflow node expected output should persist through JSON save/load.");
Check(loadedWorkflow.Nodes[0].ChecklistItems.Count == 2 && loadedWorkflow.Nodes[0].ChecklistItems[1].IsDone, "Workflow node checklist data should persist through JSON save/load.");
var readableWorkflowText = workflowService.BuildTextExport(workflowDefinition);
Check(readableWorkflowText.StartsWith(WorkflowService.TextExportMarker), "Workflow text export should include a clear ColumnPad marker.");
Check(readableWorkflowText.Contains("Workflow: Colour test"), "Workflow text export should include the workflow name.");
Check(readableWorkflowText.Contains("1. [Start] Start"), "Workflow text export should list readable node steps.");
Check(readableWorkflowText.Contains("Round-trip goal"), "Workflow text export should include node goals.");
Check(readableWorkflowText.Contains("- [x] Done check"), "Workflow text export should include checklist completion state.");
Check(readableWorkflowText.Contains("1. Start -> 2. End"), "Workflow text export should show connections using node names.");
var readableWorkflowMarkdown = workflowService.BuildMarkdownExport(workflowDefinition);
Check(readableWorkflowMarkdown.StartsWith(WorkflowService.MarkdownExportMarker), "Workflow markdown export should include a clear ColumnPad marker.");
Check(readableWorkflowMarkdown.Contains("# Colour test"), "Workflow markdown export should include the workflow name as a heading.");
Check(readableWorkflowMarkdown.Contains("### 1. Start: Start"), "Workflow markdown export should list readable node steps.");
Check(readableWorkflowMarkdown.Contains("Round-trip instructions"), "Workflow markdown export should include node instructions.");
Check(readableWorkflowMarkdown.Contains("- [x] Done check"), "Workflow markdown export should include checklist completion state.");
var readableWorkflowTextPath = Path.Combine(workflowTemp, "colour-test.workflow.txt");
workflowService.ExportTextToPath(workflowDefinition, readableWorkflowTextPath);
Check(File.Exists(readableWorkflowTextPath), "Workflow text export should write a text file.");
var readableWorkflowMarkdownPath = Path.Combine(workflowTemp, "colour-test.workflow.md");
workflowService.ExportMarkdownToPath(workflowDefinition, readableWorkflowMarkdownPath);
Check(File.Exists(readableWorkflowMarkdownPath), "Workflow markdown export should write a markdown file.");
var existingWorkflowVm = new WorkflowBuilderViewModel(workflowService);
existingWorkflowVm.Load();
var workflowCountBeforeAdd = existingWorkflowVm.Workflows.Count;
existingWorkflowVm.AddWorkflow();
Check(existingWorkflowVm.Workflows.Count == workflowCountBeforeAdd + 1, "Workflow Builder Add Workflow should add one workflow.");

Check(!WorkflowService.IsWorkflowDefinitionJson("{}"), "Workflow detection should reject unrelated empty JSON objects.");
Check(!WorkflowService.IsWorkflowDefinitionJson(json), "Workflow detection should reject ColumnPad layout JSON.");
var camelCaseWorkflowPath = Path.Combine(workflowTemp, "camel-case.workflow.json");
File.WriteAllText(camelCaseWorkflowPath, """
{
  "fileType": "ColumnPadWorkflow",
  "schemaVersion": 3,
  "id": "camel-case",
  "name": "Camel Case Workflow",
  "nodes": [
    { "id": "start", "kind": "Start", "title": "Start" }
  ],
  "links": []
}
""");
Check(workflowService.TryLoad(camelCaseWorkflowPath, out var camelCaseWorkflow), "Workflow import should accept case-insensitive property names and readable enum names.");
Check(camelCaseWorkflow.Nodes.Count == 1 && camelCaseWorkflow.Nodes[0].Kind == WorkflowNodeKind.Start, "Case-insensitive workflow import should preserve node data.");

var invalidWorkflowPath = Path.Combine(workflowTemp, "invalid.workflow.json");
File.WriteAllText(invalidWorkflowPath, "{}");
_ = workflowService.LoadAll();
Check(workflowService.LastLoadWarnings.Contains("invalid.workflow.json"), "Workflow library loading should report unreadable workflow filenames instead of silently skipping them.");

var dirtyWorkflowService = new WorkflowService(Path.Combine(workflowTemp, "dirty-state"));
var dirtyWorkflowVm = new WorkflowBuilderViewModel(dirtyWorkflowService);
dirtyWorkflowVm.Load();
Check(!dirtyWorkflowVm.HasUnsavedChanges, "Opening an empty Workflow Builder should not treat its untouched blank draft as a user edit.");
dirtyWorkflowVm.SelectedWorkflow!.Name = "My Workflow";
Check(dirtyWorkflowVm.HasUnsavedChanges, "Editing the blank workflow draft should mark it unsaved.");
dirtyWorkflowVm.SaveSelectedWorkflow();
Check(!dirtyWorkflowVm.HasUnsavedChanges, "Saving a workflow should establish a clean state.");
dirtyWorkflowVm.SelectedWorkflow!.Description = "Changed after save";
Check(dirtyWorkflowVm.HasUnsavedChanges, "Editing workflow details should mark the Workflow Builder dirty.");
Check(dirtyWorkflowVm.SaveAllChangedWorkflows() == 1, "Save-all should save each changed workflow once.");
Check(!dirtyWorkflowVm.HasUnsavedChanges, "Save-all should clear the Workflow Builder dirty state.");
Directory.Delete(workflowTemp, recursive: true);


var legacyNode = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Could not parse round-trip JSON for legacy normalization test.");
legacyNode["Version"] = 11;
var legacyColumns = legacyNode["Columns"]?.AsArray() ?? throw new InvalidOperationException("Could not find columns array for legacy normalization test.");
var legacyFirstColumn = legacyColumns[0]?.AsObject() ?? throw new InvalidOperationException("Could not find first column for legacy normalization test.");
legacyFirstColumn["Text"] = "line one\\r\\nline two\\nline three";
var legacyLoaded = new MainViewModel();
Check(legacyLoaded.LoadFromJson(legacyNode.ToJsonString(), "legacy"), "Legacy-escaped layout JSON should still load.");
Check(legacyLoaded.Columns[0].Text == "line one\nline two\nline three", "Legacy-escaped newline sequences should be decoded into real line breaks during load.");
legacyFirstColumn["Text"] = "pitch idea -> break it down -> lock it -> structure tree -> .sln -> build in sections";
Check(legacyLoaded.LoadFromJson(legacyNode.ToJsonString(), "legacy"), "Legacy inline layout JSON should still load.");
Check(legacyLoaded.Columns[0].Text.Contains("\n"), "Legacy inline text should be migrated into hard line breaks during load.");
legacyFirstColumn["Text"] = "- [ ] first task\n- [x] done task";
legacyFirstColumn["LineMarkerMode"] = null;
legacyFirstColumn["CheckedChecklistLineIndexes"] = null;
Check(legacyLoaded.LoadFromJson(legacyNode.ToJsonString(), "legacy"), "Legacy inline checklist-marker layouts should still load.");
Check(legacyLoaded.Columns[0].LineMarkerMode == LineMarkerMode.Checklist, "Legacy checklist-marker text should migrate to checklist gutter mode.");
Check(legacyLoaded.Columns[0].Text == "first task\ndone task", "Legacy checklist-marker text should decode to clean plain text.");
Check(legacyLoaded.Columns[0].IsChecklistLineChecked(1), "Legacy checklist-marker migration should restore checked rows in gutter metadata.");
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
Check(ClipboardTextService.CountLineBreaks("first\rsecond\r\nthird\nfourth") == 3, "Clipboard line-break counting should handle LF, CRLF, and standalone CR consistently.");

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
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value - ColumnViewModel.VisibleLineNumberColumnWidth) < 0.001), "Line-number gutter should default to visible width.");
lineToggleVm.ShowLineNumbers = false;
Check(lineToggleVm.Columns.All(c => c.ShowLineNumbersVisibility == Visibility.Collapsed), "Turning line numbers off should collapse line-number visibility for all columns.");
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value) < 0.001), "Turning line numbers off should collapse gutter width for all columns.");
lineToggleVm.ShowLineNumbers = true;
Check(lineToggleVm.Columns.All(c => c.ShowLineNumbersVisibility == Visibility.Visible), "Turning line numbers back on should restore line-number visibility for all columns.");
Check(lineToggleVm.Columns.All(c => c.LineNumberColumnWidth.IsAbsolute && Math.Abs(c.LineNumberColumnWidth.Value - ColumnViewModel.VisibleLineNumberColumnWidth) < 0.001), "Turning line numbers back on should restore gutter width for all columns.");

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
Check(cleanTextExport == "ColumnPad Export\nFormat: Text\n\n===== Alpha Plan =====\n\none\ntwo\n\n===== Beta =====\n\nthree\n", "Text export should use a clear marker and readable sections without trailing blank blocks.");
Check(!cleanTextExport.Contains("\\n", StringComparison.Ordinal), "Text export should write real line breaks, not escaped JSON-style line breaks.");
var cleanMarkdownExport = cleanExportVm.BuildExportMarkdown().Replace("\r\n", "\n", StringComparison.Ordinal);
Check(cleanMarkdownExport == "<!-- ColumnPad Export: Markdown -->\n\n## Alpha Plan\n\none\ntwo\n\n## Beta\n\nthree\n", "Markdown export should stay available with a clear marker and readable sections.");

var exportedText = "ColumnPad Export\nFormat: Text\n\n===== Alpha =====\n\none\n\n===== Beta =====\n\n.\n";
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

var exportedMarkdown = "<!-- ColumnPad Export: Markdown -->\n\n## Red\n\nleft\n\n## Blue\n\nright\n";
var importedFromMarkdown = new MainViewModel();
importedFromMarkdown.LoadFromExportMarkdown(exportedMarkdown, "export.md");
Check(importedFromMarkdown.Columns.Count == 2, "Markdown import should create one column per heading.");
Check(importedFromMarkdown.Columns[0].Title == "Red", "Markdown import should preserve first heading title.");
Check(importedFromMarkdown.Columns[0].Text == "left", "Markdown import should preserve first heading body.");
Check(importedFromMarkdown.Columns[1].Title == "Blue", "Markdown import should preserve second heading title.");
Check(importedFromMarkdown.Columns[1].Text == "right", "Markdown import should preserve second heading body.");
Check(!importedFromMarkdown.IsDirty, "Imported markdown exports should start clean.");

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
Check(FileWorkflowService.ClassifyOpenFile(".md", exportedMarkdown) == OpenFileLoadKind.MarkdownExport, "File workflow service should classify exported markdown as markdown-export load kind.");
Check(FileWorkflowService.ClassifyOpenFile(".md", "# note") == OpenFileLoadKind.MarkdownDocument, "File workflow service should classify plain markdown as markdown-document load kind.");
Check(FileWorkflowService.ClassifyOpenFile(".md", "## Heading\n\nplain note") == OpenFileLoadKind.MarkdownDocument, "File workflow service should not auto-split normal markdown heading files.");
Check(FileWorkflowService.ClassifyOpenFile(".json", workspaceSessionJson) == OpenFileLoadKind.WorkspaceSession, "File workflow service should classify workspace-session JSON correctly.");
Check(FileWorkflowService.ClassifyOpenFile(".json", singleLayoutJson) == OpenFileLoadKind.LayoutJson, "File workflow service should classify single-layout JSON as layout load kind.");
var workflowJson = JsonSerializer.Serialize(workflowDefinition, new JsonSerializerOptions { WriteIndented = true });
Check(FileWorkflowService.ClassifyOpenFile(".workflow.json", workflowJson) == OpenFileLoadKind.WorkflowJson, "File workflow service should classify workflow JSON so File Open can route it to Workflow Builder.");

var saveDialogDefinition = FileWorkflowService.BuildSaveDialog(SaveFileKind.TextDocument, "C:\\temp\\notes.txt", requiresSaveAsBeforeOverwrite: true);
Check(saveDialogDefinition.FileName == "notes-copy.txt", "File workflow service should suggest copy-suffixed names when Save As is required.");
Check(saveDialogDefinition.DefaultExt == ".txt", "File workflow service should return the expected default extension for text documents.");

var layoutDialogDefinition = FileWorkflowService.BuildSaveDialog(SaveFileKind.Layout, "C:\\temp\\layout.columnpad.json", requiresSaveAsBeforeOverwrite: false);
Check(layoutDialogDefinition.FileName == "layout.columnpad.json", "File workflow service should preserve existing layout filename when direct save is allowed.");

var textExportDialogDefinition = FileWorkflowService.BuildSaveDialog(SaveFileKind.TextExport, currentFilePath: null, requiresSaveAsBeforeOverwrite: false);
Check(textExportDialogDefinition.FileName == "ColumnPad_export.txt", "File workflow service should provide a standard text export filename.");

var markdownExportDialogDefinition = FileWorkflowService.BuildSaveDialog(SaveFileKind.MarkdownExport, currentFilePath: null, requiresSaveAsBeforeOverwrite: false);
Check(markdownExportDialogDefinition.FileName == "ColumnPad_export.md", "File workflow service should provide a standard markdown export filename.");

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



var searchColumns = new List<string?>
{
    "alpha beta",
    "gamma\nalpha",
    string.Empty
};

Check(TextSearchService.TryFindNext(searchColumns, "alpha", 0, 0, 0, SearchCursor.Empty, out var firstFind), "Text search service should find the first match from the active column.");
Check(firstFind.ColumnIndex == 0 && firstFind.CharIndex == 0 && firstFind.LineNumber == 1, "Text search service should report first-column hit coordinates.");
Check(TextSearchService.TryFindNext(searchColumns, "alpha", 0, 0, 0, new SearchCursor(firstFind.ColumnIndex, firstFind.CharIndex), out var secondFind), "Text search service should advance to the next match after the cursor.");
Check(secondFind.ColumnIndex == 1 && secondFind.CharIndex == 6 && secondFind.LineNumber == 2, "Text search service should report line/char for cross-column next hit.");
Check(TextSearchService.TryFindNext(searchColumns, "alpha", 0, 0, 0, new SearchCursor(secondFind.ColumnIndex, secondFind.CharIndex), out var wrappedFind), "Text search service should wrap when searching past the last match.");
Check(wrappedFind.ColumnIndex == 0 && wrappedFind.CharIndex == 0, "Text search service wrap search should return to the first match.");
Check(!TextSearchService.TryFindNext(searchColumns, "missing", 0, 0, 0, SearchCursor.Empty, out _), "Text search service should return no hit when the term is absent.");

var (replacedTextByService, replacementCountByService) = TextSearchService.ReplaceAllWithCount("one One one", "one", "two", StringComparison.CurrentCultureIgnoreCase);
Check(replacementCountByService == 3, "Text search service replace should count all case-insensitive hits.");
Check(replacedTextByService == "two two two", "Text search service replace should substitute all hits in order.");
Check(TextSearchService.ComputeLineNumber("a\nb\nc", 4) == 3, "Text search service should compute 1-based line numbers from character index.");
Check(TextSearchService.ComputeLineNumber("a\rb\r\nc", 5) == 3, "Text search service should count LF, CRLF, and standalone CR line breaks consistently.");

var listModeVm = new ColumnViewModel
{
    Text = "alpha\nbeta",
    LineMarkerMode = LineMarkerMode.Bullets
};
Check(listModeVm.LineMarkerMode == LineMarkerMode.Bullets, "Line marker mode should support bullets without mutating text.");
listModeVm.LineMarkerMode = LineMarkerMode.Checklist;
listModeVm.ToggleChecklistLineChecked(0);
Check(listModeVm.IsChecklistLineChecked(0), "Checklist gutter mode should toggle checks without inserting inline symbols.");
Check(listModeVm.Text == "alpha\nbeta", "Checklist gutter mode should keep body text unchanged.");

var expectedClipboardLines = string.Join(Environment.NewLine, "one", "two", "three");
Check(
    ClipboardTextService.NormalizeClipboardText("one\r\r\ntwo\u2028three") == expectedClipboardLines,
    "Clipboard text normalization should collapse malformed CRCRLF and Unicode line separators.");

var alternatingBlankPaste = "one\n\n two\n\nthree\n\nfour";
Check(
    ClipboardTextService.NormalizeClipboardText(alternatingBlankPaste) == string.Join(Environment.NewLine, "one", " two", "three", "four"),
    "Clipboard text normalization should collapse alternating blank rows from malformed paste sources.");

Check(
    ClipboardTextService.ApplyPastePreset("alpha\n  beta", PasteListPreset.Bullets) == string.Join(Environment.NewLine, "- alpha", "  - beta"),
    "Clipboard bullet preset should add markdown bullets while preserving indentation.");

Check(
    ClipboardTextService.ApplyPastePreset("- [x] done\nplain", PasteListPreset.Checklist) == string.Join(Environment.NewLine, "- [x] done", "- [ ] plain"),
    "Clipboard checklist preset should preserve checked checklist rows and add unchecked markers to plain rows.");

Check(
    ClipboardTextService.ApplyPastePreset("1. ordered", PasteListPreset.Bullets) == "1. ordered",
    "Clipboard paste presets should not rewrite ordered-list prefixes.");

if (failures.Count > 0)
{
    Console.Error.WriteLine($"Smoke tests failed: {failures.Count} of {checks} checks.");
    foreach (var failure in failures)
        Console.Error.WriteLine($" - {failure}");
    return 1;
}

Console.WriteLine($"Smoke tests passed ({checks} checks).");
return 0;















