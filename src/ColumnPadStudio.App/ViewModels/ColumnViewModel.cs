using System.Windows;
using System.Windows.Input;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.App.ViewModels;

public sealed class ColumnViewModel : ObservableObject
{
    private readonly Action<ColumnViewModel> _selectAction;
    private const string MetricSeparator = "\u00B7";
    private string _title;
    private string _text;
    private bool _isSelected;
    private MarkerMode _markerMode;
    private PastePreset _pastePreset;
    private bool _useDefaultFont;
    private string _fontFamilyName;
    private double _fontSize;
    private string _fontFaceStyle;
    private string _workspaceFontFamilyName = "Consolas";
    private double _workspaceFontSize = 13;
    private string _workspaceFontFaceStyle = "Regular";
    private EditorLineStyle _workspaceLineStyle = EditorLineStyle.StandardRuled;
    private double? _storedWidth;
    private bool _isWidthLocked;
    private bool _showLineNumbers = true;
    private HashSet<int> _checkedLines;
    private int _selectionStart;
    private int _selectionLength;
    private int _requestedSelectionStart = -1;
    private int _requestedSelectionLength;
    private int _checkedLinesRevision;

    public ColumnViewModel(ColumnDocument document, Action<ColumnViewModel> selectAction)
    {
        _selectAction = selectAction;
        Id = document.Id;
        _title = document.Title;
        _text = document.Text;
        _markerMode = document.MarkerMode;
        _pastePreset = document.PastePreset;
        _useDefaultFont = document.UseDefaultFont;
        _fontFamilyName = string.IsNullOrWhiteSpace(document.FontFamily) ? "Consolas" : document.FontFamily;
        _fontSize = WorkspaceRules.ClampFontSize(document.FontSize <= 0 ? 13 : document.FontSize);
        _fontFaceStyle = ComposeFontFaceStyle(document.FontStyleName, document.FontWeightName);
        _storedWidth = ColumnWidthLogic.ClampStoredWidth(document.Width);
        _isWidthLocked = document.IsWidthLocked;
        _checkedLines = new HashSet<int>(document.CheckedLines);
        SelectCommand = new RelayCommand(() => _selectAction(this));
    }

