using ColumnPadStudio.ViewModels;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl : UserControl
{
    private const string BulletPrefix = "\u2022 ";
    private const string ChecklistUncheckedPrefix = "\u2610 ";
    private const string ChecklistCheckedPrefix = "\u2611 ";

    private static readonly string[] BulletPrefixes = [BulletPrefix, "- "];
    private static readonly string[] ChecklistUncheckedPrefixes = [ChecklistUncheckedPrefix, "- [ ] "];
    private static readonly string[] ChecklistCheckedPrefixes = [ChecklistCheckedPrefix, "- [x] ", "- [X] "];

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

    public ColumnEditorControl()
    {
        InitializeComponent();
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

    public void ApplyBulletsToSelection() => ToggleBullets();
    public void ApplyChecklistToSelection() => ToggleChecklist();
    public void ToggleChecklistChecksInSelection() => ToggleCheckMarks();

    public bool ClearSelection()
    {
        if (Editor.SelectionLength <= 0)
            return false;

        var caretIndex = Editor.SelectionStart + Editor.SelectionLength;
        Editor.Select(caretIndex, 0);
        Editor.Focus();
        return true;
    }

    private void Editor_GotFocus(object sender, RoutedEventArgs e)
    {
        EditorFocused?.Invoke(this, EventArgs.Empty);
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
                ApplyBulletsToSelection();
                e.Handled = true;
                return;
            }

            if (e.Key is Key.D7 or Key.NumPad7)
            {
                ApplyChecklistToSelection();
                e.Handled = true;
                return;
            }
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Enter)
        {
            ToggleChecklistChecksInSelection();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || Editor.SelectionLength > 0)
            return;

        var lineInfo = GetLineInfoAtCharacter(Editor.CaretIndex);
        if (lineInfo is null)
            return;

        var marker = ParseLineMarker(lineInfo.Value.Text);
        if (marker.Kind == MarkerKind.None)
            return;

        e.Handled = true;
        var bodyStart = marker.LeadingWhitespaceLength + marker.Prefix.Length;
        var body = lineInfo.Value.Text[bodyStart..];

        // Empty list item: remove marker and exit the list.
        if (string.IsNullOrWhiteSpace(body))
        {
            Editor.Select(lineInfo.Value.Start + marker.LeadingWhitespaceLength, marker.Prefix.Length);
            Editor.SelectedText = string.Empty;
            Editor.CaretIndex = lineInfo.Value.Start;
            return;
        }

        var continuationPrefix = marker.Kind == MarkerKind.Bullet ? BulletPrefix : ChecklistUncheckedPrefix;
        var leading = lineInfo.Value.Text[..marker.LeadingWhitespaceLength];
        Editor.SelectedText = Environment.NewLine + leading + continuationPrefix;
    }

    private void Editor_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(Editor);
        var charIndex = Editor.GetCharacterIndexFromPoint(point, true);
        if (charIndex < 0)
            return;

        var lineInfo = GetLineInfoAtCharacter(charIndex);
        if (lineInfo is null)
            return;

        var marker = ParseLineMarker(lineInfo.Value.Text);
        if (marker.Kind is not MarkerKind.ChecklistUnchecked and not MarkerKind.ChecklistChecked)
            return;

        var offsetInLine = charIndex - lineInfo.Value.Start;
        var markerStart = marker.LeadingWhitespaceLength;
        var markerEnd = marker.LeadingWhitespaceLength + marker.Prefix.Length;
        if (offsetInLine < markerStart || offsetInLine >= markerEnd)
            return;

        e.Handled = true;

        var replacementPrefix = marker.Kind == MarkerKind.ChecklistChecked
            ? ChecklistUncheckedPrefix
            : ChecklistCheckedPrefix;

        Editor.Select(lineInfo.Value.Start + marker.LeadingWhitespaceLength, marker.Prefix.Length);
        Editor.SelectedText = replacementPrefix;
        Editor.CaretIndex = lineInfo.Value.Start + marker.LeadingWhitespaceLength + replacementPrefix.Length;
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

    private void ToggleBullets_Click(object sender, RoutedEventArgs e) => ToggleBullets();
    private void ToggleChecklist_Click(object sender, RoutedEventArgs e) => ToggleChecklist();
    private void ToggleCheckMarks_Click(object sender, RoutedEventArgs e) => ToggleCheckMarks();

    private void ToggleBullets()
    {
        ApplyLineTransform(lines =>
        {
            if (lines.Count == 0)
                return lines;

            var allBulleted = lines.All(line => ParseLineMarker(line).Kind == MarkerKind.Bullet);

            for (var i = 0; i < lines.Count; i++)
            {
                var marker = ParseLineMarker(lines[i]);
                if (allBulleted)
                {
                    if (marker.Kind == MarkerKind.Bullet)
                        lines[i] = RemoveMarker(lines[i], marker);
                    continue;
                }

                if (marker.Kind is MarkerKind.ChecklistUnchecked or MarkerKind.ChecklistChecked)
                    continue;

                lines[i] = UpsertMarker(lines[i], BulletPrefix);
            }

            return lines;
        });
    }

    private void ToggleChecklist()
    {
        ApplyLineTransform(lines =>
        {
            if (lines.Count == 0)
                return lines;

            var allChecklist = lines.All(line =>
            {
                var kind = ParseLineMarker(line).Kind;
                return kind is MarkerKind.ChecklistUnchecked or MarkerKind.ChecklistChecked;
            });

            for (var i = 0; i < lines.Count; i++)
            {
                var marker = ParseLineMarker(lines[i]);
                if (allChecklist)
                {
                    if (marker.Kind is MarkerKind.ChecklistUnchecked or MarkerKind.ChecklistChecked)
                        lines[i] = RemoveMarker(lines[i], marker);
                    continue;
                }

                if (marker.Kind == MarkerKind.ChecklistChecked)
                {
                    lines[i] = UpsertMarker(lines[i], ChecklistCheckedPrefix);
                    continue;
                }

                if (marker.Kind == MarkerKind.ChecklistUnchecked)
                {
                    lines[i] = UpsertMarker(lines[i], ChecklistUncheckedPrefix);
                    continue;
                }

                if (marker.Kind == MarkerKind.Bullet)
                {
                    lines[i] = UpsertMarker(lines[i], ChecklistUncheckedPrefix);
                    continue;
                }

                lines[i] = UpsertMarker(lines[i], ChecklistUncheckedPrefix);
            }

            return lines;
        });
    }

    private void ToggleCheckMarks()
    {
        ApplyLineTransform(lines =>
        {
            for (var i = 0; i < lines.Count; i++)
            {
                var marker = ParseLineMarker(lines[i]);
                if (marker.Kind == MarkerKind.ChecklistUnchecked)
                {
                    lines[i] = UpsertMarker(lines[i], ChecklistCheckedPrefix);
                }
                else if (marker.Kind == MarkerKind.ChecklistChecked)
                {
                    lines[i] = UpsertMarker(lines[i], ChecklistUncheckedPrefix);
                }
            }

            return lines;
        });
    }

    private void ApplyLineTransform(Func<List<string>, List<string>> transform)
    {
        var (start, end) = GetSelectedLineRange();
        if (start < 0 || end < start || end > Editor.Text.Length)
            return;

        var block = Editor.Text[start..end];
        var normalized = block.Replace("\r\n", "\n", StringComparison.Ordinal);
        var hadTrailingNewline = normalized.EndsWith('\n');

        var lines = normalized.Split('\n').ToList();
        if (hadTrailingNewline && lines.Count > 0 && lines[^1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        var updatedLines = transform(lines);
        var updatedNormalized = string.Join("\n", updatedLines);
        if (hadTrailingNewline)
            updatedNormalized += "\n";

        var updatedBlock = updatedNormalized.Replace("\n", Environment.NewLine, StringComparison.Ordinal);

        Editor.Select(start, end - start);
        Editor.SelectedText = updatedBlock;
        Editor.Select(start, updatedBlock.Length);
    }

    private (int start, int end) GetSelectedLineRange()
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

        var start = Editor.GetCharacterIndexFromLineIndex(startLine);
        var end = endLine + 1 < Editor.LineCount
            ? Editor.GetCharacterIndexFromLineIndex(endLine + 1)
            : Editor.Text.Length;

        return (start, end);
    }

    private LineInfo? GetLineInfoAtCharacter(int charIndex)
    {
        if (charIndex < 0)
            return null;

        if (Editor.LineCount <= 0)
            return new LineInfo(0, string.Empty);

        var safeIndex = Math.Min(charIndex, Editor.Text.Length);
        var lineIndex = Editor.GetLineIndexFromCharacterIndex(safeIndex);
        var start = Editor.GetCharacterIndexFromLineIndex(lineIndex);
        var end = lineIndex + 1 < Editor.LineCount
            ? Editor.GetCharacterIndexFromLineIndex(lineIndex + 1)
            : Editor.Text.Length;

        return new LineInfo(start, Editor.Text[start..end]);
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

    private static MarkerInfo ParseMarker(string line)
    {
        foreach (var prefix in ChecklistUncheckedPrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return new MarkerInfo(MarkerKind.ChecklistUnchecked, prefix);
        }

        foreach (var prefix in ChecklistCheckedPrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return new MarkerInfo(MarkerKind.ChecklistChecked, prefix);
        }

        foreach (var prefix in BulletPrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return new MarkerInfo(MarkerKind.Bullet, prefix);
        }

        return new MarkerInfo(MarkerKind.None, string.Empty);
    }

    private static LineMarkerInfo ParseLineMarker(string line)
    {
        var leadingWhitespaceLength = CountLeadingWhitespace(line);
        var marker = ParseMarker(line[leadingWhitespaceLength..]);
        return new LineMarkerInfo(marker.Kind, leadingWhitespaceLength, marker.Prefix);
    }

    private static int CountLeadingWhitespace(string line)
    {
        var i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i]))
            i++;
        return i;
    }

    private static string RemoveMarker(string line, LineMarkerInfo marker)
    {
        if (marker.Kind == MarkerKind.None)
            return line;

        var prefixStart = marker.LeadingWhitespaceLength;
        var bodyStart = prefixStart + marker.Prefix.Length;
        return string.Concat(line.AsSpan(0, prefixStart), line.AsSpan(bodyStart));
    }

    private static string UpsertMarker(string line, string prefix)
    {
        var marker = ParseLineMarker(line);
        var bodyStart = marker.Kind == MarkerKind.None
            ? marker.LeadingWhitespaceLength
            : marker.LeadingWhitespaceLength + marker.Prefix.Length;

        var leading = line[..marker.LeadingWhitespaceLength];
        var body = line[bodyStart..];
        return $"{leading}{prefix}{body}";
    }

    private static string ApplyPastePreset(string source, PasteListPreset preset)
    {
        if (preset == PasteListPreset.None || string.IsNullOrEmpty(source))
            return source;

        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            var parsed = ParseLineMarker(lines[i]);
            var bodyStart = parsed.Kind == MarkerKind.None
                ? parsed.LeadingWhitespaceLength
                : parsed.LeadingWhitespaceLength + parsed.Prefix.Length;

            var leading = lines[i][..parsed.LeadingWhitespaceLength];
            var body = lines[i][bodyStart..];

            lines[i] = preset switch
            {
                PasteListPreset.Bullets => $"{leading}{BulletPrefix}{body}",
                PasteListPreset.Checklist when parsed.Kind == MarkerKind.ChecklistChecked => $"{leading}{ChecklistCheckedPrefix}{body}",
                PasteListPreset.Checklist => $"{leading}{ChecklistUncheckedPrefix}{body}",
                _ => lines[i]
            };
        }

        var transformed = string.Join('\n', lines);
        return source.Contains("\r\n", StringComparison.Ordinal)
            ? transformed.Replace("\n", "\r\n", StringComparison.Ordinal)
            : transformed;
    }

    private enum MarkerKind
    {
        None,
        Bullet,
        ChecklistUnchecked,
        ChecklistChecked
    }

    private readonly record struct MarkerInfo(MarkerKind Kind, string Prefix);
    private readonly record struct LineMarkerInfo(MarkerKind Kind, int LeadingWhitespaceLength, string Prefix);
    private readonly record struct LineInfo(int Start, string Text);
}
