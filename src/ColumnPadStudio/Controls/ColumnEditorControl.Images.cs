using ColumnPadStudio.ViewModels;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColumnPadStudio.Controls;

public partial class ColumnEditorControl
{
    private void EditorSurface_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = TryGetSingleDroppedFile(e.Data, out _)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void EditorSurface_Drop(object sender, DragEventArgs e)
    {
        if (!TryGetSingleDroppedFile(e.Data, out var filePath))
            return;

        var position = e.GetPosition(ImageOverlay);
        ImageFileDropped?.Invoke(this, new ColumnImageFileEventArgs(
            filePath,
            Math.Max(0.0, position.X - 24.0),
            Math.Max(0.0, position.Y - 24.0)));
        e.Handled = true;
    }

    private void ImageMoveThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (GetTaggedImage(sender) is not { } image || VM is null)
            return;

        VM.SelectImage(image);
        EditorFocused?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void ImageMoveThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (GetTaggedImage(sender) is not { } image)
            return;

        var maxLeft = Math.Max(0.0, ImageOverlay.ActualWidth - image.Width);
        var maxTop = Math.Max(0.0, ImageOverlay.ActualHeight - image.Height);
        image.Left = Math.Clamp(image.Left + e.HorizontalChange, 0.0, maxLeft);
        image.Top = Math.Clamp(image.Top + e.VerticalChange, 0.0, maxTop);
        e.Handled = true;
    }

    private void ImageResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (GetTaggedImage(sender) is not { } image || VM is null)
            return;

        VM.SelectImage(image);
        EditorFocused?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void ImageResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (GetTaggedImage(sender) is not { } image)
            return;

        var aspectRatio = image.PixelWidth > 0 && image.PixelHeight > 0
            ? (double)image.PixelWidth / image.PixelHeight
            : 4.0 / 3.0;
        var verticalWidthChange = e.VerticalChange * aspectRatio;
        var requestedChange = Math.Abs(verticalWidthChange) > Math.Abs(e.HorizontalChange)
            ? verticalWidthChange
            : e.HorizontalChange;

        var maxWidthFromSurface = Math.Max(
            ColumnImageViewModel.MinDisplayWidth,
            ImageOverlay.ActualWidth - image.Left);
        var maxWidthFromHeight = Math.Max(
            ColumnImageViewModel.MinDisplayWidth,
            (ImageOverlay.ActualHeight - image.Top) * aspectRatio);
        var maxWidth = Math.Min(
            ColumnImageViewModel.MaxDisplayWidth,
            Math.Min(maxWidthFromSurface, maxWidthFromHeight));

        image.Width = Math.Clamp(
            image.Width + requestedChange,
            ColumnImageViewModel.MinDisplayWidth,
            maxWidth);
        e.Handled = true;
    }

    private void ImageToggleLayer_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaggedImage(sender) is not { } image || VM is null)
            return;

        image.Layer = image.Layer == ColumnImageLayer.InFrontOfText
            ? ColumnImageLayer.BehindText
            : ColumnImageLayer.InFrontOfText;

        if (image.Layer == ColumnImageLayer.BehindText)
            VM.DeselectImages();
        else
            VM.SelectImage(image);
    }

    private void ImageSelectFromMenu_Click(object sender, RoutedEventArgs e)
    {
        if (GetTaggedImage(sender) is not { } image || VM is null)
            return;

        VM.SelectImage(image);
        EditorFocused?.Invoke(this, EventArgs.Empty);
    }

    private void ClampImagesToSurface()
    {
        if (VM is null || ImageOverlay.ActualWidth <= 0 || ImageOverlay.ActualHeight <= 0)
            return;

        foreach (var image in VM.Images)
        {
            var maxWidth = Math.Max(ColumnImageViewModel.MinDisplayWidth, ImageOverlay.ActualWidth - image.Left);
            image.Width = Math.Min(image.Width, maxWidth);
            image.Left = Math.Min(image.Left, Math.Max(0.0, ImageOverlay.ActualWidth - image.Width));
            image.Top = Math.Min(image.Top, Math.Max(0.0, ImageOverlay.ActualHeight - image.Height));
        }
    }

    private static ColumnImageViewModel? GetTaggedImage(object sender)
        => (sender as FrameworkElement)?.Tag as ColumnImageViewModel;

    private static bool TryGetSingleDroppedFile(IDataObject data, out string filePath)
    {
        filePath = string.Empty;
        if (!data.GetDataPresent(DataFormats.FileDrop) || data.GetData(DataFormats.FileDrop) is not string[] files || files.Length != 1)
            return false;

        filePath = files[0];
        return !string.IsNullOrWhiteSpace(filePath);
    }
}