    public Guid Id { get; }
    public ICommand SelectCommand { get; }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value))
            {
                if (NormalizeCheckedLines())
                {
                    OnPropertyChanged(nameof(CheckedLinesRevision));
                }

                RaiseDerivedStateChanged();
            }
        }
    }

    public MarkerMode MarkerMode
    {
        get => _markerMode;
        set
        {
            if (SetProperty(ref _markerMode, value))
            {
                RaiseDerivedStateChanged();
            }
        }
    }

    public PastePreset PastePreset
    {
        get => _pastePreset;
        set => SetProperty(ref _pastePreset, value);
    }

    public bool UseDefaultFont
    {
        get => _useDefaultFont;
        set
        {
            if (SetProperty(ref _useDefaultFont, value))
            {
                OnPropertyChanged(nameof(IsCustomFontEnabled));
                RaiseFontStateChanged();
            }
        }
    }

    public bool IsCustomFontEnabled => !UseDefaultFont;

    public string FontFamilyName
    {
        get => _fontFamilyName;
        set
        {
            if (SetProperty(ref _fontFamilyName, value))
            {
                RaiseFontStateChanged();
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
                RaiseFontStateChanged();
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
                RaiseFontStateChanged();
            }
        }
    }

    public string EffectiveFontFamilyName => UseDefaultFont ? _workspaceFontFamilyName : FontFamilyName;
    public double EffectiveFontSize => UseDefaultFont ? _workspaceFontSize : FontSize;
    public string EffectiveFontFaceStyle => UseDefaultFont ? _workspaceFontFaceStyle : FontFaceStyle;
    public EditorLineStyle WorkspaceLineStyle
    {
        get => _workspaceLineStyle;
        set => SetProperty(ref _workspaceLineStyle, value);
    }

    public FontStyle EffectiveFontStyle => EffectiveFontFaceStyle.Contains("Italic", StringComparison.OrdinalIgnoreCase)
        ? FontStyles.Italic
        : FontStyles.Normal;

    public FontWeight EffectiveFontWeight => EffectiveFontFaceStyle.Contains("Bold", StringComparison.OrdinalIgnoreCase)
        ? FontWeights.Bold
        : FontWeights.Normal;

    public double? StoredWidth
    {
        get => _storedWidth;
        set
        {
            var clampedWidth = ColumnWidthLogic.ClampStoredWidth(value);
            if (Nullable.Equals(_storedWidth, clampedWidth))
            {
                return;
            }

            _storedWidth = clampedWidth;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasStoredWidth));
        }
    }

    public bool HasStoredWidth => StoredWidth.HasValue;
    public bool HasMeaningfulEdits =>
        !string.IsNullOrWhiteSpace(Text) ||
        MarkerMode != MarkerMode.Numbers ||
        PastePreset != PastePreset.None ||
        !UseDefaultFont ||
        HasStoredWidth ||
        IsWidthLocked ||
        _checkedLines.Count > 0;

    public bool IsWidthLocked
    {
        get => _isWidthLocked;
        set => SetProperty(ref _isWidthLocked, value);
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            if (SetProperty(ref _showLineNumbers, value))
            {
                RaiseDerivedStateChanged();
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public int RequestedSelectionStart
    {
        get => _requestedSelectionStart;
        private set => SetProperty(ref _requestedSelectionStart, value);
    }

    public int RequestedSelectionLength
    {
        get => _requestedSelectionLength;
        private set => SetProperty(ref _requestedSelectionLength, value);
    }

    public int SelectionStart
    {
        get => _selectionStart;
        private set
        {
            if (SetProperty(ref _selectionStart, value))
            {
                OnPropertyChanged(nameof(SelectionEnd));
            }
        }
    }

    public int SelectionLength
    {
        get => _selectionLength;
        private set
        {
            if (SetProperty(ref _selectionLength, value))
            {
                OnPropertyChanged(nameof(SelectionEnd));
            }
        }
    }

    public int SelectionEnd => SelectionStart + SelectionLength;
    public int LineCount => TextMetrics.GetLineCount(Text);
    public int WordCount => TextMetrics.GetWordCount(Text);
    public int ChecklistTotal => ChecklistMetrics.GetMetrics(MarkerMode, Text, _checkedLines).Total;
    public int ChecklistDone => ChecklistMetrics.GetMetrics(MarkerMode, Text, _checkedLines).Done;
    public int CheckedLinesRevision => _checkedLinesRevision;

    public string MetricsText
    {
        get
        {
            var metrics = $"{LineCount} lines {MetricSeparator} {WordCount} words";
            if (ChecklistTotal > 0 || MarkerMode == MarkerMode.Checklist)
            {
                metrics += $" {MetricSeparator} {ChecklistDone}/{ChecklistTotal} done";
            }

            return metrics;
        }
    }

    public string GutterText => MarkerFormatter.BuildGutter(MarkerMode, Text, _checkedLines, ShowLineNumbers);

    public void ApplyWorkspaceFontDefaults(string fontFamilyName, double fontSize, string fontFaceStyle)
    {
        var familyChanged = !string.Equals(_workspaceFontFamilyName, fontFamilyName, StringComparison.Ordinal);
        var sizeChanged = Math.Abs(_workspaceFontSize - fontSize) > double.Epsilon;
        var styleChanged = !string.Equals(_workspaceFontFaceStyle, fontFaceStyle, StringComparison.Ordinal);

        _workspaceFontFamilyName = fontFamilyName;
        _workspaceFontSize = WorkspaceRules.ClampFontSize(fontSize);
        _workspaceFontFaceStyle = fontFaceStyle;

        if (UseDefaultFont && (familyChanged || sizeChanged || styleChanged))
        {
            RaiseFontStateChanged();
        }
    }

    public void ToggleChecklistLine(int zeroBasedLineIndex)
    {
        if (zeroBasedLineIndex < 0 || zeroBasedLineIndex >= LineCount)
        {
            return;
        }

        if (_checkedLines.Contains(zeroBasedLineIndex))
        {
            _checkedLines.Remove(zeroBasedLineIndex);
        }
        else
        {
            _checkedLines.Add(zeroBasedLineIndex);
        }

        RaiseCheckedLinesChanged();
    }

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public void ClearStoredWidth()
    {
        StoredWidth = null;
    }

    public void ShiftCheckedLinesAfterInsertedLine(int afterZeroBasedLineIndex)
    {
        if (_checkedLines.Count == 0)
        {
            return;
        }

        var shifted = new HashSet<int>();
        foreach (var line in _checkedLines)
        {
            shifted.Add(line > afterZeroBasedLineIndex ? line + 1 : line);
        }

        _checkedLines = shifted;
        RaiseCheckedLinesChanged();
    }

    public void UpdateSelection(int start, int length)
    {
        var boundedStart = Math.Clamp(start, 0, Text.Length);
        var boundedLength = Math.Clamp(length, 0, Text.Length - boundedStart);
        SelectionStart = boundedStart;
        SelectionLength = boundedLength;
    }

    public int ToggleChecklistSelection()
    {
        var (startLineIndex, endLineIndex) = TextSelectionLogic.GetSelectedLineRange(Text, SelectionStart, SelectionLength);
        if (MarkerMode != MarkerMode.Checklist)
        {
            MarkerMode = MarkerMode.Checklist;
        }

        var toggledCount = 0;
        for (var lineIndex = startLineIndex; lineIndex <= endLineIndex; lineIndex++)
        {
            if (_checkedLines.Contains(lineIndex))
            {
                _checkedLines.Remove(lineIndex);
            }
            else
            {
                _checkedLines.Add(lineIndex);
            }

            toggledCount++;
        }

        if (toggledCount > 0)
        {
            RaiseCheckedLinesChanged();
        }

        return toggledCount;
    }

    public bool ApplyPresetToSelection(PastePreset preset)
    {
        var transformedText = PasteTransformLogic.ApplyPresetToSelectedLines(Text, SelectionStart, SelectionLength, preset);
        if (string.Equals(transformedText, Text, StringComparison.Ordinal))
        {
            return false;
        }

        Text = transformedText;
        return true;
    }

    public void RequestSelection(int start, int length)
    {
        var boundedStart = Math.Clamp(start, 0, Text.Length);
        RequestedSelectionStart = boundedStart;
        RequestedSelectionLength = Math.Clamp(length, 0, Text.Length - boundedStart);
    }

    public void ClearRequestedSelection()
    {
        RequestedSelectionStart = -1;
        RequestedSelectionLength = 0;
    }

    public ColumnDocument ToDocument()
    {
        return new ColumnDocument
        {
            Id = Id,
            Title = Title,
            Text = Text,
            MarkerMode = MarkerMode,
            PastePreset = PastePreset,
            UseDefaultFont = UseDefaultFont,
            FontFamily = EffectiveFontFamilyName,
            FontSize = EffectiveFontSize,
            FontStyleName = EffectiveFontStyle == FontStyles.Italic ? "Italic" : "Normal",
            FontWeightName = EffectiveFontWeight == FontWeights.Bold ? "Bold" : "Normal",
            Width = StoredWidth,
            IsWidthLocked = IsWidthLocked,
            CheckedLines = new HashSet<int>(_checkedLines.OrderBy(i => i))
        };
    }

    private static string ComposeFontFaceStyle(string fontStyleName, string fontWeightName)
    {
        var isBold = string.Equals(fontWeightName, "Bold", StringComparison.OrdinalIgnoreCase);
        var isItalic = string.Equals(fontStyleName, "Italic", StringComparison.OrdinalIgnoreCase);

        return (isBold, isItalic) switch
        {
            (true, true) => "Bold Italic",
            (true, false) => "Bold",
            (false, true) => "Italic",
            _ => "Regular"
        };
    }

    private bool NormalizeCheckedLines()
    {
        var previous = new HashSet<int>(_checkedLines);
        var lineCount = LineCount;
        _checkedLines.RemoveWhere(i => i < 0 || i >= lineCount);
        _checkedLines = new HashSet<int>(_checkedLines.OrderBy(i => i));
        UpdateSelection(SelectionStart, SelectionLength);

        if (!previous.SetEquals(_checkedLines))
        {
            _checkedLinesRevision++;
            return true;
        }

        return false;
    }

    private void RaiseDerivedStateChanged()
    {
        OnPropertyChanged(nameof(GutterText));
        OnPropertyChanged(nameof(LineCount));
        OnPropertyChanged(nameof(WordCount));
        OnPropertyChanged(nameof(ChecklistTotal));
        OnPropertyChanged(nameof(ChecklistDone));
        OnPropertyChanged(nameof(MetricsText));
    }

    private void RaiseCheckedLinesChanged()
    {
        _checkedLinesRevision++;
        OnPropertyChanged(nameof(CheckedLinesRevision));
        RaiseDerivedStateChanged();
    }

    private void RaiseFontStateChanged()
    {
        OnPropertyChanged(nameof(IsCustomFontEnabled));
        OnPropertyChanged(nameof(EffectiveFontFamilyName));
        OnPropertyChanged(nameof(EffectiveFontSize));
        OnPropertyChanged(nameof(EffectiveFontFaceStyle));
        OnPropertyChanged(nameof(EffectiveFontStyle));
        OnPropertyChanged(nameof(EffectiveFontWeight));
    }
}
