using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Media;
using ColumnPadStudio.Services;

namespace ColumnPadStudio.ViewModels;

public enum SaveFileKind
{
    Layout,
    TextDocument,
    MarkdownDocument,
    TextExport,
    MarkdownExport
}

public sealed class MainViewModel : NotifyBase
{
    public event EventHandler? RequestRebuildColumns;

    public ObservableCollection<ColumnViewModel> Columns { get; } = new();

    private string? _activeColumnId;
    private bool _showLineNumbers = true;
    private bool _wordWrap = true;
    private string _editorFontFamily = "Consolas";
    private string _editorFontStyleName = "Regular";
    private double _editorFontSize = 13;
    private FontStyle _editorFontStyle = FontStyles.Normal;
    private FontWeight _editorFontWeight = FontWeights.Normal;
    private string _themePreset = "Default Mode";
    private bool _spellCheckEnabled = true;
    private string _editorLanguageTag = "en-US";
    private bool _linedPaperEnabled;
    private bool _requiresSaveAsBeforeOverwrite;
    private string _statusText = "";
    private string _cleanStateSignature = string.Empty;
    private bool _forceDirty;
    private static readonly JsonSerializerOptions LayoutJsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<string, FontFaceOption> _fontFaceOptionsByName =
        new(StringComparer.CurrentCultureIgnoreCase);

    public IReadOnlyList<string> EditorFontFamilies { get; } = BuildInstalledFontFamilies();
    public ObservableCollection<string> EditorFontStyles { get; } = new();
    public IReadOnlyList<double> EditorFontSizes { get; } = Enumerable.Range(8, 33).Select(n => (double)n).ToList();
    public IReadOnlyList<string> ThemePresets { get; } = ["Default Mode", "Light Mode", "Dark Mode"];
    public IReadOnlyList<EditorLanguageOption> EditorLanguages { get; } = BuildEditorLanguages();

    public static string AutoSaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ColumnPadStudio");



    public string? CurrentFilePath { get; private set; }
    public SaveFileKind CurrentFileKind { get; private set; } = SaveFileKind.Layout;
    public bool CanSaveCurrentFileDirectly => !string.IsNullOrWhiteSpace(CurrentFilePath) && !_requiresSaveAsBeforeOverwrite;
    public bool RequiresSaveAsBeforeOverwrite => _requiresSaveAsBeforeOverwrite;
    public bool IsDirty => _forceDirty || !string.Equals(_cleanStateSignature, CaptureDirtyState(), StringComparison.Ordinal);

    public string? ActiveColumnId
    {
        get => _activeColumnId;
        set
        {
            if (Equals(_activeColumnId, value))
                return;

            _activeColumnId = value;
            OnPropertyChanged();
            NotifyActiveColumnActionPropertiesChanged();
        }
    }

    public string LockActiveWidthActionLabel => GetActive()?.IsWidthLocked == true
        ? "_Allow Selected Column Width to Resize"
        : "_Freeze Selected Column Width";

    public string LockActiveWidthActionToolTip => GetActive()?.IsWidthLocked == true
        ? "The selected column width is frozen. Click to allow drag resizing again."
        : "Freeze the selected column width so the splitter cannot resize it.";

