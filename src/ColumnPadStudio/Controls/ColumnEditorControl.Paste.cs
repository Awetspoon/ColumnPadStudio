using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
    private void Editor_PreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        if (e.Command != ApplicationCommands.Paste)
            return;

        if (!TryHandleTextPasteFromClipboard())
            return;

        e.Handled = true;
    }

    private bool TryHandleTextPasteFromClipboard()
    {
        if (!Clipboard.ContainsText())
            return false;

        var source = Clipboard.GetText();
        var preset = VM?.PastePreset ?? PasteListPreset.None;
        var transformed = ClipboardTextService.FormatPastedText(source, preset);
        if (string.IsNullOrEmpty(transformed))
            return false;

        ShiftChecklistMetadataForPaste(transformed);
        Editor.SelectedText = transformed;
        return true;
    }

    private void ShiftChecklistMetadataForPaste(string pastedText)
    {
        if (VM?.LineMarkerMode != LineMarkerMode.Checklist)
            return;

        var insertedLineBreaks = ClipboardTextService.CountLineBreaks(pastedText);
        var removedLineBreaks = ClipboardTextService.CountLineBreaks(Editor.SelectedText);
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
}
