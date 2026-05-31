using ColumnPadStudio.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
    private void LineNumbers_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        EditorFocused?.Invoke(this, EventArgs.Empty);

        var lineIndex = GetLineIndexFromGutterPoint(e.GetPosition(LineNumberGutter));
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
}
