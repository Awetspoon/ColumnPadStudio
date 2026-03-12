using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ColumnPadStudio.Domain.Lists;

namespace ColumnPadStudio.ViewModels;

public enum PasteListPreset
{
    None,
    Bullets,
    Checklist
}

public enum LineMarkerMode
{
    Numbers,
    Bullets,
    Checklist
}

public sealed class ColumnViewModel : NotifyBase
{
    private string _title = "Column";
    private string _text = "";
    private int? _widthPx;

    private bool _showLineNumbers = true;
    private bool _wordWrap;
    private string _editorFontFamily = "Consolas";
    private double _editorFontSize = 13;
    private FontStyle _editorFontStyle = FontStyles.Normal;
    private FontWeight _editorFontWeight = FontWeights.Normal;

    private bool _isWidthLocked;
    private bool _canMoveLeft;
    private bool _canMoveRight;
    private bool _isActive;
    private bool _isRenaming;
    private PasteListPreset _pastePreset = PasteListPreset.None;
    private LineMarkerMode _lineMarkerMode = LineMarkerMode.Numbers;
    private bool _useDefaultFont = true;
    private int _lineCount = 1;
    private int? _visibleLineCount;
    private int _wordCount;
    private int _checklistTotal;
    private int _checklistDone;
    private string _metricsText = "0 words | 1 line";
    private HashSet<int> _checkedChecklistLineIndexes = [];

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public string Title
    {
        get => _title;
        set => Set(ref _title, value);
    }

    public string Text
    {
        get => _text;
        set
        {
            _visibleLineCount = null;
            Set(ref _text, value ?? string.Empty);
            RecomputeDerivedMetrics();
        }
    }

    public int? WidthPx
    {
        get => _widthPx;
        set => Set(ref _widthPx, value);
    }

    public bool IsWidthLocked
    {
        get => _isWidthLocked;
        set
        {
            Set(ref _isWidthLocked, value);
            OnPropertyChanged(nameof(WidthLockActionLabel));
            OnPropertyChanged(nameof(WidthLockActionToolTip));
        }
    }

    public bool CanMoveLeft
    {
        get => _canMoveLeft;
        set => Set(ref _canMoveLeft, value);
    }

