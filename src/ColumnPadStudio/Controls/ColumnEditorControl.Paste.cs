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

        Editor.SelectedText = transformed;
        return true;
    }
}
