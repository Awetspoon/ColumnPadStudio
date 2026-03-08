using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColumnPadStudio.ViewModels;

public enum PasteListPreset
{
    None,
    Bullets,
    Checklist
}

public sealed class ColumnViewModel : NotifyBase
{
    private const string ChecklistUncheckedPrefix = "\u2610 ";
    private const string ChecklistCheckedPrefix = "\u2611 ";

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
    private bool _useDefaultFont = true;

    private string _lineNumbersText = "1\n";
    private int _lineCount = 1;
    private int _wordCount;
    private int _checklistTotal;
    private int _checklistDone;
    private string _metricsText = "0 words | 1 line";

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
    public TextWrapping TextWrappingMode => WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
    public ScrollBarVisibility HorizontalScrollBarMode => WordWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

    public string WidthLockActionLabel => IsWidthLocked ? "Allow Resize" : "Freeze Width";
    public string WidthLockActionToolTip => IsWidthLocked
        ? "This column width is frozen. Click to allow drag resizing again."
        : "Freeze this column width so the splitter cannot resize it.";

    public double LineNumberFontSize => Math.Max(8.0, EditorFontSize - 1.0);

    public string LineNumbersText
    {
        get => _lineNumbersText;
        private set => Set(ref _lineNumbersText, value);
    }

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

    private void RecomputeDerivedMetrics()
    {
        // Keep metrics and line numbers in one pass for consistency.
        var text = _text ?? string.Empty;

        var lines = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                lines++;
        }

        LineCount = lines;

        var sb = new StringBuilder(lines * 3);
        for (int i = 1; i <= lines; i++)
            sb.Append(i).Append('\n');
        LineNumbersText = sb.ToString();

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

        var checklistTotal = 0;
        var checklistDone = 0;
        var logicalLines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var line in logicalLines)
        {
            if (line.StartsWith(ChecklistUncheckedPrefix, StringComparison.Ordinal) || line.StartsWith("- [ ] ", StringComparison.Ordinal))
            {
                checklistTotal++;
                continue;
            }

            if (line.StartsWith(ChecklistCheckedPrefix, StringComparison.Ordinal) ||
                line.StartsWith("- [x] ", StringComparison.Ordinal) ||
                line.StartsWith("- [X] ", StringComparison.Ordinal))
            {
                checklistTotal++;
                checklistDone++;
            }
        }

        ChecklistTotal = checklistTotal;
        ChecklistDone = checklistDone;

        MetricsText = checklistTotal > 0
            ? $"{wordCount} words | {lines} lines | {checklistDone}/{checklistTotal} done"
            : $"{wordCount} words | {lines} lines";
    }
}

