using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.ViewModels;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl : UserControl
{
    public event EventHandler? EditorFocused;
    public event EventHandler? LockWidthRequested;
    public event EventHandler? MoveLeftRequested;
    public event EventHandler? MoveRightRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? ResetWidthRequested;
    public event EventHandler? ResetAllWidthsRequested;
    public event EventHandler? ResizeRequested;
    public event EventHandler? SetFontFamilyRequested;
    public event EventHandler? IncreaseFontRequested;
    public event EventHandler? DecreaseFontRequested;
    public event EventHandler? ToggleBoldRequested;
    public event EventHandler? ToggleItalicRequested;
    public event EventHandler? ResetFontRequested;

    private ScrollViewer? _editorScrollViewer;
    private ScrollViewer? _lineNumberScrollViewer;
    private bool _isSyncingLineNumberScroll;
    private bool _lineNumberRefreshPending;
    private int _lastRenderedLineNumberCount = -1;
    private int _gutterContextLineIndex = -1;
    private ColumnViewModel? _observedVm;

    public ColumnEditorControl()
    {
        InitializeComponent();
        Loaded += ColumnEditorControl_Loaded;
        Unloaded += ColumnEditorControl_Unloaded;
        DataContextChanged += ColumnEditorControl_DataContextChanged;
    }

    public int SelectionStart => Editor.SelectionStart;
    public int SelectionLength => Editor.SelectionLength;

    private ColumnViewModel? VM => DataContext as ColumnViewModel;

    public void FocusEditor()
    {
        Editor.Focus();
        Editor.CaretIndex = Math.Clamp(Editor.CaretIndex, 0, Editor.Text.Length);
    }

    public void FocusAndSelectRange(int start, int length)
    {
        var textLength = Editor.Text.Length;
        var safeStart = Math.Clamp(start, 0, textLength);
        var safeLength = Math.Clamp(length, 0, textLength - safeStart);

        Editor.Focus();
        Editor.Select(safeStart, safeLength);
        var line = Editor.GetLineIndexFromCharacterIndex(safeStart);
        Editor.ScrollToLine(line);
    }

    public void ApplyBulletsToSelection() => SetLineMarkerMode(LineMarkerMode.Bullets);
    public void ApplyChecklistToSelection() => SetLineMarkerMode(LineMarkerMode.Checklist);
    public void ToggleChecklistChecksInSelection() => ToggleChecklistChecksForSelection();

    public bool ClearSelection(bool focusEditor = true)
    {
        if (Editor.SelectionLength <= 0)
            return false;

        var caretIndex = Editor.SelectionStart + Editor.SelectionLength;
        Editor.Select(caretIndex, 0);
        if (focusEditor)
            Editor.Focus();

        return true;
    }

    private void ColumnEditorControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_observedVm is not null)
            _observedVm.PropertyChanged -= ObservedVm_PropertyChanged;

        _observedVm = e.NewValue as ColumnViewModel;
        if (_observedVm is not null)
            _observedVm.PropertyChanged += ObservedVm_PropertyChanged;

        _lastRenderedLineNumberCount = -1;
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void ObservedVm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ColumnViewModel.LineMarkerMode) or nameof(ColumnViewModel.ChecklistDone) or nameof(ColumnViewModel.ShowLineNumbers))
        {
            _lastRenderedLineNumberCount = -1;
            QueueLineNumberRefresh();
        }
    }

    private void Editor_GotFocus(object sender, RoutedEventArgs e)
    {
        EditorFocused?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnEditorControl_Loaded(object sender, RoutedEventArgs e)
    {
        AttachEditorScrollViewer();
        AttachLineNumberScrollViewer();
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void ColumnEditorControl_Unloaded(object sender, RoutedEventArgs e)
    {
        DetachEditorScrollViewer();
        DetachLineNumberScrollViewer();

        if (_observedVm is not null)
            _observedVm.PropertyChanged -= ObservedVm_PropertyChanged;
    }

    private void Editor_TextChanged(object sender, TextChangedEventArgs e)
    {
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void Editor_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueLineNumberRefresh();
        SyncLineNumberScrollWithEditor();
    }

    private void AttachEditorScrollViewer()
    {
        if (_editorScrollViewer is not null)
            return;

        _editorScrollViewer = FindDescendant<ScrollViewer>(Editor);
        if (_editorScrollViewer is null)
            return;

        _editorScrollViewer.ScrollChanged += EditorScrollViewer_ScrollChanged;
    }

    private void DetachEditorScrollViewer()
    {
        if (_editorScrollViewer is null)
            return;

        _editorScrollViewer.ScrollChanged -= EditorScrollViewer_ScrollChanged;
        _editorScrollViewer = null;
    }

    private void AttachLineNumberScrollViewer()
    {
        if (_lineNumberScrollViewer is not null)
            return;

        _lineNumberScrollViewer = FindDescendant<ScrollViewer>(LineNumbers);
    }

    private void DetachLineNumberScrollViewer()
    {
        _lineNumberScrollViewer = null;
    }

    private void EditorScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.VerticalChange == 0 && e.ExtentHeightChange == 0)
            return;

        if (e.ExtentHeightChange != 0)
            QueueLineNumberRefresh();

        SyncLineNumberScroll(e.VerticalOffset);
    }

    private void SyncLineNumberScrollWithEditor()
    {
        AttachEditorScrollViewer();
        AttachLineNumberScrollViewer();
        SyncLineNumberScroll(_editorScrollViewer?.VerticalOffset ?? 0);
    }

    private void SyncLineNumberScroll(double verticalOffset)
    {
        if (_isSyncingLineNumberScroll)
            return;

        _isSyncingLineNumberScroll = true;
        try
        {
            _lineNumberScrollViewer?.ScrollToVerticalOffset(verticalOffset);
        }
        finally
        {
            _isSyncingLineNumberScroll = false;
        }
    }

    private static T? FindDescendant<T>(DependencyObject parent) where T : DependencyObject
    {
        var childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;

            var nested = FindDescendant<T>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private void QueueLineNumberRefresh()
    {
        if (_lineNumberRefreshPending)
            return;

        _lineNumberRefreshPending = true;
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _lineNumberRefreshPending = false;
            RefreshVisibleLineNumbers();
        }), DispatcherPriority.Background);
    }

    private void RefreshVisibleLineNumbers()
    {
        var lineCount = Math.Max(1, Editor.LineCount);
        var markerMode = VM?.LineMarkerMode ?? LineMarkerMode.Numbers;

        var lineBreak = Environment.NewLine;
        var sb = new StringBuilder(lineCount * (lineBreak.Length + 3));
        for (var lineIndex = 0; lineIndex < lineCount; lineIndex++)
            sb.Append(GetLineNumberLabel(markerMode, lineIndex)).Append(lineBreak);

        var renderedLineNumbers = sb.ToString();
        if (lineCount == _lastRenderedLineNumberCount &&
            string.Equals(LineNumbers.Text, renderedLineNumbers, StringComparison.Ordinal))
        {
            return;
        }

        LineNumbers.Text = renderedLineNumbers;
        VM?.SetVisibleLineCount(lineCount);
        _lastRenderedLineNumberCount = lineCount;
        SyncLineNumberScrollWithEditor();
    }

    private string GetLineNumberLabel(LineMarkerMode markerMode, int lineIndex)
    {
        if (markerMode == LineMarkerMode.Bullets)
            return "\u2022";

        if (markerMode == LineMarkerMode.Checklist)
            return VM?.IsChecklistLineChecked(lineIndex) == true ? "\u2611" : "\u2610";

        return (lineIndex + 1).ToString(CultureInfo.InvariantCulture);
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape && ClearSelection())
        {
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key is Key.D8 or Key.NumPad8)
            {
                SetLineMarkerMode(LineMarkerMode.Bullets);
                e.Handled = true;
                return;
            }

            if (e.Key is Key.D7 or Key.NumPad7)
            {
                SetLineMarkerMode(LineMarkerMode.Checklist);
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter)
        {
            ToggleChecklistChecksForSelection();
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Enter && Editor.SelectionLength == 0 && VM?.LineMarkerMode == LineMarkerMode.Checklist)
        {
            var caretIndex = Editor.CaretIndex;
            var lineIndex = Editor.GetLineIndexFromCharacterIndex(caretIndex);
            var lineStart = Editor.GetCharacterIndexFromLineIndex(lineIndex);
            var shiftFrom = caretIndex == lineStart ? lineIndex : lineIndex + 1;
            VM.ShiftChecklistLineIndexes(shiftFrom, +1);
        }
    }

    private void LineNumbers_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        EditorFocused?.Invoke(this, EventArgs.Empty);

        var lineIndex = GetLineIndexFromGutterPoint(e.GetPosition(LineNumbers));
        if (lineIndex < 0)
            return;

        _gutterContextLineIndex = lineIndex;
        if (VM?.LineMarkerMode == LineMarkerMode.Checklist)
        {
            VM.ToggleChecklistLineChecked(lineIndex);
            QueueLineNumberRefresh();
            e.Handled = true;
            return;
        }

        MoveCaretToLineStart(lineIndex);
        e.Handled = true;
    }

    private void LineNumbers_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        EditorFocused?.Invoke(this, EventArgs.Empty);
        _gutterContextLineIndex = GetLineIndexFromGutterPoint(e.GetPosition(LineNumbers));
    }

    private int GetLineIndexFromGutterPoint(Point point)
    {
        var charIndex = LineNumbers.GetCharacterIndexFromPoint(point, true);
        if (charIndex < 0)
            return -1;

        var lineIndex = LineNumbers.GetLineIndexFromCharacterIndex(charIndex);
        if (lineIndex < 0)
            return -1;

        return Math.Clamp(lineIndex, 0, Math.Max(0, Editor.LineCount - 1));
    }

    private void MoveCaretToLineStart(int lineIndex)
    {
        if (Editor.LineCount <= 0)
            return;

        var safeLine = Math.Clamp(lineIndex, 0, Math.Max(0, Editor.LineCount - 1));
        var charIndex = Editor.GetCharacterIndexFromLineIndex(safeLine);
        if (charIndex < 0)
            return;

        Editor.Focus();
        Editor.Select(charIndex, 0);
        Editor.ScrollToLine(safeLine);
    }

    private void LineNumbersContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        var markerMode = VM?.LineMarkerMode ?? LineMarkerMode.Numbers;
        LineMarkerNumbersMenuItem.IsChecked = markerMode == LineMarkerMode.Numbers;
        LineMarkerBulletsMenuItem.IsChecked = markerMode == LineMarkerMode.Bullets;
        LineMarkerChecklistMenuItem.IsChecked = markerMode == LineMarkerMode.Checklist;
        LineMarkerToggleCheckMenuItem.IsEnabled = markerMode == LineMarkerMode.Checklist;
    }

    private void LineMarkerNumbers_Click(object sender, RoutedEventArgs e) => SetLineMarkerMode(LineMarkerMode.Numbers);
    private void LineMarkerBullets_Click(object sender, RoutedEventArgs e) => SetLineMarkerMode(LineMarkerMode.Bullets);
    private void LineMarkerChecklist_Click(object sender, RoutedEventArgs e) => SetLineMarkerMode(LineMarkerMode.Checklist);

    private void LineMarkerToggleCheck_Click(object sender, RoutedEventArgs e)
    {
        if (VM is null)
            return;

        if (VM.LineMarkerMode != LineMarkerMode.Checklist)
            VM.LineMarkerMode = LineMarkerMode.Checklist;

        var targetLine = _gutterContextLineIndex >= 0
            ? _gutterContextLineIndex
            : Editor.GetLineIndexFromCharacterIndex(Editor.CaretIndex);

        VM.ToggleChecklistLineChecked(targetLine);
        QueueLineNumberRefresh();
    }

    private void SetLineMarkerMode(LineMarkerMode markerMode)
    {
        if (VM is null)
            return;

        VM.LineMarkerMode = markerMode;
        QueueLineNumberRefresh();
    }

    private void Editor_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Command != ApplicationCommands.Paste)
            return;

        if (!TryApplyPastePresetFromClipboard())
            return;

        e.Handled = true;
    }

    private void ColumnContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        UpdatePastePresetMenuChecks();

        if (VM is null)
            return;

        ColumnFontBoldMenuItem.IsChecked = VM.EditorFontWeight == FontWeights.Bold;
        ColumnFontItalicMenuItem.IsChecked = VM.EditorFontStyle == FontStyles.Italic;
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ClearSelection();
    }

    private void ColumnMenuRename_Click(object sender, RoutedEventArgs e)
    {
        if (VM is not null)
            VM.IsRenaming = true;
    }

    private void ColumnMenuDelete_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuMoveLeft_Click(object sender, RoutedEventArgs e)
    {
        MoveLeftRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuMoveRight_Click(object sender, RoutedEventArgs e)
    {
        MoveRightRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuResetWidth_Click(object sender, RoutedEventArgs e)
    {
        ResetWidthRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuResetAllWidths_Click(object sender, RoutedEventArgs e)
    {
        ResetAllWidthsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuResize_Click(object sender, RoutedEventArgs e)
    {
        ResizeRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnMenuToggleWidthLock_Click(object sender, RoutedEventArgs e)
    {
        LockWidthRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontSetFamily_Click(object sender, RoutedEventArgs e)
    {
        SetFontFamilyRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontIncrease_Click(object sender, RoutedEventArgs e)
    {
        IncreaseFontRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontDecrease_Click(object sender, RoutedEventArgs e)
    {
        DecreaseFontRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontBold_Click(object sender, RoutedEventArgs e)
    {
        ToggleBoldRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontItalic_Click(object sender, RoutedEventArgs e)
    {
        ToggleItalicRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ColumnFontReset_Click(object sender, RoutedEventArgs e)
    {
        ResetFontRequested?.Invoke(this, EventArgs.Empty);
    }

    private void EditorContextMenu_Opened(object sender, RoutedEventArgs e)
    {
        UpdatePastePresetMenuChecks();
    }

    private void PastePresetNone_Click(object sender, RoutedEventArgs e) => SetPastePreset(PasteListPreset.None);
    private void PastePresetBullets_Click(object sender, RoutedEventArgs e) => SetPastePreset(PasteListPreset.Bullets);
    private void PastePresetChecklist_Click(object sender, RoutedEventArgs e) => SetPastePreset(PasteListPreset.Checklist);

    private void ToggleBullets_Click(object sender, RoutedEventArgs e) => SetLineMarkerMode(LineMarkerMode.Bullets);
    private void ToggleChecklist_Click(object sender, RoutedEventArgs e) => SetLineMarkerMode(LineMarkerMode.Checklist);
    private void ToggleCheckMarks_Click(object sender, RoutedEventArgs e) => ToggleChecklistChecksForSelection();

    private void ToggleChecklistChecksForSelection()
    {
        if (VM is null)
            return;

        if (VM.LineMarkerMode != LineMarkerMode.Checklist)
            VM.LineMarkerMode = LineMarkerMode.Checklist;

        var (startLine, endLine) = GetSelectedLineRange();
        for (var i = startLine; i <= endLine; i++)
            VM.ToggleChecklistLineChecked(i);

        QueueLineNumberRefresh();
    }

    private (int StartLine, int EndLine) GetSelectedLineRange()
    {
        var selectionStart = Editor.SelectionStart;
        var selectionEnd = selectionStart + Editor.SelectionLength;

        var startLine = Editor.GetLineIndexFromCharacterIndex(selectionStart);
        var endLine = Editor.GetLineIndexFromCharacterIndex(selectionEnd);

        // If selection ends at the start of a line, operate through the previous line.
        if (selectionEnd > selectionStart &&
            endLine > startLine &&
            selectionEnd == Editor.GetCharacterIndexFromLineIndex(endLine))
        {
            endLine--;
        }

        if (endLine < startLine)
            endLine = startLine;

        return (startLine, endLine);
    }

    private void SetPastePreset(PasteListPreset preset)
    {
        if (VM is null)
            return;

        VM.PastePreset = preset;
        UpdatePastePresetMenuChecks();
    }

    private void UpdatePastePresetMenuChecks()
    {
        if (Editor.ContextMenu is null)
            return;

        var presetMenu = Editor.ContextMenu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(item => string.Equals(item.Tag as string, "PastePresetMenu", StringComparison.Ordinal));

        if (presetMenu is null)
            return;

        var activePreset = VM?.PastePreset ?? PasteListPreset.None;
        foreach (var child in presetMenu.Items.OfType<MenuItem>())
        {
            if (child.Tag is not string tag || !Enum.TryParse<PasteListPreset>(tag, ignoreCase: true, out var preset))
                continue;

            child.IsChecked = preset == activePreset;
        }
    }

    private bool TryApplyPastePresetFromClipboard()
    {
        var preset = VM?.PastePreset ?? PasteListPreset.None;
        if (preset == PasteListPreset.None || !Clipboard.ContainsText())
            return false;

        var source = Clipboard.GetText();
        if (string.IsNullOrEmpty(source))
            return false;

        Editor.SelectedText = ApplyPastePreset(source, preset);
        return true;
    }

    private static LineMarkerInfo ParseLineMarker(string line) => ListMarkerRules.ParseLineMarker(line);

    private static bool IsOrderedListLine(string line)
        => ListMarkerRules.HasOrderedListPrefix(line);

    private static string ApplyPastePreset(string source, PasteListPreset preset)
    {
        if (preset == PasteListPreset.None || string.IsNullOrEmpty(source))
            return source;

        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || IsOrderedListLine(lines[i]))
                continue;

            var parsed = ParseLineMarker(lines[i]);
            var bodyStart = parsed.Kind == ListMarkerKind.None
                ? parsed.LeadingWhitespaceLength
                : parsed.LeadingWhitespaceLength + parsed.Prefix.Length;

            var leading = lines[i][..parsed.LeadingWhitespaceLength];
            var body = lines[i][bodyStart..];

            lines[i] = preset switch
            {
                PasteListPreset.Bullets => $"{leading}{ListMarkerRules.MarkdownBulletPrefix}{body}",
                PasteListPreset.Checklist when parsed.Kind == ListMarkerKind.ChecklistChecked => $"{leading}{ListMarkerRules.MarkdownChecklistCheckedPrefix}{body}",
                PasteListPreset.Checklist => $"{leading}{ListMarkerRules.MarkdownChecklistUncheckedPrefix}{body}",
                _ => lines[i]
            };
        }

        var transformed = string.Join('\n', lines);
        return source.Contains("\r\n", StringComparison.Ordinal)
            ? transformed.Replace("\n", "\r\n", StringComparison.Ordinal)
            : transformed;
    }
}
