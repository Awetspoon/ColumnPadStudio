using ColumnPadStudio.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
    private void LineNumbers_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        EditorFocused?.Invoke(this, EventArgs.Empty);

        var visualLineIndex = GetLineIndexFromGutterPoint(e.GetPosition(LineNumberGutter));
        if (visualLineIndex < 0)
            return;

        _gutterContextLineIndex = visualLineIndex;
        if (VM?.LineMarkerMode == LineMarkerMode.Checklist)
        {
            ToggleChecklistCheckAtVisualLine(visualLineIndex);
            e.Handled = true;
            return;
        }

        MoveCaretToLineStart(visualLineIndex);
        e.Handled = true;
    }

    private void LineNumbers_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        EditorFocused?.Invoke(this, EventArgs.Empty);
        _gutterContextLineIndex = GetLineIndexFromGutterPoint(e.GetPosition(LineNumberGutter));
    }

    private int GetLineIndexFromGutterPoint(Point point)
    {
        var lineHeight = VM?.EditorLineHeight ?? 23.0;
        if (lineHeight <= 0)
            return -1;

        var verticalOffset = _editorScrollViewer?.VerticalOffset ?? 0;
        var lineIndex = (int)Math.Floor((point.Y + verticalOffset) / lineHeight);
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

        if (_gutterContextLineIndex >= 0)
        {
            ToggleChecklistCheckAtVisualLine(_gutterContextLineIndex);
            return;
        }

        VM.ToggleChecklistLineChecked(GetLogicalLineIndexFromCharacterIndex(Editor.CaretIndex));
        QueueLineNumberRefresh();
    }

    private void ToggleChecklistCheckAtVisualLine(int visualLineIndex)
    {
        if (VM is null || visualLineIndex < 0)
            return;

        VM.ToggleChecklistLineChecked(GetLogicalLineIndexFromVisualLineIndex(visualLineIndex));
        QueueLineNumberRefresh();
    }

    private int GetLogicalLineIndexFromVisualLineIndex(int visualLineIndex)
    {
        if (Editor.LineCount <= 0)
            return 0;

        var safeVisualLine = Math.Clamp(visualLineIndex, 0, Editor.LineCount - 1);
        return BuildVisualToLogicalLineMap(Editor.LineCount)[safeVisualLine];
    }

    private int GetLogicalLineIndexFromCharacterIndex(int characterIndex)
    {
        var text = Editor.Text ?? string.Empty;
        var safeCharacterIndex = Math.Clamp(characterIndex, 0, text.Length);
        var logicalLineIndex = 0;

        for (var index = 0; index < safeCharacterIndex; index++)
        {
            if (text[index] == '\r')
            {
                logicalLineIndex++;
                if (index + 1 < safeCharacterIndex && text[index + 1] == '\n')
                    index++;
            }
            else if (text[index] == '\n')
            {
                logicalLineIndex++;
            }
        }

        return logicalLineIndex;
    }

    private int[] BuildVisualToLogicalLineMap(int visualLineCount)
    {
        var safeVisualLineCount = Math.Max(1, visualLineCount);
        if (Editor.LineCount <= 0)
            return new int[safeVisualLineCount];

        var logicalLineStarts = new List<int> { 0 };
        var text = Editor.Text ?? string.Empty;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\r')
            {
                if (index + 1 < text.Length && text[index + 1] == '\n')
                    index++;

                logicalLineStarts.Add(index + 1);
            }
            else if (text[index] == '\n')
            {
                logicalLineStarts.Add(index + 1);
            }
        }

        var logicalVisualLineStarts = logicalLineStarts
            .Select(characterIndex => Editor.GetLineIndexFromCharacterIndex(characterIndex))
            .Select(visualLineIndex => Math.Clamp(visualLineIndex, 0, safeVisualLineCount - 1))
            .ToArray();

        var visualToLogical = new int[safeVisualLineCount];
        var logicalLineIndex = 0;
        for (var visualLineIndex = 0; visualLineIndex < safeVisualLineCount; visualLineIndex++)
        {
            while (logicalLineIndex + 1 < logicalVisualLineStarts.Length
                   && logicalVisualLineStarts[logicalLineIndex + 1] <= visualLineIndex)
            {
                logicalLineIndex++;
            }

            visualToLogical[visualLineIndex] = logicalLineIndex;
        }

        return visualToLogical;
    }

    private static bool IsLogicalLineStart(string text, int characterIndex)
    {
        var safeCharacterIndex = Math.Clamp(characterIndex, 0, text.Length);
        if (safeCharacterIndex == 0)
            return true;

        var previous = text[safeCharacterIndex - 1];
        if (previous == '\n')
            return true;

        return previous == '\r'
               && (safeCharacterIndex >= text.Length || text[safeCharacterIndex] != '\n');
    }

    private void SetLineMarkerMode(LineMarkerMode markerMode)
    {
        if (VM is null)
            return;

        VM.LineMarkerMode = markerMode;
        QueueLineNumberRefresh();
    }
}