    public bool CanMoveActiveColumnLeft => CanMoveActiveColumn(-1);
    public bool CanMoveActiveColumnRight => CanMoveActiveColumn(+1);

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            Set(ref _showLineNumbers, value);
            foreach (var c in Columns) c.ShowLineNumbers = value;
            RefreshStatus();
        }
    }

    public bool WordWrap
    {
        get => _wordWrap;
        set
        {
            Set(ref _wordWrap, value);
            foreach (var c in Columns) c.WordWrap = value;
            RefreshStatus();
        }
    }

    public string EditorFontFamily
    {
        get => _editorFontFamily;
        set
        {
            var next = ResolveInstalledFamily(value);
            Set(ref _editorFontFamily, next);

            UpdateFontFaceOptionsForFamily(next, _editorFontStyleName);
            ApplyEditorFontToColumns();
            RefreshStatus();
        }
    }

    public string EditorFontStyleName
    {
        get => _editorFontStyleName;
        set
        {
            if (!_fontFaceOptionsByName.TryGetValue(value ?? string.Empty, out var option))
                option = _fontFaceOptionsByName.Values.FirstOrDefault(new FontFaceOption("Regular", FontStyles.Normal, FontWeights.Normal));

            Set(ref _editorFontStyleName, option.Name);
            _editorFontStyle = option.Style;
            _editorFontWeight = option.Weight;

            ApplyEditorFontToColumns();
            RefreshStatus();
        }
    }

    public double EditorFontSize
    {
        get => _editorFontSize;
        set
        {
            var clamped = Math.Clamp(value, 8.0, 40.0);
            Set(ref _editorFontSize, clamped);

            ApplyEditorFontToColumns();
            RefreshStatus();
        }
    }

    public string ThemePreset
    {
        get => _themePreset;
        set
        {
            var normalized = NormalizeThemePreset(value);
            var next = ThemePresets.Contains(normalized) ? normalized : ThemePresets[0];
            Set(ref _themePreset, next);
            RefreshStatus();
        }
    }

    public bool SpellCheckEnabled
    {
        get => _spellCheckEnabled;
        set
        {
            Set(ref _spellCheckEnabled, value);
            RefreshStatus();
        }
    }

    public string EditorLanguageTag
    {
        get => _editorLanguageTag;
        set
        {
            var normalized = NormalizeEditorLanguageTag(value);
            Set(ref _editorLanguageTag, normalized);
            RefreshStatus();
        }
    }

    public bool LinedPaperEnabled
    {
        get => _linedPaperEnabled;
        set
        {
            Set(ref _linedPaperEnabled, value);
            RefreshStatus();
        }
    }

    public int ColumnCount
    {
        get => Columns.Count;
        set => SetColumnCount(value);
    }

    public string StatusText
    {
        get => _statusText;
        set => Set(ref _statusText, value);
    }

    public FontStyle DefaultEditorFontStyle => _editorFontStyle;
    public FontWeight DefaultEditorFontWeight => _editorFontWeight;

    public MainViewModel()
    {
        if (!EditorFontFamilies.Contains(_editorFontFamily, StringComparer.OrdinalIgnoreCase))
            _editorFontFamily = EditorFontFamilies.Count > 0 ? EditorFontFamilies[0] : "Consolas";

        UpdateFontFaceOptionsForFamily(_editorFontFamily, _editorFontStyleName);
        _editorLanguageTag = NormalizeEditorLanguageTag(_editorLanguageTag);

        Columns.Add(MakeColumn("Column 1"));
        Columns.Add(MakeColumn("Column 2"));
        Columns.Add(MakeColumn("Column 3"));
        ActiveColumnId = Columns.Count > 0 ? Columns[0].Id : null;

        WordWrap = true;
        RefreshStatus();
        MarkClean();
    }

    private ColumnViewModel MakeColumn(string title)
    {
        var c = new ColumnViewModel
        {
            Title = title,
            ShowLineNumbers = ShowLineNumbers,
            WordWrap = WordWrap,
            EditorFontFamily = EditorFontFamily,
            EditorFontSize = EditorFontSize,
            EditorFontStyle = _editorFontStyle,
            EditorFontWeight = _editorFontWeight,
            UseDefaultFont = true
        };
        c.PropertyChanged += Column_PropertyChanged;
        return c;
    }

    private void Column_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ColumnViewModel column)
            return;

        if (!ReferenceEquals(column, GetActive()))
            return;

        if (e.PropertyName is nameof(ColumnViewModel.Title) or nameof(ColumnViewModel.ChecklistTotal) or nameof(ColumnViewModel.ChecklistDone))
            RefreshStatus();
    }

    private void ApplyEditorFontToColumns()
    {
        foreach (var c in Columns)
        {
            if (!c.UseDefaultFont)
                continue;

            c.EditorFontFamily = EditorFontFamily;
            c.EditorFontSize = EditorFontSize;
            c.EditorFontStyle = _editorFontStyle;
            c.EditorFontWeight = _editorFontWeight;
        }
    }

    public void RefreshStatus()
    {
        var active = GetActive();
        var checklistTotal = active?.ChecklistTotal ?? 0;
        var checklistDone = active?.ChecklistDone ?? 0;

        var checkText = checklistTotal > 0
            ? $"    Done: {checklistDone}/{checklistTotal}"
            : string.Empty;

        var spellText = SpellCheckEnabled ? "On" : "Off";
        var paperText = LinedPaperEnabled ? "On" : "Off";
        StatusText = $"Columns: {Columns.Count}    Selected: {active?.Title ?? "-"}    Line nums: {(ShowLineNumbers ? "On" : "Off")}    Wrap: {(WordWrap ? "On" : "Off")}    Font: {EditorFontFamily} {EditorFontStyleName} {EditorFontSize:0}    Theme: {ThemePreset}    Spell: {spellText}    Lang: {EditorLanguageTag}    Paper: {paperText}{checkText}";
    }

    public ColumnViewModel? GetActive()
    {
        if (ActiveColumnId is null) return Columns.FirstOrDefault();
        return Columns.FirstOrDefault(c => c.Id == ActiveColumnId) ?? Columns.FirstOrDefault();
    }

    private int GetActiveColumnIndex()
    {
        var active = GetActive();
        if (active is null)
            return 0;

        var index = Columns.IndexOf(active);
        return index < 0 ? 0 : index;
    }

    private void SetCurrentFileReference(string? path, SaveFileKind kind, bool requiresSaveAs = false)
    {
        CurrentFilePath = string.IsNullOrWhiteSpace(path) ? null : path;
        CurrentFileKind = kind;
        _requiresSaveAsBeforeOverwrite = CurrentFilePath is not null && requiresSaveAs;
        OnPropertyChanged(nameof(CanSaveCurrentFileDirectly));
        OnPropertyChanged(nameof(RequiresSaveAsBeforeOverwrite));
    }
    private void MarkClean()
    {
        _cleanStateSignature = CaptureDirtyState();
        _forceDirty = false;
    }

    private void ForceDirty()
    {
        _forceDirty = true;
    }

    private string CaptureDirtyState()
    {
        return CurrentFileKind switch
        {
            SaveFileKind.TextDocument or SaveFileKind.MarkdownDocument => BuildSingleDocumentText(),
            SaveFileKind.TextExport => BuildExportText(),
            SaveFileKind.MarkdownExport => BuildExportMarkdown(),
            _ => JsonSerializer.Serialize(new DirtyWorkspaceState(
                ShowLineNumbers,
                WordWrap,
                EditorFontFamily,
                EditorFontStyleName,
                EditorFontSize,
                ThemePreset,
                SpellCheckEnabled,
                EditorLanguageTag,
                LinedPaperEnabled,
                Columns.Select(c => new DirtyColumnState(
                    c.Title,
                    c.Text ?? string.Empty,
                    c.WidthPx,
                    c.IsWidthLocked,
                    c.PastePreset.ToString(),
                    c.EditorFontFamily,
                    c.EditorFontSize,
                    c.EditorFontStyle.ToString(),
                    c.EditorFontWeight.ToString(),
                    c.UseDefaultFont)).ToList()))
        };
    }

    private bool IsRawDocumentKind => CurrentFileKind is SaveFileKind.TextDocument or SaveFileKind.MarkdownDocument;

    private void PromoteRawDocumentToLayoutIfNeeded(int targetColumnCount)
    {
        if (!IsRawDocumentKind || targetColumnCount <= 1)
            return;

        SetCurrentFileReference(null, SaveFileKind.Layout);
    }

    private static string BuildDocumentTitle(string? sourceLabel)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourceLabel);
        return string.IsNullOrWhiteSpace(baseName) ? "Document" : baseName;
    }

    private bool CanMoveActiveColumn(int delta)
    {
        var active = GetActive();
        if (active is null)
            return false;

        var currentIndex = Columns.IndexOf(active);
        var targetIndex = currentIndex + delta;
        return currentIndex >= 0 && targetIndex >= 0 && targetIndex < Columns.Count;
    }

    private void NotifyActiveColumnActionPropertiesChanged()
    {
        OnPropertyChanged(nameof(LockActiveWidthActionLabel));
        OnPropertyChanged(nameof(LockActiveWidthActionToolTip));
        OnPropertyChanged(nameof(CanMoveActiveColumnLeft));
        OnPropertyChanged(nameof(CanMoveActiveColumnRight));
    }

    public IReadOnlyList<ColumnViewModel> GetQuickJumpColumns()
    {
        return Columns.ToList();
    }

    public void SetColumnCount(int requestedCount)
    {
        var target = Math.Clamp(requestedCount, 1, 9999);
        PromoteRawDocumentToLayoutIfNeeded(target);
        var changed = false;

        while (Columns.Count < target)
        {
            Columns.Add(MakeColumn($"Column {Columns.Count + 1}"));
            changed = true;
        }

        while (Columns.Count > target && Columns.Count > 1)
        {
            Columns.RemoveAt(Columns.Count - 1);
            changed = true;
        }

        var activeWasInvalid = ActiveColumnId is null || !Columns.Any(c => c.Id == ActiveColumnId);
        if (activeWasInvalid)
            ActiveColumnId = Columns.First().Id;

        // Prevent feedback loops from UI text updates when the effective count did not change.
        if (!changed && !activeWasInvalid)
            return;

        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
    }

    public void AddColumn()
    {
        PromoteRawDocumentToLayoutIfNeeded(Columns.Count + 1);
        Columns.Add(MakeColumn($"Column {Columns.Count + 1}"));
        ActiveColumnId = Columns.Last().Id;
        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
    }

    public bool RemoveActiveColumn()
    {
        if (Columns.Count <= 1)
        {
            StatusText = "You need at least 1 column.";
            return false;
        }

        var active = GetActive();
        if (active is null)
            return false;

        var idx = Columns.IndexOf(active);
        Columns.Remove(active);

        ActiveColumnId = Columns[Math.Max(0, idx - 1)].Id;
        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        return true;
    }

    public void ResetActiveColumnWidth()
    {
        var active = GetActive();
        if (active is null)
            return;

        active.WidthPx = null;
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        StatusText = "Selected column width reset.";
    }

    public void ResetAllColumnWidths()
    {
        foreach (var c in Columns)
            c.WidthPx = null;

        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        StatusText = "All column widths reset.";
    }

    public void SetActiveColumnWidth(int widthPx)
    {
        var active = GetActive();
        if (active is null)
            return;

        active.WidthPx = Math.Clamp(widthPx, 120, 5000);
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        StatusText = $"Set {active.Title} width to {active.WidthPx}px.";
    }

    public void ToggleLockActiveWidth()
    {
        var active = GetActive();
        if (active is null) return;

        active.IsWidthLocked = !active.IsWidthLocked;
        NotifyActiveColumnActionPropertiesChanged();
        StatusText = active.IsWidthLocked
            ? $"Froze {active.Title} width."
            : $"{active.Title} width can resize again.";
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
    }

    private bool SwapActiveColumn(int delta)
    {
        var active = GetActive();
        if (active is null)
            return false;

        var currentIndex = Columns.IndexOf(active);
        var targetIndex = currentIndex + delta;
        if (currentIndex < 0)
            return false;

        if (targetIndex < 0)
        {
            StatusText = $"{active.Title} is already the first column.";
            return false;
        }

        if (targetIndex >= Columns.Count)
        {
            StatusText = $"{active.Title} is already the last column.";
            return false;
        }

        var other = Columns[targetIndex];
        (Columns[currentIndex], Columns[targetIndex]) = (Columns[targetIndex], Columns[currentIndex]);

        ActiveColumnId = active.Id;
        NotifyActiveColumnActionPropertiesChanged();
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        StatusText = $"Swapped {active.Title} with {other.Title}.";
        return true;
    }

    public bool MoveActiveColumnLeft()
    {
        return SwapActiveColumn(-1);
    }

    public bool MoveActiveColumnRight()
    {
        return SwapActiveColumn(+1);
    }

    public void ClearAll()
    {
        foreach (var c in Columns) c.Text = string.Empty;
        StatusText = "Cleared.";
    }

    public void DuplicateActive()
    {
        var a = GetActive();
        if (a is null) return;

        PromoteRawDocumentToLayoutIfNeeded(Columns.Count + 1);

        var copy = MakeColumn($"{a.Title} (copy)");
        copy.Text = a.Text;
        copy.WidthPx = a.WidthPx;
        copy.IsWidthLocked = a.IsWidthLocked;
        copy.PastePreset = a.PastePreset;
        copy.EditorFontFamily = a.EditorFontFamily;
        copy.EditorFontSize = a.EditorFontSize;
        copy.EditorFontStyle = a.EditorFontStyle;
        copy.EditorFontWeight = a.EditorFontWeight;
        copy.UseDefaultFont = a.UseDefaultFont;

        Columns.Add(copy);
        ActiveColumnId = copy.Id;
        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
    }

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
        var parsed = ParseTextExportColumns(text);
        ApplyImportedColumns(parsed, sourceLabel, sourcePath, SaveFileKind.TextExport, "Text imported.");
    }

    public void LoadFromExportMarkdown(string markdown, string? sourceLabel = null, string? sourcePath = null)
    {
        var parsed = ParseMarkdownExportColumns(markdown);
        ApplyImportedColumns(parsed, sourceLabel, sourcePath, SaveFileKind.MarkdownExport, "Markdown imported.");
    }

    public string ToLayoutJson()
    {
        var lf = new LayoutFile(
            Version: 11,
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

            var text = GetJsonValueOrDefault(obj, nameof(LayoutColumn.Text), string.Empty);
            var widthPx = GetJsonNullableInt(obj, nameof(LayoutColumn.WidthPx));
            var isWidthLocked = GetJsonValueOrDefault(obj, nameof(LayoutColumn.IsWidthLocked), false);
            var pastePresetName = GetJsonValueOrDefault(obj, nameof(LayoutColumn.PastePreset), nameof(PasteListPreset.None));
            var pastePreset = ParsePastePreset(pastePresetName);
            var columnFontFamily = GetJsonValueOrDefault(obj, nameof(LayoutColumn.FontFamily), fontFamily);
            var columnFontSize = GetJsonDoubleOrDefault(obj, nameof(LayoutColumn.FontSize), fontSize);
            var columnFontStyle = GetJsonValueOrDefault(obj, nameof(LayoutColumn.FontStyle), _editorFontStyle.ToString());
            var columnFontWeight = GetJsonValueOrDefault(obj, nameof(LayoutColumn.FontWeight), _editorFontWeight.ToString());
            var useDefaultFont = GetJsonValueOrDefault(obj, nameof(LayoutColumn.UseDefaultFont), true);

            parsedColumns.Add(new LayoutColumn(
                title,
                text,
                widthPx,
                isWidthLocked,
                pastePreset.ToString(),
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
        SetCurrentFileReference(sourcePath, SaveFileKind.Layout, requiresSaveAs: !string.IsNullOrWhiteSpace(sourcePath));
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

    public void LoadFromFile(string path, bool preserveCurrentTheme = false)
    {
        LoadFromJson(File.ReadAllText(path), Path.GetFileName(path), path, preserveCurrentTheme);
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

    public bool LoadRecoverySnapshot(WorkspaceRecoveryWorkspace workspace)
    {
        if (!LoadFromJson(workspace.LayoutJson))
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
        IReadOnlyList<(string Title, string Text)> parsed,
        string? sourceLabel,
        string? sourcePath,
        SaveFileKind kind,
        string fallbackStatus)
    {
        var imported = parsed.Count > 0
            ? parsed
            : [("Column 1", string.Empty)];

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

    private static List<(string Title, string Text)> ParseTextExportColumns(string text)
    {
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var parsed = new List<(string Title, string Text)>();

        string? currentTitle = null;
        var body = new StringBuilder();
        var skipInitialBlank = false;

        void Flush()
        {
            if (currentTitle is null)
                return;

            parsed.Add((currentTitle, body.ToString().TrimEnd('\n')));
            body.Clear();
        }

        foreach (var line in lines)
        {
            if (TryParseTextExportHeader(line, out var title))
            {
                Flush();
                currentTitle = string.IsNullOrWhiteSpace(title) ? $"Column {parsed.Count + 1}" : title.Trim();
                skipInitialBlank = true;
                continue;
            }

            currentTitle ??= "Column 1";

            if (skipInitialBlank && line.Length == 0)
            {
                skipInitialBlank = false;
                continue;
            }

            skipInitialBlank = false;
            body.Append(line).Append('\n');
        }

        Flush();

        if (parsed.Count == 0)
            parsed.Add(("Column 1", normalized.TrimEnd('\n')));

        return parsed;
    }

    private static bool TryParseTextExportHeader(string line, out string title)
    {
        const string prefix = "===== ";
        const string suffix = " =====";

        if (line.StartsWith(prefix, StringComparison.Ordinal) &&
            line.EndsWith(suffix, StringComparison.Ordinal) &&
            line.Length >= prefix.Length + suffix.Length + 1)
        {
            title = line[prefix.Length..^suffix.Length];
            return true;
        }

        title = string.Empty;
        return false;
    }

    private static List<(string Title, string Text)> ParseMarkdownExportColumns(string markdown)
    {
        var normalized = (markdown ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        var parsed = new List<(string Title, string Text)>();

        string? currentTitle = null;
        var body = new StringBuilder();
        var skipInitialBlank = false;

        void Flush()
        {
            if (currentTitle is null)
                return;

            parsed.Add((currentTitle, body.ToString().TrimEnd('\n')));
            body.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Flush();
                var heading = line[3..];
                currentTitle = string.IsNullOrWhiteSpace(heading) ? $"Column {parsed.Count + 1}" : heading.Trim();
                skipInitialBlank = true;
                continue;
            }

            currentTitle ??= "Column 1";

            if (skipInitialBlank && line.Length == 0)
            {
                skipInitialBlank = false;
                continue;
            }

            skipInitialBlank = false;
            body.Append(line).Append('\n');
        }

        Flush();

        if (parsed.Count == 0)
            parsed.Add(("Column 1", normalized.TrimEnd('\n')));

        return parsed;
    }

    private static IReadOnlyList<EditorLanguageOption> BuildEditorLanguages()
    {
        var languageTags = new[]
        {
            "en-US",
            "en-GB",
            "fr-FR",
            "de-DE",
            "es-ES",
            "it-IT",
            "pt-BR",
            "pt-PT",
            "nl-NL",
            "sv-SE",
            "da-DK",
            "nb-NO"
        };

        return languageTags
            .Select(tag => new EditorLanguageOption(tag, BuildLanguageDisplayName(tag)))
            .ToList();
    }

    private string NormalizeEditorLanguageTag(string? requestedTag)
    {
        if (!string.IsNullOrWhiteSpace(requestedTag))
        {
            var match = EditorLanguages.FirstOrDefault(language =>
                string.Equals(language.Tag, requestedTag, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match.Tag;
        }

        return EditorLanguages.Count > 0 ? EditorLanguages[0].Tag : "en-US";
    }

    private static string BuildLanguageDisplayName(string tag)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(tag);
            return $"{culture.EnglishName} ({culture.Name})";
        }
        catch (CultureNotFoundException)
        {
            return tag;
        }
    }

    private static IReadOnlyList<string> BuildInstalledFontFamilies()
    {
        var names = Fonts.SystemFontFamilies
            .Select(f => f.Source)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        if (names.Count == 0)
            names.AddRange(["Consolas", "Segoe UI", "Courier New"]);

        return names;
    }

    private string ResolveInstalledFamily(string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var match = EditorFontFamilies.FirstOrDefault(f => string.Equals(f, requested, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
                return match;
        }

        return EditorFontFamilies.Count > 0 ? EditorFontFamilies[0] : "Consolas";
    }

    private void UpdateFontFaceOptionsForFamily(string familyName, string? preferredStyleName)
    {
        var family = Fonts.SystemFontFamilies.FirstOrDefault(f =>
            string.Equals(f.Source, familyName, StringComparison.OrdinalIgnoreCase));

        family ??= new FontFamily(EditorFontFamilies.Count > 0 ? EditorFontFamilies[0] : "Consolas");

        var options = BuildFontFaceOptions(family);

        _fontFaceOptionsByName.Clear();
        EditorFontStyles.Clear();
        foreach (var option in options)
        {
            _fontFaceOptionsByName[option.Name] = option;
            EditorFontStyles.Add(option.Name);
        }

        var desired = preferredStyleName;
        if (string.IsNullOrWhiteSpace(desired) || !_fontFaceOptionsByName.ContainsKey(desired))
            desired = EditorFontStyles.FirstOrDefault() ?? "Regular";

        if (_fontFaceOptionsByName.TryGetValue(desired, out var selected))
        {
            Set(ref _editorFontStyleName, selected.Name);
            _editorFontStyle = selected.Style;
            _editorFontWeight = selected.Weight;
        }
    }

    private static List<FontFaceOption> BuildFontFaceOptions(FontFamily family)
    {
        var options = new Dictionary<string, FontFaceOption>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var typeface in family.GetTypefaces())
        {
            if (!typeface.TryGetGlyphTypeface(out _))
                continue;

            var name = ToStyleName(typeface.Style, typeface.Weight);
            options[name] = new FontFaceOption(name, typeface.Style, typeface.Weight);
        }

        if (options.Count == 0)
            options["Regular"] = new FontFaceOption("Regular", FontStyles.Normal, FontWeights.Normal);

        return options.Values
            .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static string ToStyleName(FontStyle style, FontWeight weight)
    {
        var parts = new List<string>();

        var weightName = weight.ToOpenTypeWeight() switch
        {
            <= 150 => "Thin",
            <= 250 => "ExtraLight",
            <= 350 => "Light",
            <= 450 => "Regular",
            <= 550 => "Medium",
            <= 650 => "SemiBold",
            <= 750 => "Bold",
            <= 850 => "ExtraBold",
            <= 950 => "Black",
            _ => "ExtraBlack"
        };

        if (!string.Equals(weightName, "Regular", StringComparison.OrdinalIgnoreCase) || style == FontStyles.Normal)
            parts.Add(weightName);

        if (style == FontStyles.Italic)
            parts.Add("Italic");
        else if (style == FontStyles.Oblique)
            parts.Add("Oblique");

        return parts.Count == 0 ? "Regular" : string.Join(' ', parts);
    }

    private static string NormalizeThemePreset(string? value)
    {
        if (string.Equals(value, "Notepad Classic", StringComparison.OrdinalIgnoreCase))
            return "Light Mode";

        if (string.Equals(value, "High Contrast", StringComparison.OrdinalIgnoreCase))
            return "Dark Mode";

        if (string.Equals(value, "Compact", StringComparison.OrdinalIgnoreCase))
            return "Default Mode";

        return value ?? string.Empty;
    }

    private static PasteListPreset ParsePastePreset(string? value)
    {
        if (Enum.TryParse<PasteListPreset>(value, ignoreCase: true, out var parsed))
            return parsed;

        return PasteListPreset.None;
    }

    private static FontStyle ParseFontStyle(string? value, FontStyle fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        try
        {
            var converter = new FontStyleConverter();
            if (converter.ConvertFromString(value) is FontStyle parsed)
                return parsed;
        }
        catch (FormatException)
        {
            // Fallback to existing font style when persisted value is invalid.
        }
        catch (NotSupportedException)
        {
            // Fallback to existing font style when persisted value is invalid.
        }

        return fallback;
    }

    private static FontWeight ParseFontWeight(string? value, FontWeight fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        try
        {
            var converter = new FontWeightConverter();
            if (converter.ConvertFromString(value) is FontWeight parsed)
                return parsed;
        }
        catch (FormatException)
        {
            // Fallback to existing font weight when persisted value is invalid.
        }
        catch (NotSupportedException)
        {
            // Fallback to existing font weight when persisted value is invalid.
        }

        return fallback;
    }

    private static T GetJsonValueOrDefault<T>(JsonObject? node, string propertyName, T fallback)
    {
        if (node is not null &&
            node[propertyName] is JsonValue valueNode &&
            valueNode.TryGetValue<T>(out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static double GetJsonDoubleOrDefault(JsonObject? node, string propertyName, double fallback)
    {
        if (node is null || node[propertyName] is not JsonValue valueNode)
            return fallback;

        if (valueNode.TryGetValue<double>(out var asDouble))
            return asDouble;

        if (valueNode.TryGetValue<int>(out var asInt))
            return asInt;

        return fallback;
    }

    private static int? GetJsonNullableInt(JsonObject? node, string propertyName)
    {
        if (node is not null &&
            node[propertyName] is JsonValue valueNode &&
            valueNode.TryGetValue<int>(out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private sealed record LayoutFile(
        int Version,
        bool ShowLineNumbers,
        bool WordWrap,
        string EditorFontFamily,
        string EditorFontStyle,
        double EditorFontSize,
        string ThemePreset,
        bool SpellCheckEnabled,
        string EditorLanguageTag,
        bool LinedPaperEnabled,
        string? ActiveId,
        int? ActiveIndex,
        List<LayoutColumn> Columns);

    private sealed record LayoutColumn(
        string Title,
        string Text,
        int? WidthPx,
        bool IsWidthLocked,
        string PastePreset,
        string FontFamily,
        double FontSize,
        string FontStyle,
        string FontWeight,
        bool UseDefaultFont);

    private sealed record DirtyWorkspaceState(
        bool ShowLineNumbers,
        bool WordWrap,
        string EditorFontFamily,
        string EditorFontStyle,
        double EditorFontSize,
        string ThemePreset,
        bool SpellCheckEnabled,
        string EditorLanguageTag,
        bool LinedPaperEnabled,
        List<DirtyColumnState> Columns);

    private sealed record DirtyColumnState(
        string Title,
        string Text,
        int? WidthPx,
        bool IsWidthLocked,
        string PastePreset,
        string FontFamily,
        double FontSize,
        string FontStyle,
        string FontWeight,
        bool UseDefaultFont);

    public sealed record EditorLanguageOption(string Tag, string DisplayName);

    private readonly record struct FontFaceOption(string Name, FontStyle Style, FontWeight Weight);
}






















