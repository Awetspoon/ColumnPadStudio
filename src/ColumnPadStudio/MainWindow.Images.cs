using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private const double ImageColumnPaddingPx = 72.0;

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

        TryRunFileAction("Insert Picture Failed", $"insert {Path.GetFileName(dialog.FileName)}", () =>
        {
            var imported = ColumnImageFileService.ImportImage(dialog.FileName);
            var image = new ColumnImageViewModel(
                imported.FilePath,
                imported.OriginalFileName,
                imported.DisplayWidth,
                imported.PixelWidth,
                imported.PixelHeight);

            active.Images.Add(image);
            EnsureColumnFitsImage(active, image);
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

    private void EnsureColumnFitsImage(ColumnViewModel column, ColumnImageViewModel image)
    {
        if (column.IsWidthLocked)
            return;

        var desiredWidth = (int)Math.Ceiling(image.Width + ImageColumnPaddingPx);
        if (desiredWidth <= 0)
            return;

        var currentWidth = column.WidthPx ?? (int)DefaultColumnWidthPx;
        if (desiredWidth <= currentWidth)
            return;

        ApplyColumnWidth(column, Math.Clamp(desiredWidth, 220, 5000));
        ActiveVm.StatusText = $"Expanded {column.Title} to fit picture.";
    }

    private void ApplyColumnWidth(ColumnViewModel column, int widthPx)
    {
        column.WidthPx = widthPx;

        if (!_editorsById.TryGetValue(column.Id, out var editor))
        {
            RebuildColumns();
            return;
        }

        var gridColumn = Grid.GetColumn(editor);
        if (gridColumn < 0 || gridColumn >= ColumnsHost.ColumnDefinitions.Count)
        {
            RebuildColumns();
            return;
        }

        ColumnsHost.ColumnDefinitions[gridColumn].Width = new GridLength(widthPx, GridUnitType.Pixel);
    }
}
