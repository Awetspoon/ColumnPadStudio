using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using ColumnPadStudio.Application.Services;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.App.ViewModels;

public sealed class WorkspaceViewModel : ObservableObject
{
    private static readonly HashSet<string> PersistedColumnPropertyNames =
    [
        nameof(ColumnViewModel.Title),
        nameof(ColumnViewModel.Text),
        nameof(ColumnViewModel.MarkerMode),
        nameof(ColumnViewModel.PastePreset),
        nameof(ColumnViewModel.UseDefaultFont),
        nameof(ColumnViewModel.FontFamilyName),
        nameof(ColumnViewModel.FontSize),
        nameof(ColumnViewModel.FontFaceStyle),
        nameof(ColumnViewModel.StoredWidth),
        nameof(ColumnViewModel.IsWidthLocked),
        nameof(ColumnViewModel.CheckedLinesRevision)
    ];

    private string _name;
    private ColumnViewModel? _selectedColumn;
    private bool _isDirty;
    private string? _filePath;
    private string _fontFamily;
    private double _fontSize;
    private EditorLineStyle _lineStyle;
    private bool _showLineNumbers;
    private bool _wordWrap;
    private bool _linedPaper;
    private bool _spellCheckEnabled;
    private string _languageTag;
    private ThemePreset _themePreset;
    private WorkspaceFileKind _fileKind;
    private bool _saveAsRequired;
    private string _fontFaceStyle = "Regular";
    private int _lastMultiColumnCount;
    private string? _savedComparisonSignature;

    public WorkspaceViewModel(WorkspaceDocument document)
    {
        document = WorkspaceDocumentLogic.Normalize(document);
        Id = document.Id;
        _name = document.Name;
        _lastMultiColumnCount = WorkspaceRules.ClampColumnCount(document.LastMultiColumnCount <= 1 ? 3 : document.LastMultiColumnCount);
        _fontFamily = document.Defaults.FontFamily;
        _fontFaceStyle = string.IsNullOrWhiteSpace(document.Defaults.FontFaceStyle) ? "Regular" : document.Defaults.FontFaceStyle;
        _fontSize = WorkspaceRules.ClampFontSize(document.Defaults.FontSize <= 0 ? 13 : document.Defaults.FontSize);
        _lineStyle = document.Defaults.LineStyle;
        _showLineNumbers = document.Defaults.ShowLineNumbers;
        _wordWrap = document.Defaults.WordWrap;
        _linedPaper = document.Defaults.LinedPaper;
        _spellCheckEnabled = document.Defaults.SpellCheckEnabled;
        _languageTag = string.IsNullOrWhiteSpace(document.Defaults.LanguageTag) ? "en-US" : document.Defaults.LanguageTag;
        _themePreset = document.Defaults.ThemePreset;
        _fileKind = WorkspaceFileKind.Layout;
        _saveAsRequired = true;

        Columns = new ObservableCollection<ColumnViewModel>(
            document.Columns.Select(c => CreateColumnViewModel(c)));

        if (Columns.Count == 0)
        {
            Columns.Add(CreateColumnViewModel(BuildColumnDocument("Column 1", MarkerMode.Numbers)));
        }

        SelectColumn(Columns[Math.Clamp(document.ActiveColumnIndex, 0, Columns.Count - 1)]);
        ApplyWorkspaceFontDefaultsToColumns();
        ApplyWorkspaceLineStyleToColumns();
        CaptureSavedComparisonSignature();
    }

    public Guid Id { get; }
    public ObservableCollection<ColumnViewModel> Columns { get; }
    public bool HasSelectedColumn => SelectedColumn is not null;
    public bool CanDirectSave => !string.IsNullOrWhiteSpace(FilePath) && !SaveAsRequired;

