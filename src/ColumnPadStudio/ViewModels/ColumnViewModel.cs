using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Domain.Text;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.Services;

namespace ColumnPadStudio.ViewModels;

public sealed partial class ColumnViewModel : NotifyBase
{
    private string _title = "Column";
    private string _text = "";
    private int? _widthPx;

    private bool _showLineNumbers = true;
    private int _sharedGutterWidthPx = MainViewModel.DefaultGutterWidthPx;
    private bool _wordWrap;
    private string _editorFontFamily = "Consolas";
    private double _editorFontSize = 13;
    private FontStyle _editorFontStyle = FontStyles.Normal;
    private FontWeight _editorFontWeight = FontWeights.Normal;
    private string _editorTextColor = ColumnTextColorService.ThemeDefault;
    private SolidColorBrush? _customEditorTextColorBrush;

    private bool _isWidthLocked;
    private bool _canMoveLeft;
    private bool _canMoveRight;
    private bool _isActive;
    private bool _isRenaming;
    private bool _isStandaloneDocument;
    private bool _isWidthManagementEnabled = true;
    private PasteListPreset _pastePreset = PasteListPreset.None;
    private LineMarkerMode _lineMarkerMode = LineMarkerMode.Numbers;
    private bool _useDefaultFont = true;
    private int _lineCount = 1;
    private int? _visibleLineCount;
    private int _wordCount;
    private int _checklistTotal;
    private int _checklistDone;
    private int _gutterStateVersion;
    private string _metricsText = "0 words | 1 line";
    private HashSet<int> _checkedChecklistLineIndexes = [];

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public ObservableCollection<ColumnImageViewModel> Images { get; } = new();

    public ColumnViewModel()
    {
        Images.CollectionChanged += Images_CollectionChanged;
    }

    public string Title
    {
        get => _title;
        set => Set(ref _title, DisplayTextRules.CleanSingleLineLabel(value, "Column"));
    }

    public string Text
    {
        get => _text;
        set
        {
            var nextText = value ?? string.Empty;
            if (LineMarkerMode == LineMarkerMode.Checklist)
                RemapChecklistLineIndexes(_text, nextText);

            _visibleLineCount = null;
            Set(ref _text, nextText);
            RecomputeDerivedMetrics();
        }
    }

    public int? WidthPx
    {
        get => _widthPx;
        set => Set(ref _widthPx, NormalizeWidth(value));
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
            InvalidateGutterState();
            RecomputeDerivedMetrics();
        }
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set => Set(ref _isRenaming, value);
    }

    public bool IsStandaloneDocument
    {
        get => _isStandaloneDocument;
        set
        {
            if (_isStandaloneDocument == value)
                return;

            _isStandaloneDocument = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WidthLockActionToolTip));
        }
    }

    public bool IsWidthManagementEnabled
    {
        get => _isWidthManagementEnabled;
        set
        {
            if (_isWidthManagementEnabled == value)
                return;

            _isWidthManagementEnabled = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(WidthLockActionToolTip));
        }
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

    public string EditorTextColor
    {
        get => _editorTextColor;
        set
        {
            var normalized = ColumnTextColorService.Normalize(value);
            if (string.Equals(_editorTextColor, normalized, StringComparison.Ordinal))
                return;

            _editorTextColor = normalized;
            _customEditorTextColorBrush = ColumnTextColorService.CreateCustomBrush(normalized);
            OnPropertyChanged();
            OnPropertyChanged(nameof(CustomEditorTextColorBrush));
            OnPropertyChanged(nameof(HasCustomEditorTextColor));
        }
    }

    public SolidColorBrush? CustomEditorTextColorBrush => _customEditorTextColorBrush;
    public bool HasCustomEditorTextColor => _customEditorTextColorBrush is not null;

    public Visibility ShowLineNumbersVisibility => ShowLineNumbers ? Visibility.Visible : Visibility.Collapsed;
    public GridLength LineNumberColumnWidth => ShowLineNumbers ? new GridLength(_sharedGutterWidthPx) : new GridLength(0);
    public TextWrapping TextWrappingMode => WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
    public ScrollBarVisibility HorizontalScrollBarMode => WordWrap ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;

    public string WidthLockActionLabel => IsWidthLocked ? "Allow Resize" : "Freeze Width";
    public string WidthLockActionToolTip => !IsWidthManagementEnabled
        ? IsStandaloneDocument
            ? "Single Text Mode fills the window. Switch to Column Mode to resize or freeze columns."
            : "Choose Standard or Custom column width to resize and freeze columns."
        : IsWidthLocked
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

    public int GutterStateVersion => _gutterStateVersion;

    public string MetricsText
    {
        get => _metricsText;
        private set => Set(ref _metricsText, value);
    }

    public void SetVisibleLineCount(int lineCount)
    {
        var normalized = Math.Max(1, lineCount);
        if (_visibleLineCount == normalized)
            return;

        _visibleLineCount = normalized;
        UpdateMetricsText();
    }

    internal void SetSharedGutterWidth(int widthPx)
    {
        var normalized = Math.Clamp(
            widthPx,
            MainViewModel.MinimumGutterWidthPx,
            MainViewModel.MaximumGutterWidthPx);
        if (_sharedGutterWidthPx == normalized)
            return;

        _sharedGutterWidthPx = normalized;
        OnPropertyChanged(nameof(LineNumberColumnWidth));
    }

    private static int? NormalizeWidth(int? widthPx)
    {
        if (widthPx is null or <= 0)
            return null;

        return WorkspaceConstraints.ClampColumnWidth(widthPx.Value);
    }

    private void InvalidateGutterState()
    {
        unchecked
        {
            _gutterStateVersion++;
        }

        OnPropertyChanged(nameof(GutterStateVersion));
    }

}
