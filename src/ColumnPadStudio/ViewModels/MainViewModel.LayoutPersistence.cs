using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.Models;
using ColumnPadStudio.Services;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    public string ToLayoutJson()
    {
        return SerializeLayoutSnapshot(CaptureRecoveryLayoutSnapshot());
    }

    internal LayoutFile CaptureRecoveryLayoutSnapshot()
    {
        return CreateLayoutSnapshot(includeActiveSelection: true, includeImageContent: true);
    }

    internal static string SerializeLayoutSnapshot(LayoutFile snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return JsonSerializer.Serialize(snapshot, LayoutJsonOptions);
    }

    private LayoutFile CreateLayoutSnapshot(bool includeActiveSelection, bool includeImageContent)
    {
        return new LayoutFile(
            FileType: LayoutFileType,
            Version: CurrentLayoutVersion,
            ShowLineNumbers: ShowLineNumbers,
            GutterWidthPx: GutterWidthPx,
            WordWrap: WordWrap,
            EditorFontFamily: EditorFontFamily,
            EditorFontStyle: EditorFontStyleName,
            EditorFontSize: EditorFontSize,
            ThemePreset: ThemePreset,
            SpellCheckEnabled: SpellCheckEnabled,
            EditorLanguageTag: EditorLanguageTag,
            LinedPaperEnabled: LinedPaperEnabled,
            PaperStyle: SelectedPaperStyle.ToString(),
            ActiveId: includeActiveSelection ? ActiveColumnId : null,
            ActiveIndex: includeActiveSelection ? GetActiveColumnIndex() : null,
            Columns: Columns
                .Select(column => CreateLayoutColumnSnapshot(column, includeImageContent))
                .ToList()
                .AsReadOnly());
    }

    private static LayoutColumn CreateLayoutColumnSnapshot(ColumnViewModel column, bool includeImageContent)
    {
        return new LayoutColumn(
            column.Title,
            column.Text ?? string.Empty,
            column.WidthPx,
            column.IsWidthLocked,
            column.PastePreset.ToString(),
            column.LineMarkerMode.ToString(),
            column.GetCheckedChecklistLineIndexes().ToList().AsReadOnly(),
            column.Images
                .Select(image => new LayoutImage(
                    image.FilePath,
                    image.OriginalFileName,
                    image.Width,
                    image.PixelWidth,
                    image.PixelHeight,
                    image.Left,
                    image.Top,
                    image.Layer.ToString(),
                    includeImageContent && image.ImageContent is not null
                        ? (byte[])image.ImageContent.Clone()
                        : null))
                .ToList()
                .AsReadOnly(),
            column.EditorFontFamily,
            column.EditorFontSize,
            column.EditorFontStyle.ToString(),
            column.EditorFontWeight.ToString(),
            column.UseDefaultFont,
            column.EditorTextColor);
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
        string? fileType = null;
        var hasFileType = root[nameof(LayoutFile.FileType)] is JsonValue fileTypeNode &&
                          fileTypeNode.TryGetValue(out fileType);
        if (root.ContainsKey(nameof(LayoutFile.FileType)) && !hasFileType)
            return RejectInvalidLayout("Invalid layout file type.");

        if (hasFileType && !string.Equals(fileType, LayoutFileType, StringComparison.Ordinal))
            return RejectInvalidLayout("This file is not a ColumnPad layout.");

        if (layoutVersion < 0 || layoutVersion > CurrentLayoutVersion)
            return RejectInvalidLayout("This layout was created by a newer version of ColumnPad.");

        if (layoutVersion >= CurrentLayoutVersion && !hasFileType)
            return RejectInvalidLayout("Invalid ColumnPad layout header.");

        var showLineNumbers = GetJsonValueOrDefault(root, nameof(LayoutFile.ShowLineNumbers), true);
        var gutterWidthPx = GetJsonValueOrDefault(root, nameof(LayoutFile.GutterWidthPx), DefaultGutterWidthPx);
        var wordWrap = GetJsonValueOrDefault(root, nameof(LayoutFile.WordWrap), true);
        var fontFamily = GetJsonValueOrDefault(root, nameof(LayoutFile.EditorFontFamily), "Consolas");
        var fontStyle = GetJsonValueOrDefault(root, nameof(LayoutFile.EditorFontStyle), "Regular");
        var defaultColumnFontFace = ResolveFontFaceOption(ResolveInstalledFamily(fontFamily), fontStyle);
        var theme = preserveCurrentTheme
            ? currentTheme
            : GetJsonValueOrDefault(root, nameof(LayoutFile.ThemePreset), ThemePresets[0]);
        var fontSize = GetJsonDoubleOrDefault(root, nameof(LayoutFile.EditorFontSize), 13.0);
        var spellCheckEnabled = GetJsonValueOrDefault(root, nameof(LayoutFile.SpellCheckEnabled), true);
        var defaultLanguageTag = EditorLanguages.Count > 0 ? EditorLanguages[0].Tag : "en-US";
        var editorLanguageTag = NormalizeEditorLanguageTag(
            GetJsonValueOrDefault(root, nameof(LayoutFile.EditorLanguageTag), defaultLanguageTag));
        var linedPaperEnabled = GetJsonValueOrDefault(root, nameof(LayoutFile.LinedPaperEnabled), false);
        var paperStyle = ParsePaperStyle(
            GetJsonValueOrDefault(root, nameof(LayoutFile.PaperStyle), PaperStyle.Ruled.ToString()));

        if (root[nameof(LayoutFile.Columns)] is not JsonArray columnNodes || columnNodes.Count == 0)
            return RejectInvalidLayout();

        if (columnNodes.Count > WorkspaceConstraints.MaxColumns)
        {
            return RejectInvalidLayout(
                $"This layout contains {columnNodes.Count} columns. ColumnPad supports up to {WorkspaceConstraints.MaxColumns} columns.");
        }

        var parsedColumns = new List<LayoutColumn>(columnNodes.Count);
        for (var index = 0; index < columnNodes.Count; index++)
        {
            if (!TryParseLayoutColumn(
                    columnNodes[index],
                    index,
                    layoutVersion,
                    fontFamily,
                    fontSize,
                    defaultColumnFontFace.Style.ToString(),
                    defaultColumnFontFace.Weight.ToString(),
                    out var column))
                return false;

            parsedColumns.Add(column);
        }

        Columns.Clear();
        ShowLineNumbers = showLineNumbers;
        GutterWidthPx = gutterWidthPx;
        WordWrap = wordWrap;
        EditorFontFamily = fontFamily;
        EditorFontStyleName = fontStyle;
        EditorFontSize = fontSize;
        ThemePreset = theme;
        SpellCheckEnabled = spellCheckEnabled;
        EditorLanguageTag = editorLanguageTag;
        SelectedPaperStyle = paperStyle;
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

    private static PaperStyle ParsePaperStyle(string? value)
    {
        if (string.Equals(value, "Grid", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Dots", StringComparison.OrdinalIgnoreCase))
        {
            return PaperStyle.Ruled;
        }

        return Enum.TryParse<PaperStyle>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
                ? parsed
                : PaperStyle.Ruled;
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
        string defaultFontStyle,
        string defaultFontWeight,
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
        var text = NormalizeLoadedColumnText(layoutVersion, GetJsonValueOrDefault(source, nameof(LayoutColumn.Text), string.Empty));
        var columnFontSize = GetJsonDoubleOrDefault(source, nameof(LayoutColumn.FontSize), defaultFontSize);
        List<LayoutImage> images;
        try
        {
            images = ReadLayoutImages(source);
        }
        catch (InvalidDataException)
        {
            StatusText = $"Invalid layout file: Column {displayIndex} contains damaged or oversized picture data.";
            return false;
        }

        var markerMigration = MigrateLegacyLineMarkersIfNeeded(layoutVersion, text, markerMode, checkedRows);

        column = new LayoutColumn(
            title,
            markerMigration.Text,
            width,
            GetJsonValueOrDefault(source, nameof(LayoutColumn.IsWidthLocked), false),
            ParsePastePreset(GetJsonValueOrDefault(source, nameof(LayoutColumn.PastePreset), nameof(PasteListPreset.None))).ToString(),
            markerMigration.Mode.ToString(),
            markerMigration.CheckedChecklistLineIndexes,
            images,
            GetJsonValueOrDefault(source, nameof(LayoutColumn.FontFamily), defaultFontFamily),
            columnFontSize,
            GetJsonValueOrDefault(source, nameof(LayoutColumn.FontStyle), defaultFontStyle),
            GetJsonValueOrDefault(source, nameof(LayoutColumn.FontWeight), defaultFontWeight),
            GetJsonValueOrDefault(source, nameof(LayoutColumn.UseDefaultFont), true),
            ColumnTextColorService.Normalize(
                GetJsonValueOrDefault(source, nameof(LayoutColumn.EditorTextColor), ColumnTextColorService.ThemeDefault)));
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
                ParseImageLayer(image.Layer),
                image.Content));
        }

        viewModel.EditorFontFamily = string.IsNullOrWhiteSpace(column.FontFamily) ? EditorFontFamily : column.FontFamily;
        viewModel.EditorFontSize = column.FontSize <= 0 ? EditorFontSize : column.FontSize;
        viewModel.EditorFontStyle = ParseFontStyle(column.FontStyle, _editorFontStyle);
        viewModel.EditorFontWeight = ParseFontWeight(column.FontWeight, _editorFontWeight);
        viewModel.UseDefaultFont = column.UseDefaultFont;
        viewModel.EditorTextColor = column.EditorTextColor;
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

    private bool RejectInvalidLayout(string message = "Invalid layout file.")
    {
        StatusText = message;
        return false;
    }
}
