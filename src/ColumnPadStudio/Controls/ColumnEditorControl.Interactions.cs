using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
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

        if (!TryHandleTextPasteFromClipboard())
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

    private bool TryHandleTextPasteFromClipboard()
    {
        if (!Clipboard.ContainsText())
            return false;

        var source = Clipboard.GetText();
        if (string.IsNullOrEmpty(source))
            return false;

        var normalized = NormalizeClipboardText(source);
        var preset = VM?.PastePreset ?? PasteListPreset.None;
        var transformed = ApplyPastePreset(normalized, preset);

        ShiftChecklistMetadataForPaste(transformed);
        Editor.SelectedText = transformed;
        return true;
    }

    private void ShiftChecklistMetadataForPaste(string pastedText)
    {
        if (VM?.LineMarkerMode != LineMarkerMode.Checklist)
            return;

        var insertedLineBreaks = CountLineBreaks(pastedText);
        var removedLineBreaks = CountLineBreaks(Editor.SelectedText);
        var delta = insertedLineBreaks - removedLineBreaks;
        if (delta == 0)
            return;

        var selectionStart = Editor.SelectionStart;
        var selectionEnd = selectionStart + Editor.SelectionLength;
        var startLine = Editor.GetLineIndexFromCharacterIndex(selectionStart);
        var endLine = Editor.GetLineIndexFromCharacterIndex(selectionEnd);
        var lineStart = Editor.GetCharacterIndexFromLineIndex(startLine);

        var shiftFrom = Editor.SelectionLength == 0
            ? (selectionStart == lineStart ? startLine : startLine + 1)
            : (selectionEnd == Editor.GetCharacterIndexFromLineIndex(endLine) ? endLine : endLine + 1);

        VM.ShiftChecklistLineIndexes(shiftFrom, delta);
    }

    private static int CountLineBreaks(string text)
    {
        var count = 0;
        foreach (var ch in text)
        {
            if (ch == '\n')
                count++;
        }

        return count;
    }

    private static LineMarkerInfo ParseLineMarker(string line) => ListMarkerRules.ParseLineMarker(line);

    private static bool IsOrderedListLine(string line)
        => ListMarkerRules.HasOrderedListPrefix(line);

    private static string NormalizeClipboardText(string source)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        // Some clipboard providers emit CRCRLF for single line breaks.
        // Collapse that malformed sequence first so we avoid double-spaced paste.
        while (source.Contains("\r\r\n", StringComparison.Ordinal))
            source = source.Replace("\r\r\n", "\r\n", StringComparison.Ordinal);

        source = source
            .Replace("\u2028", "\n", StringComparison.Ordinal)
            .Replace("\u2029", "\n", StringComparison.Ordinal)
            .Replace("\n\r", "\n", StringComparison.Ordinal);

        var normalized = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        normalized = CollapseAlternatingBlankClipboardLines(normalized);
        return normalized.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private static string CollapseAlternatingBlankClipboardLines(string text)
    {
        var lines = text.Split('\n');
        if (lines.Length < 6)
            return text;

        // Only collapse if there are no consecutive content lines; this targets
        // malformed alternating blank-line paste without flattening normal paragraphs.
        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]) && !string.IsNullOrWhiteSpace(lines[i + 1]))
                return text;
        }

        var evenCount = 0;
        var oddCount = 0;
        var evenBlank = 0;
        var oddBlank = 0;
        var evenContent = 0;
        var oddContent = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var isBlank = string.IsNullOrWhiteSpace(lines[i]);
            if ((i & 1) == 0)
            {
                evenCount++;
                if (isBlank)
                    evenBlank++;
                else
                    evenContent++;
            }
            else
            {
                oddCount++;
                if (isBlank)
                    oddBlank++;
                else
                    oddContent++;
            }
        }

        var collapseOdd = oddCount > 0 &&
                          oddBlank >= (int)Math.Ceiling(oddCount * 0.85) &&
                          evenContent >= 3 &&
                          evenBlank <= 1;
        var collapseEven = evenCount > 0 &&
                           evenBlank >= (int)Math.Ceiling(evenCount * 0.85) &&
                           oddContent >= 3 &&
                           oddBlank <= 1;

        if (!collapseOdd && !collapseEven)
            return text;

        var blankParityToRemove = collapseOdd ? 1 : 0;
        var filtered = lines
            .Where((line, index) => !((index & 1) == blankParityToRemove && string.IsNullOrWhiteSpace(line)))
            .ToArray();

        return string.Join('\n', filtered);
    }

    private static string ApplyPastePreset(string source, PasteListPreset preset)
    {
        if (preset == PasteListPreset.None || string.IsNullOrEmpty(source))
            return source;

        var normalized = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

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

        return string.Join(Environment.NewLine, lines);
    }
}