    public bool CanMoveRight
    {
        get => _canMoveRight;
        set => Set(ref _canMoveRight, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => Set(ref _isActive, value);
    }

    public PasteListPreset PastePreset
    {
        get => _pastePreset;
        set => Set(ref _pastePreset, value);
    }

    public LineMarkerMode LineMarkerMode
    {
        get => _lineMarkerMode;
        set
        {
            if (_lineMarkerMode == value)
                return;

            _lineMarkerMode = value;
            OnPropertyChanged();
            RecomputeDerivedMetrics();
        }
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set => Set(ref _isRenaming, value);
    }

    public bool UseDefaultFont
    {
        get => _useDefaultFont;
        set => Set(ref _useDefaultFont, value);
    }

    public bool ShowLineNumbers
    {
        get => _showLineNumbers;
        set
        {
            Set(ref _showLineNumbers, value);
            OnPropertyChanged(nameof(ShowLineNumbersVisibility));
            OnPropertyChanged(nameof(LineNumberColumnWidth));
        }
    }

    public bool WordWrap
    {
        get => _wordWrap;
        set
        {
            Set(ref _wordWrap, value);
            OnPropertyChanged(nameof(TextWrappingMode));
            OnPropertyChanged(nameof(HorizontalScrollBarMode));
        }
    }

    public string EditorFontFamily
    {
        get => _editorFontFamily;
        set => Set(ref _editorFontFamily, string.IsNullOrWhiteSpace(value) ? "Consolas" : value);
    }

    public double EditorFontSize
    {
        get => _editorFontSize;
        set
        {
            Set(ref _editorFontSize, Math.Clamp(value, 8.0, 40.0));
            OnPropertyChanged(nameof(LineNumberFontSize));
            OnPropertyChanged(nameof(EditorLineHeight));
        }
    }

    public FontStyle EditorFontStyle
    {
        get => _editorFontStyle;
        set => Set(ref _editorFontStyle, value);
    }

    public FontWeight EditorFontWeight
    {
        get => _editorFontWeight;
        set => Set(ref _editorFontWeight, value);
    }

    public Visibility ShowLineNumbersVisibility => ShowLineNumbers ? Visibility.Visible : Visibility.Collapsed;
    public GridLength LineNumberColumnWidth => ShowLineNumbers ? new GridLength(56) : new GridLength(0);
    public TextWrapping TextWrappingMode => WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
    public ScrollBarVisibility HorizontalScrollBarMode => WordWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

    public string WidthLockActionLabel => IsWidthLocked ? "Allow Resize" : "Freeze Width";
    public string WidthLockActionToolTip => IsWidthLocked
        ? "This column width is frozen. Click to allow drag resizing again."
        : "Freeze this column width so the splitter cannot resize it.";

    public double LineNumberFontSize => Math.Max(8.0, EditorFontSize);
    public double EditorLineHeight => Math.Max(15.0, Math.Round((EditorFontSize / 13.0) * 23.0, 2));

    public int LineCount
    {
        get => _lineCount;
        private set => Set(ref _lineCount, value);
    }

    public int WordCount
    {
        get => _wordCount;
        private set => Set(ref _wordCount, value);
    }

    public int ChecklistTotal
    {
        get => _checklistTotal;
        private set => Set(ref _checklistTotal, value);
    }

    public int ChecklistDone
    {
        get => _checklistDone;
        private set => Set(ref _checklistDone, value);
    }

    public string MetricsText
    {
        get => _metricsText;
        private set => Set(ref _metricsText, value);
    }

    public IReadOnlyList<int> GetCheckedChecklistLineIndexes()
    {
        var sorted = _checkedChecklistLineIndexes.ToList();
        sorted.Sort();
        return sorted;
    }

    public void SetCheckedChecklistLineIndexes(IEnumerable<int>? lineIndexes)
    {
        var next = new HashSet<int>();
        if (lineIndexes is not null)
        {
            foreach (var lineIndex in lineIndexes)
            {
                if (lineIndex >= 0)
                    next.Add(lineIndex);
            }
        }

        if (_checkedChecklistLineIndexes.SetEquals(next))
            return;

        _checkedChecklistLineIndexes = next;
        TrimChecklistLineIndexesToBounds();
        RecomputeDerivedMetrics();
    }

    public bool IsChecklistLineChecked(int lineIndex)
        => lineIndex >= 0 && _checkedChecklistLineIndexes.Contains(lineIndex);

    public void ToggleChecklistLineChecked(int lineIndex)
    {
        if (lineIndex < 0)
            return;

        if (!_checkedChecklistLineIndexes.Remove(lineIndex))
            _checkedChecklistLineIndexes.Add(lineIndex);

        TrimChecklistLineIndexesToBounds();
        RecomputeDerivedMetrics();
    }

    public void ShiftChecklistLineIndexes(int startLineIndexInclusive, int delta)
    {
        if (delta == 0 || _checkedChecklistLineIndexes.Count == 0)
            return;

        var remapped = new HashSet<int>();
        foreach (var index in _checkedChecklistLineIndexes)
        {
            if (index < startLineIndexInclusive)
            {
                remapped.Add(index);
                continue;
            }

            var shifted = index + delta;
            if (shifted >= 0)
                remapped.Add(shifted);
        }

        if (_checkedChecklistLineIndexes.SetEquals(remapped))
            return;

        _checkedChecklistLineIndexes = remapped;
        TrimChecklistLineIndexesToBounds();
        RecomputeDerivedMetrics();
    }

    public void SetVisibleLineCount(int lineCount)
    {
        var normalized = Math.Max(1, lineCount);
        if (_visibleLineCount == normalized)
            return;

        _visibleLineCount = normalized;
        UpdateMetricsText();
    }

    private void UpdateMetricsText()
    {
        var displayedLines = _visibleLineCount ?? LineCount;
        MetricsText = ChecklistTotal > 0
            ? $"{WordCount} words | {displayedLines} lines | {ChecklistDone}/{ChecklistTotal} done"
            : $"{WordCount} words | {displayedLines} lines";
    }

    private void RecomputeDerivedMetrics()
    {
        // Keep all text-derived metrics in one pass for consistency.
        var text = _text ?? string.Empty;

        var lines = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                lines++;
        }

        LineCount = lines;
        TrimChecklistLineIndexesToBounds();

        var wordCount = 0;
        var inWord = false;
        for (int i = 0; i < text.Length; i++)
        {
            var isWhite = char.IsWhiteSpace(text[i]);
            if (!isWhite && !inWord)
            {
                wordCount++;
                inWord = true;
            }
            else if (isWhite)
            {
                inWord = false;
            }
        }

        WordCount = wordCount;

        if (LineMarkerMode == LineMarkerMode.Checklist)
        {
            var normalizedText = text
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);

            var splitLines = normalizedText.Split('\n');
            var checklistTotal = 0;
            var checklistDone = 0;

            for (var i = 0; i < splitLines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(splitLines[i]))
                    continue;

                checklistTotal++;
                if (_checkedChecklistLineIndexes.Contains(i))
                    checklistDone++;
            }

            ChecklistTotal = checklistTotal;
            ChecklistDone = checklistDone;
        }
        else
        {
            var checklistMetrics = ChecklistMetricsCalculator.Compute(text);
            ChecklistTotal = checklistMetrics.Total;
            ChecklistDone = checklistMetrics.Done;
        }

        UpdateMetricsText();
    }

    private void TrimChecklistLineIndexesToBounds()
    {
        var maxIndex = Math.Max(0, LineCount - 1);
        _checkedChecklistLineIndexes.RemoveWhere(index => index < 0 || index > maxIndex);
    }
}
