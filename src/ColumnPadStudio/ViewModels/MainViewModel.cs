using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
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

public sealed partial class MainViewModel : NotifyBase
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
    private string _themePreset = ThemePresetService.DefaultPreset;
    private bool _spellCheckEnabled = true;
    private string _editorLanguageTag = "en-US";
    private bool _linedPaperEnabled;
    private bool _requiresSaveAsBeforeOverwrite;
    private string _statusText = "";
    private string _cleanStateSignature = string.Empty;
    private bool _forceDirty;
    private static readonly JsonSerializerOptions LayoutJsonOptions = new() { WriteIndented = true };
    private const int CurrentLayoutVersion = 13;

    private readonly Dictionary<string, FontFaceOption> _fontFaceOptionsByName =
        new(StringComparer.CurrentCultureIgnoreCase);

    public IReadOnlyList<string> EditorFontFamilies { get; } = BuildInstalledFontFamilies();
    public ObservableCollection<string> EditorFontStyles { get; } = new();
    public IReadOnlyList<double> EditorFontSizes { get; } = Enumerable.Range(8, 33).Select(n => (double)n).ToList();
    public IReadOnlyList<string> ThemePresets { get; } = ThemePresetService.Presets;
    public IReadOnlyList<EditorLanguageOption> EditorLanguages { get; } = BuildEditorLanguages();

    public string? CurrentFilePath { get; private set; }
    public SaveFileKind CurrentFileKind { get; private set; } = SaveFileKind.Layout;
    public string CurrentFileDisplayName => string.IsNullOrWhiteSpace(CurrentFilePath)
        ? "Untitled"
        : Path.GetFileName(CurrentFilePath);

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
    public bool IsDefaultThemeSelected => string.Equals(ThemePreset, ThemePresetService.DefaultPreset, StringComparison.Ordinal);
    public bool IsLightThemeSelected => string.Equals(ThemePreset, ThemePresetService.LightPreset, StringComparison.Ordinal);
    public bool IsDarkThemeSelected => string.Equals(ThemePreset, ThemePresetService.DarkPreset, StringComparison.Ordinal);
    public string EditorFontSummary => $"{EditorFontFamily} {EditorFontStyleName} {EditorFontSize:0}";
    public string ProofingLanguageDisplayName => EditorLanguages.FirstOrDefault(language => string.Equals(language.Tag, EditorLanguageTag, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? EditorLanguageTag;
    public string ProofingLanguageHelpText => $"Proofing language: {ProofingLanguageDisplayName}. Spell-check availability depends on installed Windows/WPF dictionaries.";

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
            NotifyEditorFontSummaryChanged();
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
            NotifyEditorFontSummaryChanged();
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
            NotifyEditorFontSummaryChanged();
        }
    }

    public string ThemePreset
    {
        get => _themePreset;
        set
        {
            var normalized = ThemePresetService.Normalize(value);
            var next = ThemePresets.Contains(normalized) ? normalized : ThemePresets[0];
            var previous = _themePreset;
            Set(ref _themePreset, next);
            if (!string.Equals(previous, next, StringComparison.Ordinal))
                NotifyThemeSelectionPropertiesChanged();

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
            if (Equals(_editorLanguageTag, normalized))
                return;

            _editorLanguageTag = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ProofingLanguageDisplayName));
            OnPropertyChanged(nameof(ProofingLanguageHelpText));
            StatusText = $"Proofing language: {ProofingLanguageDisplayName}. Availability depends on installed Windows/WPF dictionaries.";
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

    private void NotifyThemeSelectionPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsDefaultThemeSelected));
        OnPropertyChanged(nameof(IsLightThemeSelected));
        OnPropertyChanged(nameof(IsDarkThemeSelected));
    }

    private void NotifyEditorFontSummaryChanged()
    {
        OnPropertyChanged(nameof(EditorFontSummary));
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
        StatusText = $"Columns: {Columns.Count}    Selected: {active?.Title ?? "-"}    Line nums: {(ShowLineNumbers ? "On" : "Off")}    Wrap: {(WordWrap ? "On" : "Off")}    Font: {EditorFontFamily} {EditorFontStyleName} {EditorFontSize:0}    Theme: {ThemePreset}    Spell: {spellText}    Proofing: {EditorLanguageTag}    Paper: {paperText}{checkText}";
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
}
