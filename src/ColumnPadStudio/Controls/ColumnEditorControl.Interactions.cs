using ColumnPadStudio.ViewModels;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        VM?.DeselectImages();

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

    }

    private void RightEdgeResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        RightEdgeResizeDeltaRequested?.Invoke(this, new ColumnResizeDeltaEventArgs(e.HorizontalChange));
        e.Handled = true;
    }
}
