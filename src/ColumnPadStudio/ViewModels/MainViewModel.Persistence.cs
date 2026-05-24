using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ColumnPadStudio.Services;
using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Domain.Workspaces;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    public string BuildExportText()
    {
        var sb = new StringBuilder();
        foreach (var c in Columns)
        {
            sb.Append("===== ").Append(c.Title).AppendLine(" =====");
            sb.AppendLine(c.Text ?? string.Empty);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public string BuildExportMarkdown()
    {
        var sb = new StringBuilder();
        foreach (var c in Columns)
        {
            sb.Append("## ").AppendLine(c.Title);
            sb.AppendLine();
            sb.AppendLine(c.Text ?? string.Empty);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public string BuildSingleDocumentText()
    {
        return Columns.FirstOrDefault()?.Text ?? string.Empty;
    }

    public void LoadTextDocument(string text, string? sourceLabel = null, string? sourcePath = null, SaveFileKind kind = SaveFileKind.TextDocument)
    {
        Columns.Clear();

        var document = MakeColumn(BuildDocumentTitle(sourceLabel));
        document.Text = text ?? string.Empty;
        document.WidthPx = null;
        document.IsWidthLocked = false;
        document.PastePreset = PasteListPreset.None;
        document.EditorFontFamily = EditorFontFamily;
        document.EditorFontSize = EditorFontSize;
        document.EditorFontStyle = _editorFontStyle;
        document.EditorFontWeight = _editorFontWeight;
        document.UseDefaultFont = true;
        Columns.Add(document);

        ActiveColumnId = document.Id;
        SetCurrentFileReference(sourcePath, kind, requiresSaveAs: !string.IsNullOrWhiteSpace(sourcePath));
        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        StatusText = sourceLabel is null ? "Document opened." : $"Opened: {sourceLabel}";
        MarkClean();
    }

    public void LoadFromExportText(string text, string? sourceLabel = null, string? sourcePath = null)
    {
        var parsed = WorkspaceImportRules.ParseTextExportColumns(text);
        ApplyImportedColumns(parsed, sourceLabel, sourcePath, SaveFileKind.TextExport, "Text imported.");
    }

    public void LoadFromExportMarkdown(string markdown, string? sourceLabel = null, string? sourcePath = null)
    {
        var parsed = WorkspaceImportRules.ParseMarkdownExportColumns(markdown);
        ApplyImportedColumns(parsed, sourceLabel, sourcePath, SaveFileKind.MarkdownExport, "Markdown imported.");
    }

    public string ToLayoutJson()
    {
        var lf = new LayoutFile(
            Version: CurrentLayoutVersion,
            ShowLineNumbers: ShowLineNumbers,
            WordWrap: WordWrap,
            EditorFontFamily: EditorFontFamily,
            EditorFontStyle: EditorFontStyleName,
            EditorFontSize: EditorFontSize,
            ThemePreset: ThemePreset,
            SpellCheckEnabled: SpellCheckEnabled,
            EditorLanguageTag: EditorLanguageTag,
            LinedPaperEnabled: LinedPaperEnabled,
            ActiveId: ActiveColumnId,
            ActiveIndex: GetActiveColumnIndex(),
            Columns: Columns.Select(c => new LayoutColumn(
                c.Title,
                c.Text ?? string.Empty,
                c.WidthPx,
                c.IsWidthLocked,
                c.PastePreset.ToString(),
                c.LineMarkerMode.ToString(),
                c.GetCheckedChecklistLineIndexes().ToList(),
                c.EditorFontFamily,
                c.EditorFontSize,
                c.EditorFontStyle.ToString(),
                c.EditorFontWeight.ToString(),
                c.UseDefaultFont)).ToList()
        );

        return JsonSerializer.Serialize(lf, LayoutJsonOptions);
    }

    public bool LoadFromJson(string json, string? sourceLabel = null, string? sourcePath = null, bool preserveCurrentTheme = false)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            StatusText = "Invalid layout file.";
            return false;
        }

        JsonObject? node;
        try
        {
            node = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            StatusText = "Invalid layout file.";
            return false;
        }

        if (node is null)
        {
            StatusText = "Invalid layout file.";
            return false;
        }

        var currentTheme = ThemePreset;
        var layoutVersion = GetJsonValueOrDefault(node, nameof(LayoutFile.Version), 0);
        var showLine = GetJsonValueOrDefault(node, nameof(LayoutFile.ShowLineNumbers), true);
        var wrap = GetJsonValueOrDefault(node, nameof(LayoutFile.WordWrap), true);
        var fontFamily = GetJsonValueOrDefault(node, nameof(LayoutFile.EditorFontFamily), "Consolas");
        var fontStyle = GetJsonValueOrDefault(node, nameof(LayoutFile.EditorFontStyle), "Regular");
        var theme = preserveCurrentTheme
            ? currentTheme
            : GetJsonValueOrDefault(node, nameof(LayoutFile.ThemePreset), ThemePresets[0]);
        var fontSize = GetJsonDoubleOrDefault(node, nameof(LayoutFile.EditorFontSize), 13.0);
        var spellCheckEnabled = GetJsonValueOrDefault(node, nameof(LayoutFile.SpellCheckEnabled), true);
        var defaultLanguageTag = EditorLanguages.Count > 0 ? EditorLanguages[0].Tag : "en-US";
        var editorLanguageTag = NormalizeEditorLanguageTag(GetJsonValueOrDefault(node, nameof(LayoutFile.EditorLanguageTag), defaultLanguageTag));
        var linedPaperEnabled = GetJsonValueOrDefault(node, nameof(LayoutFile.LinedPaperEnabled), false);

        var colsNode = node[nameof(LayoutFile.Columns)] as JsonArray;
        if (colsNode is null || colsNode.Count == 0)
        {
            StatusText = "Invalid layout file.";
            return false;
        }

        var parsedColumns = new List<LayoutColumn>(colsNode.Count);
        var i = 1;
        foreach (var item in colsNode)
        {
            var obj = item as JsonObject;
            var defaultTitle = $"Column {i}";
            var title = GetJsonValueOrDefault(obj, nameof(LayoutColumn.Title), defaultTitle);
            if (string.IsNullOrWhiteSpace(title))
                title = defaultTitle;

            var widthPx = GetJsonNullableInt(obj, nameof(LayoutColumn.WidthPx));
            var isWidthLocked = GetJsonValueOrDefault(obj, nameof(LayoutColumn.IsWidthLocked), false);
            var pastePresetName = GetJsonValueOrDefault(obj, nameof(LayoutColumn.PastePreset), nameof(PasteListPreset.None));
            var pastePreset = ParsePastePreset(pastePresetName);
            var markerModeName = GetJsonValueOrDefault(obj, nameof(LayoutColumn.LineMarkerMode), nameof(LineMarkerMode.Numbers));
            var markerMode = ParseLineMarkerMode(markerModeName);
            var checkedChecklistLineIndexes = GetJsonIntArray(obj, nameof(LayoutColumn.CheckedChecklistLineIndexes));
            var columnFontFamily = GetJsonValueOrDefault(obj, nameof(LayoutColumn.FontFamily), fontFamily);
            var columnFontSize = GetJsonDoubleOrDefault(obj, nameof(LayoutColumn.FontSize), fontSize);
            var columnFontStyle = GetJsonValueOrDefault(obj, nameof(LayoutColumn.FontStyle), _editorFontStyle.ToString());
            var columnFontWeight = GetJsonValueOrDefault(obj, nameof(LayoutColumn.FontWeight), _editorFontWeight.ToString());
            var useDefaultFont = GetJsonValueOrDefault(obj, nameof(LayoutColumn.UseDefaultFont), true);
            var text = NormalizeLoadedColumnText(GetJsonValueOrDefault(obj, nameof(LayoutColumn.Text), string.Empty));
            text = MigrateLegacyInlineTextIfNeeded(layoutVersion, text, widthPx, columnFontSize);
            var markerMigration = MigrateLegacyLineMarkersIfNeeded(layoutVersion, text, markerMode, checkedChecklistLineIndexes);
            text = markerMigration.Text;
            markerMode = markerMigration.Mode;
            checkedChecklistLineIndexes = markerMigration.CheckedChecklistLineIndexes;

            parsedColumns.Add(new LayoutColumn(
                title,
                text,
                widthPx,
                isWidthLocked,
                pastePreset.ToString(),
                markerMode.ToString(),
                checkedChecklistLineIndexes,
                columnFontFamily,
                columnFontSize,
                columnFontStyle,
                columnFontWeight,
                useDefaultFont));
            i++;
        }

        Columns.Clear();
        ShowLineNumbers = showLine;
        WordWrap = wrap;
        EditorFontFamily = fontFamily;
        EditorFontStyleName = fontStyle;
        EditorFontSize = fontSize;
        ThemePreset = theme;
        SpellCheckEnabled = spellCheckEnabled;
        EditorLanguageTag = editorLanguageTag;
        LinedPaperEnabled = linedPaperEnabled;

        foreach (var column in parsedColumns)
        {
            var vm = MakeColumn(column.Title);
            vm.Text = column.Text;
            vm.WidthPx = column.WidthPx;
            vm.IsWidthLocked = column.IsWidthLocked;
            vm.PastePreset = ParsePastePreset(column.PastePreset);
            vm.LineMarkerMode = ParseLineMarkerMode(column.LineMarkerMode);
            vm.SetCheckedChecklistLineIndexes(column.CheckedChecklistLineIndexes);
            vm.EditorFontFamily = string.IsNullOrWhiteSpace(column.FontFamily) ? EditorFontFamily : column.FontFamily;
            vm.EditorFontSize = column.FontSize <= 0 ? EditorFontSize : column.FontSize;
            vm.EditorFontStyle = ParseFontStyle(column.FontStyle, _editorFontStyle);
            vm.EditorFontWeight = ParseFontWeight(column.FontWeight, _editorFontWeight);
            vm.UseDefaultFont = column.UseDefaultFont;
            Columns.Add(vm);
        }


        var activeIndex = GetJsonNullableInt(node, nameof(LayoutFile.ActiveIndex));
        if (activeIndex.HasValue && activeIndex.Value >= 0 && activeIndex.Value < Columns.Count)
        {
            ActiveColumnId = Columns[activeIndex.Value].Id;
        }
        else
        {
            var activeId = GetJsonValueOrDefault(node, nameof(LayoutFile.ActiveId), string.Empty);
            ActiveColumnId = activeId;
            if (string.IsNullOrWhiteSpace(ActiveColumnId) || !Columns.Any(c => c.Id == ActiveColumnId))
                ActiveColumnId = Columns.First().Id;
        }

        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        SetCurrentFileReference(sourcePath, SaveFileKind.Layout);
        StatusText = sourceLabel is null ? "Layout loaded." : $"Opened: {sourceLabel}";
        MarkClean();
        return true;
    }

    public bool SaveCurrentFile()
    {
        if (!CanSaveCurrentFileDirectly)
            return false;

        SaveToPath(CurrentFilePath!, CurrentFileKind);
        return true;
    }

    public void SaveToPath(string path, SaveFileKind kind)
    {
        switch (kind)
        {
            case SaveFileKind.TextDocument:
            case SaveFileKind.MarkdownDocument:
                File.WriteAllText(path, BuildSingleDocumentText(), Encoding.UTF8);
                break;
            case SaveFileKind.TextExport:
                File.WriteAllText(path, BuildExportText(), Encoding.UTF8);
                break;
            case SaveFileKind.MarkdownExport:
                File.WriteAllText(path, BuildExportMarkdown(), Encoding.UTF8);
                break;
            default:
                File.WriteAllText(path, ToLayoutJson());
                break;
        }

        SetCurrentFileReference(path, kind);
        StatusText = $"Saved: {Path.GetFileName(path)}";
        MarkClean();
    }

    public void NewLayout()
    {
        Columns.Clear();
        Columns.Add(MakeColumn("Column 1"));
        Columns.Add(MakeColumn("Column 2"));
        Columns.Add(MakeColumn("Column 3"));

        ActiveColumnId = Columns.First().Id;
        SetCurrentFileReference(null, SaveFileKind.Layout);
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        StatusText = "New layout.";
        MarkClean();
    }

    public bool LoadRecoverySnapshot(WorkspaceRecoveryWorkspace workspace, bool preserveCurrentTheme = false)
    {
        if (!LoadFromJson(workspace.LayoutJson, preserveCurrentTheme: preserveCurrentTheme))
            return false;

        SetCurrentFileReference(workspace.CurrentFilePath, workspace.CurrentFileKind, workspace.RequiresSaveAsBeforeOverwrite);
        if (workspace.IsDirty)
            ForceDirty();
        else
            MarkClean();

        StatusText = $"Recovered: {workspace.Name}";
        return true;
    }

    private void ApplyImportedColumns(
        IReadOnlyList<ImportedColumn> parsed,
        string? sourceLabel,
        string? sourcePath,
        SaveFileKind kind,
        string fallbackStatus)
    {
        var imported = parsed.Count > 0
            ? parsed
            : [new ImportedColumn("Column 1", string.Empty)];

        SetColumnCount(imported.Count);

        for (var i = 0; i < imported.Count; i++)
        {
            var (title, text) = imported[i];
            var column = Columns[i];

            column.Title = string.IsNullOrWhiteSpace(title) ? $"Column {i + 1}" : title.Trim();
            column.Text = text ?? string.Empty;
            column.WidthPx = null;
            column.IsWidthLocked = false;
            column.PastePreset = PasteListPreset.None;
            column.LineMarkerMode = LineMarkerMode.Numbers;
            column.SetCheckedChecklistLineIndexes(null);
            column.EditorFontFamily = EditorFontFamily;
            column.EditorFontSize = EditorFontSize;
            column.EditorFontStyle = _editorFontStyle;
            column.EditorFontWeight = _editorFontWeight;
            column.UseDefaultFont = true;
        }

        ActiveColumnId = Columns.First().Id;
        SetCurrentFileReference(sourcePath, kind, requiresSaveAs: !string.IsNullOrWhiteSpace(sourcePath));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        StatusText = sourceLabel is null ? fallbackStatus : $"Opened: {sourceLabel}";
        MarkClean();
    }
}
