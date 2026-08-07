using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using Microsoft.Win32;
using System.IO;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private void InsertImageIntoActiveColumn()
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        var dialog = new OpenFileDialog
        {
            Filter = ColumnImageFileService.ImageOpenFileFilter,
            FilterIndex = 1,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != true)
            return;

        ImportImageIntoActiveColumn(dialog.FileName, 12.0, 12.0);
    }

    private void ImportImageIntoActiveColumn(string filePath, double left, double top)
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        TryRunFileAction("Insert Picture Failed", $"insert {Path.GetFileName(filePath)}", () =>
        {
            var imported = ColumnImageFileService.ImportImage(filePath);
            var maximumInitialWidth = GetMaximumInitialImageWidth(active);
            var image = new ColumnImageViewModel(
                imported.FilePath,
                imported.OriginalFileName,
                Math.Min(imported.DisplayWidth, maximumInitialWidth),
                imported.PixelWidth,
                imported.PixelHeight,
                left,
                top,
                imageContent: imported.Content);

            ActiveVm.PrepareForRichContent();
            active.Images.Add(image);
            active.SelectImage(image);
            ActiveVm.RefreshStatus();
            ActiveVm.StatusText = $"Inserted picture: {image.DisplayName}";
        });
    }

    private void RemoveImageFromActiveColumn(ColumnImageViewModel image)
    {
        var active = ActiveVm.GetActive();
        if (active is null)
            return;

        if (!active.Images.Remove(image))
            return;

        ActiveVm.RefreshStatus();
        ActiveVm.StatusText = $"Removed picture: {image.DisplayName}";
    }

    private double GetMaximumInitialImageWidth(ColumnViewModel column)
    {
        if (_editorsById.TryGetValue(column.Id, out var editor) && editor.PictureSurfaceWidth > 0)
            return Math.Max(ColumnImageViewModel.MinDisplayWidth, editor.PictureSurfaceWidth - 24.0);

        var columnWidth = column.WidthPx ?? _appPreferences.DefaultColumnWidthPx;
        var gutterWidth = column.LineNumberColumnWidth.IsAbsolute ? column.LineNumberColumnWidth.Value : 0;
        return Math.Max(ColumnImageViewModel.MinDisplayWidth, columnWidth - gutterWidth - 24.0);
    }
}
