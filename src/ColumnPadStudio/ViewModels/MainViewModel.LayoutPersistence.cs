using System.Text.Json;
using System.Text.Json.Nodes;
using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Models;
using ColumnPadStudio.Services;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    public string ToLayoutJson()
    {
        var layout = new LayoutFile(
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
            Columns: Columns.Select(column => new LayoutColumn(
                column.Title,
                column.Text ?? string.Empty,
                column.WidthPx,
                column.IsWidthLocked,
                column.PastePreset.ToString(),
                column.LineMarkerMode.ToString(),
                column.GetCheckedChecklistLineIndexes().ToList(),
                column.Images.Select(image => new LayoutImage(
                    image.FilePath,
                    image.OriginalFileName,
                    image.Width,
                    image.PixelWidth,
                    image.PixelHeight,
                    image.Left,
                    image.Top,
                    image.Layer.ToString())).ToList(),
                column.EditorFontFamily,
                column.EditorFontSize,
                column.EditorFontStyle.ToString(),
                column.EditorFontWeight.ToString(),
                column.UseDefaultFont)).ToList());

        return JsonSerializer.Serialize(layout, LayoutJsonOptions);
    }

    public bool LoadFromJson(
        string json,
        string? sourceLabel = null,
        string? sourcePath = null,
        bool preserveCurrentTheme = false)
    {
        if (string.IsNullOrWhiteSpace(json))
            return RejectInvalidLayout();

        JsonObject? root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject;
        }
        catch (JsonException)
        {
            return RejectInvalidLayout();
        }

        if (root is null)
            return RejectInvalidLayout();

        var currentTheme = ThemePreset;
        var layoutVersion = GetJsonValueOrDefault(root, nameof(LayoutFile.Version), 0);
        var showLineNumbers = GetJsonValueOrDefault(root, nameof(LayoutFile.ShowLineNumbers), true);
        var wordWrap = GetJsonValueOrDefault(root, nameof(LayoutFile.WordWrap), true);
        var fontFamily = GetJsonValueOrDefault(root, nameof(LayoutFile.EditorFontFamily), "Consolas");
        var fontStyle = GetJsonValueOrDefault(root, nameof(LayoutFile.EditorFontStyle), "Regular");
        var theme = preserveCurrentTheme
            ? currentTheme
            : GetJsonValueOrDefault(root, nameof(LayoutFile.ThemePreset), ThemePresets[0]);
        var fontSize = GetJsonDoubleOrDefault(root, nameof(LayoutFile.EditorFontSize), 13.0);
        var spellCheckEnabled = GetJsonValueOrDefault(root, nameof(LayoutFile.SpellCheckEnabled), true);
        var defaultLanguageTag = EditorLanguages.Count > 0 ? EditorLanguages[0].Tag : "en-US";
        var editorLanguageTag = NormalizeEditorLanguageTag(
            GetJsonValueOrDefault(root, nameof(LayoutFile.EditorLanguageTag), defaultLanguageTag));
        var linedPaperEnabled = GetJsonValueOrDefault(root, nameof(LayoutFile.LinedPaperEnabled), false);

        if (root[nameof(LayoutFile.Columns)] is not JsonArray columnNodes || columnNodes.Count == 0)
            return RejectInvalidLayout();

        var parsedColumns = new List<LayoutColumn>(columnNodes.Count);
        for (var index = 0; index < columnNodes.Count; index++)
        {
            if (!TryParseLayoutColumn(columnNodes[index], index, layoutVersion, fontFamily, fontSize, out var column))
                return false;

            parsedColumns.Add(column);
        }

        Columns.Clear();
        ShowLineNumbers = showLineNumbers;
        WordWrap = wordWrap;
        EditorFontFamily = fontFamily;
        EditorFontStyleName = fontStyle;
        EditorFontSize = fontSize;
        ThemePreset = theme;
        SpellCheckEnabled = spellCheckEnabled;
        EditorLanguageTag = editorLanguageTag;
        LinedPaperEnabled = linedPaperEnabled;

        foreach (var column in parsedColumns)
            Columns.Add(CreateColumnFromLayout(column));

        RestoreActiveColumn(root);
        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        SetCurrentFileReference(sourcePath, SaveFileKind.Layout);
        StatusText = sourceLabel is null ? "Layout loaded." : $"Opened: {sourceLabel}";
        MarkClean();
        return true;
    }

    public bool LoadRecoverySnapshot(WorkspaceRecoveryWorkspace workspace, bool preserveCurrentTheme = false)
    {
        ArgumentNullException.ThrowIfNull(workspace);

        if (!LoadFromJson(workspace.LayoutJson, preserveCurrentTheme: preserveCurrentTheme))
            return false;

        SetCurrentFileReference(
            workspace.CurrentFilePath,
            workspace.CurrentFileKind,
            workspace.RequiresSaveAsBeforeOverwrite);

        if (workspace.IsDirty)
            ForceDirty();
        else
            MarkClean();

        StatusText = $"Recovered: {workspace.Name}";
        return true;
    }

    private bool TryParseLayoutColumn(
        JsonNode? node,
        int index,
        int layoutVersion,
        string defaultFontFamily,
        double defaultFontSize,
        out LayoutColumn column)
    {
        column = default!;
        var displayIndex = index + 1;
        if (node is not JsonObject source ||
            source[nameof(LayoutColumn.Text)] is not JsonValue textValue ||
            !textValue.TryGetValue<string>(out _))
        {
            StatusText = $"Invalid layout file: Column {displayIndex} is damaged.";
            return false;
        }

        var defaultTitle = $"Column {displayIndex}";
        var title = GetJsonValueOrDefault(source, nameof(LayoutColumn.Title), defaultTitle);
        if (string.IsNullOrWhiteSpace(title))
            title = defaultTitle;

        var width = GetJsonNullableInt(source, nameof(LayoutColumn.WidthPx));
        var markerMode = ParseLineMarkerMode(
            GetJsonValueOrDefault(source, nameof(LayoutColumn.LineMarkerMode), nameof(LineMarkerMode.Numbers)));
        var checkedRows = GetJsonIntArray(source, nameof(LayoutColumn.CheckedChecklistLineIndexes));
        var text = NormalizeLoadedColumnText(GetJsonValueOrDefault(source, nameof(LayoutColumn.Text), string.Empty));
        var columnFontSize = GetJsonDoubleOrDefault(source, nameof(LayoutColumn.FontSize), defaultFontSize);

        text = MigrateLegacyInlineTextIfNeeded(layoutVersion, text, width, columnFontSize);
        var markerMigration = MigrateLegacyLineMarkersIfNeeded(layoutVersion, text, markerMode, checkedRows);

        column = new LayoutColumn(
            title,
            markerMigration.Text,
            width,
            GetJsonValueOrDefault(source, nameof(LayoutColumn.IsWidthLocked), false),
            ParsePastePreset(GetJsonValueOrDefault(source, nameof(LayoutColumn.PastePreset), nameof(PasteListPreset.None))).ToString(),
            markerMigration.Mode.ToString(),
            markerMigration.CheckedChecklistLineIndexes,
            ReadLayoutImages(source),
            GetJsonValueOrDefault(source, nameof(LayoutColumn.FontFamily), defaultFontFamily),
            columnFontSize,
            GetJsonValueOrDefault(source, nameof(LayoutColumn.FontStyle), _editorFontStyle.ToString()),
            GetJsonValueOrDefault(source, nameof(LayoutColumn.FontWeight), _editorFontWeight.ToString()),
            GetJsonValueOrDefault(source, nameof(LayoutColumn.UseDefaultFont), true));
        return true;
    }

    private ColumnViewModel CreateColumnFromLayout(LayoutColumn column)
    {
        var viewModel = MakeColumn(column.Title);
        viewModel.Text = column.Text;
        viewModel.WidthPx = column.WidthPx;
        viewModel.IsWidthLocked = column.IsWidthLocked;
        viewModel.PastePreset = ParsePastePreset(column.PastePreset);
        viewModel.LineMarkerMode = ParseLineMarkerMode(column.LineMarkerMode);
        viewModel.SetCheckedChecklistLineIndexes(column.CheckedChecklistLineIndexes);

        foreach (var image in column.Images)
        {
            viewModel.Images.Add(new ColumnImageViewModel(
                image.FilePath,
                image.OriginalFileName,
                image.Width,
                image.PixelWidth,
                image.PixelHeight,
                image.Left,
                image.Top,
                ParseImageLayer(image.Layer)));
        }

        viewModel.EditorFontFamily = string.IsNullOrWhiteSpace(column.FontFamily) ? EditorFontFamily : column.FontFamily;
        viewModel.EditorFontSize = column.FontSize <= 0 ? EditorFontSize : column.FontSize;
        viewModel.EditorFontStyle = ParseFontStyle(column.FontStyle, _editorFontStyle);
        viewModel.EditorFontWeight = ParseFontWeight(column.FontWeight, _editorFontWeight);
        viewModel.UseDefaultFont = column.UseDefaultFont;
        return viewModel;
    }

    private void RestoreActiveColumn(JsonObject root)
    {
        var activeIndex = GetJsonNullableInt(root, nameof(LayoutFile.ActiveIndex));
        if (activeIndex is >= 0 && activeIndex < Columns.Count)
        {
            ActiveColumnId = Columns[activeIndex.Value].Id;
            return;
        }

        var activeId = GetJsonValueOrDefault(root, nameof(LayoutFile.ActiveId), string.Empty);
        ActiveColumnId = activeId;
        if (string.IsNullOrWhiteSpace(ActiveColumnId) || !Columns.Any(column => column.Id == ActiveColumnId))
            ActiveColumnId = Columns.First().Id;
    }

    private bool RejectInvalidLayout()
    {
        StatusText = "Invalid layout file.";
        return false;
    }
}