    public int LastMultiColumnCount
    {
        get => _lastMultiColumnCount;
        private set => _lastMultiColumnCount = value;
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                OnPropertyChanged(nameof(DisplayName));
                RecalculateDirty();
            }
        }
    }

    public ColumnViewModel? SelectedColumn
    {
        get => _selectedColumn;
        set
        {
            if (_selectedColumn == value)
            {
                return;
            }

            if (_selectedColumn is not null)
            {
                _selectedColumn.IsSelected = false;
            }

            _selectedColumn = value;
            if (_selectedColumn is not null)
            {
                _selectedColumn.IsSelected = true;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedColumn));
        }
    }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (SetProperty(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DisplayName => IsDirty ? $"{Name} *" : Name;
    public string? FilePath => _filePath;

    public WorkspaceFileKind FileKind => _fileKind;

    public string FileKindDisplay => FileKind switch
    {
        WorkspaceFileKind.RawText => "Raw text",
        WorkspaceFileKind.RawMarkdown => "Raw markdown",
        WorkspaceFileKind.TextExport => "Text export",
        WorkspaceFileKind.MarkdownExport => "Markdown export",
        WorkspaceFileKind.Session => "Workspace session",
        _ => "Layout"
    };

    public bool SaveAsRequired => _saveAsRequired;

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            if (SetProperty(ref _showLineNumbers, value))
            {
                foreach (var column in Columns)
                {
                    column.ShowLineNumbers = value;
                }

                RecalculateDirty();
            }
        }
    }

    public string FontFamilyName
    {
        get => _fontFamily;
        set
        {
            if (SetProperty(ref _fontFamily, value))
            {
                ApplyWorkspaceFontDefaultsToColumns();
                RecalculateDirty();
            }
        }
    }

    public double FontSize
    {
        get => _fontSize;
        set
        {
            if (SetProperty(ref _fontSize, WorkspaceRules.ClampFontSize(value)))
            {
                OnPropertyChanged(nameof(FontSizeText));
                ApplyWorkspaceFontDefaultsToColumns();
                RecalculateDirty();
            }
        }
    }

    public EditorLineStyle LineStyle
    {
        get => _lineStyle;
        set
        {
            if (SetProperty(ref _lineStyle, value))
            {
                ApplyWorkspaceLineStyleToColumns();
                RecalculateDirty();
            }
        }
    }

    public string FontSizeText
    {
        get => WorkspaceRules.FormatFontSize(FontSize);
        set
        {
            if (WorkspaceRules.TryParseFontSize(value, out var parsed))
            {
                FontSize = parsed;
            }

            OnPropertyChanged();
        }
    }

    public string FontFaceStyle
    {
        get => _fontFaceStyle;
        set
        {
            if (SetProperty(ref _fontFaceStyle, value))
            {
                OnPropertyChanged(nameof(EditorFontStyle));
                OnPropertyChanged(nameof(EditorFontWeight));
                ApplyWorkspaceFontDefaultsToColumns();
                RecalculateDirty();
            }
        }
    }

    public FontStyle EditorFontStyle => FontFaceStyle.Contains("Italic", StringComparison.OrdinalIgnoreCase)
        ? FontStyles.Italic
        : FontStyles.Normal;

    public FontWeight EditorFontWeight => FontFaceStyle.Contains("Bold", StringComparison.OrdinalIgnoreCase)
        ? FontWeights.Bold
        : FontWeights.Normal;

    public bool WordWrap
    {
        get => _wordWrap;
        set
        {
            if (SetProperty(ref _wordWrap, value))
            {
                RecalculateDirty();
            }
        }
    }

    public bool LinedPaper
    {
        get => _linedPaper;
        set
        {
            if (SetProperty(ref _linedPaper, value))
            {
                RecalculateDirty();
            }
        }
    }

    public bool SpellCheckEnabled
    {
        get => _spellCheckEnabled;
        set
        {
            if (SetProperty(ref _spellCheckEnabled, value))
            {
                RecalculateDirty();
            }
        }
    }

    public string LanguageTag
    {
        get => _languageTag;
        set
        {
            if (SetProperty(ref _languageTag, value))
            {
                RecalculateDirty();
            }
        }
    }

    public ThemePreset ThemePreset
    {
        get => _themePreset;
        set
        {
            if (SetProperty(ref _themePreset, value))
            {
                RecalculateDirty();
            }
        }
    }

    public void AddColumn()
    {
        if (Columns.Count >= WorkspaceRules.MaxColumns)
        {
            return;
        }

        var column = CreateColumnViewModel(BuildColumnDocument($"Column {Columns.Count + 1}", MarkerMode.Numbers));
        Columns.Add(column);
        UpdateLastMultiColumnCount(Math.Max(LastMultiColumnCount, Columns.Count));
        SelectColumn(column);
        PromoteRawDocumentToLayoutIfNeeded();
        RecalculateDirty();
    }

    public bool RemoveSelectedColumn()
    {
        if (SelectedColumn is null || Columns.Count <= WorkspaceRules.MinColumns)
        {
            return false;
        }

        var columnToRemove = SelectedColumn;
        var index = Columns.IndexOf(columnToRemove);
        UnsubscribeColumn(columnToRemove);
        Columns.Remove(columnToRemove);
        SelectColumn(Columns[Math.Clamp(index - 1, 0, Columns.Count - 1)]);
        RecalculateDirty();
        return true;
    }

    public void MoveSelectedColumnLeft()
    {
        if (SelectedColumn is null)
        {
            return;
        }

        var index = Columns.IndexOf(SelectedColumn);
        if (index <= 0)
        {
            return;
        }

        Columns.Move(index, index - 1);
        RecalculateDirty();
    }

    public void MoveSelectedColumnRight()
    {
        if (SelectedColumn is null)
        {
            return;
        }

        var index = Columns.IndexOf(SelectedColumn);
        if (index < 0 || index >= Columns.Count - 1)
        {
            return;
        }

        Columns.Move(index, index + 1);
        RecalculateDirty();
    }

    public void SelectColumn(ColumnViewModel column)
    {
        SelectedColumn = column;
    }

    public ColumnViewModel? DuplicateSelectedColumn()
    {
        if (SelectedColumn is null || Columns.Count >= WorkspaceRules.MaxColumns)
        {
            return null;
        }

        var index = Columns.IndexOf(SelectedColumn);
        var duplicateDocument = SelectedColumn.ToDocument();
        duplicateDocument.Id = Guid.NewGuid();
        duplicateDocument.Title = $"{SelectedColumn.Title} Copy";
        var duplicate = CreateColumnViewModel(duplicateDocument);

        Columns.Insert(index + 1, duplicate);
        SelectColumn(duplicate);
        UpdateLastMultiColumnCount(Math.Max(LastMultiColumnCount, Columns.Count));
        PromoteRawDocumentToLayoutIfNeeded();
        RecalculateDirty();
        return duplicate;
    }

    public void ClearAllColumns()
    {
        foreach (var column in Columns)
        {
            column.Text = string.Empty;
        }

        RecalculateDirty();
    }

    public void ResetSelectedWidth()
    {
        if (SelectedColumn is null)
        {
            return;
        }

        SelectedColumn.ClearStoredWidth();
        SelectedColumn.IsWidthLocked = false;
        RecalculateDirty();
    }

    public void ResetAllWidths()
    {
        foreach (var column in Columns)
        {
            column.ClearStoredWidth();
            column.IsWidthLocked = false;
        }

        RecalculateDirty();
    }

    public bool ToggleSelectedWidthLock()
    {
        if (SelectedColumn is null)
        {
            return false;
        }

        SelectedColumn.IsWidthLocked = !SelectedColumn.IsWidthLocked;
        RecalculateDirty();
        return SelectedColumn.IsWidthLocked;
    }

    public bool UseSingleTextMode()
    {
        if (SelectedColumn is null || Columns.Count <= 1)
        {
            return false;
        }

        UpdateLastMultiColumnCount(Math.Max(LastMultiColumnCount, Columns.Count));
        var keepDocument = SelectedColumn.ToDocument();
        keepDocument.Width = null;
        keepDocument.IsWidthLocked = false;

        foreach (var column in Columns.ToList())
        {
            UnsubscribeColumn(column);
        }

        Columns.Clear();
        var single = CreateColumnViewModel(keepDocument);
        Columns.Add(single);
        SelectColumn(single);
        RecalculateDirty();
        return true;
    }

    public int UseColumnMode()
    {
        var targetCount = Math.Max(2, WorkspaceRules.ClampColumnCount(LastMultiColumnCount));
        if (Columns.Count >= targetCount)
        {
            return Columns.Count;
        }

        var markerCycle = new[] { MarkerMode.Numbers, MarkerMode.Bullets, MarkerMode.Checklist };
        while (Columns.Count < targetCount)
        {
            var markerMode = markerCycle[Columns.Count % markerCycle.Length];
            Columns.Add(CreateColumnViewModel(BuildColumnDocument($"Column {Columns.Count + 1}", markerMode)));
        }

        SelectColumn(Columns[Math.Clamp(Columns.Count - 1, 0, Columns.Count - 1)]);
        PromoteRawDocumentToLayoutIfNeeded();
        RecalculateDirty();
        return Columns.Count;
    }

    public void ApplyOpenedFileContext(string path, WorkspaceFileKind fileKind, bool saveAsRequired)
    {
        UpdateSaveReference(path, fileKind, saveAsRequired, captureAsBaseline: true);
    }

    public void MarkSaved(string? path, WorkspaceFileKind fileKind, bool saveAsRequired)
    {
        UpdateSaveReference(path, fileKind, saveAsRequired, captureAsBaseline: true);
    }

    public void MarkNeedsSave()
    {
        _savedComparisonSignature = null;
        IsDirty = true;
    }

    public void PersistRuntimeStateForRecovery()
    {
        foreach (var column in Columns)
        {
            column.StoredWidth = ColumnWidthLogic.ClampStoredWidth(column.StoredWidth);
        }
    }

    public RecoveryWorkspaceState CaptureRecoveryState()
    {
        return new RecoveryWorkspaceState
        {
            WorkspaceId = Id,
            FilePath = FilePath,
            FileKind = FileKind,
            SaveAsRequired = SaveAsRequired
        };
    }

    public void ApplyRecoveryState(RecoveryWorkspaceState state)
    {
        if (state.WorkspaceId != Guid.Empty && state.WorkspaceId != Id)
        {
            return;
        }

        UpdateSaveReference(state.FilePath, state.FileKind, state.SaveAsRequired, captureAsBaseline: true);
    }

    public WorkspaceDocument ToDocument()
    {
        return new WorkspaceDocument
        {
            Id = Id,
            Name = Name,
            ActiveColumnIndex = SelectedColumn is null ? 0 : Columns.IndexOf(SelectedColumn),
            LastMultiColumnCount = LastMultiColumnCount,
            Defaults = new EditorDefaults
            {
                FontFamily = FontFamilyName,
                FontFaceStyle = FontFaceStyle,
                FontSize = FontSize,
                LineStyle = LineStyle,
                ShowLineNumbers = ShowLineNumbers,
                WordWrap = WordWrap,
                LinedPaper = LinedPaper,
                SpellCheckEnabled = SpellCheckEnabled,
                LanguageTag = LanguageTag,
                ThemePreset = ThemePreset
            },
            Columns = Columns.Select(c => c.ToDocument()).ToList()
        };
    }

    private ColumnViewModel CreateColumnViewModel(ColumnDocument document)
    {
        var column = new ColumnViewModel(document, SelectColumn);
        column.ApplyWorkspaceFontDefaults(FontFamilyName, FontSize, FontFaceStyle);
        column.WorkspaceLineStyle = LineStyle;
        column.ShowLineNumbers = ShowLineNumbers;
        SubscribeColumn(column);
        return column;
    }

    private ColumnDocument BuildColumnDocument(string title, MarkerMode markerMode)
    {
        return new ColumnDocument
        {
            Title = title,
            PastePreset = PastePreset.None,
            UseDefaultFont = true,
            FontFamily = FontFamilyName,
            FontSize = FontSize,
            FontStyleName = EditorFontStyle == FontStyles.Italic ? "Italic" : "Normal",
            FontWeightName = EditorFontWeight == FontWeights.Bold ? "Bold" : "Normal",
            MarkerMode = markerMode,
            Text = string.Empty
        };
    }

    private void SubscribeColumn(ColumnViewModel column)
    {
        column.PropertyChanged += ColumnOnPropertyChanged;
    }

    private void ApplyWorkspaceFontDefaultsToColumns()
    {
        foreach (var column in Columns)
        {
            column.ApplyWorkspaceFontDefaults(FontFamilyName, FontSize, FontFaceStyle);
        }
    }

    private void ApplyWorkspaceLineStyleToColumns()
    {
        foreach (var column in Columns)
        {
            column.WorkspaceLineStyle = LineStyle;
        }
    }

    private void UnsubscribeColumn(ColumnViewModel column)
    {
        column.PropertyChanged -= ColumnOnPropertyChanged;
    }

    private void ColumnOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(e.PropertyName) &&
            !PersistedColumnPropertyNames.Contains(e.PropertyName))
        {
            return;
        }

        RecalculateDirty();
    }

    private void CaptureSavedComparisonSignature()
    {
        _savedComparisonSignature = ComputeComparisonSignature();
        IsDirty = false;
    }

    private void RecalculateDirty()
    {
        IsDirty = _savedComparisonSignature is null ||
            !string.Equals(_savedComparisonSignature, ComputeComparisonSignature(), StringComparison.Ordinal);
    }

    private string ComputeComparisonSignature()
    {
        var document = ToDocument();
        return FileKind switch
        {
            WorkspaceFileKind.RawText => Columns.FirstOrDefault()?.Text ?? string.Empty,
            WorkspaceFileKind.RawMarkdown => Columns.FirstOrDefault()?.Text ?? string.Empty,
            WorkspaceFileKind.TextExport => WorkspaceExporter.ToText(document),
            WorkspaceFileKind.MarkdownExport => WorkspaceExporter.ToMarkdown(document),
            _ => WorkspaceImportService.SerializeLayout(document)
        };
    }

    private void PromoteRawDocumentToLayoutIfNeeded()
    {
        if (Columns.Count <= 1)
        {
            return;
        }

        if (FileKind is WorkspaceFileKind.RawText or WorkspaceFileKind.RawMarkdown)
        {
            UpdateSaveReference(path: null, fileKind: WorkspaceFileKind.Layout, saveAsRequired: true, captureAsBaseline: false);
        }
    }

    private void UpdateSaveReference(string? path, WorkspaceFileKind fileKind, bool saveAsRequired, bool captureAsBaseline)
    {
        SetFilePath(path);
        SetFileKind(fileKind);
        SetSaveAsRequired(saveAsRequired);

        if (captureAsBaseline)
        {
            CaptureSavedComparisonSignature();
        }
        else
        {
            RecalculateDirty();
        }
    }

    private void SetFilePath(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
        if (SetProperty(ref _filePath, normalized, nameof(FilePath)))
        {
            OnPropertyChanged(nameof(CanDirectSave));
        }
    }

    private void SetFileKind(WorkspaceFileKind value)
    {
        if (SetProperty(ref _fileKind, value, nameof(FileKind)))
        {
            OnPropertyChanged(nameof(FileKindDisplay));
        }
    }

    private void SetSaveAsRequired(bool value)
    {
        if (SetProperty(ref _saveAsRequired, value, nameof(SaveAsRequired)))
        {
            OnPropertyChanged(nameof(CanDirectSave));
        }
    }

    private void UpdateLastMultiColumnCount(int value)
    {
        var normalizedValue = WorkspaceRules.ClampColumnCount(value <= 1 ? 3 : value);
        if (_lastMultiColumnCount == normalizedValue)
        {
            return;
        }

        _lastMultiColumnCount = normalizedValue;
        RecalculateDirty();
    }
}
