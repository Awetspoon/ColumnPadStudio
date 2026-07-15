using ColumnPadStudio.Controls;
using ColumnPadStudio.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private const double DefaultColumnWidthPx = 320.0;
    private readonly Dictionary<string, ColumnEditorControl> _editorsById = new(StringComparer.Ordinal);

    private static void SyncActiveColumnVisualState(MainViewModel vm)
    {
        var selectedId = vm.GetActive()?.Id;
        foreach (var column in vm.Columns)
            column.IsActive = column.Id == selectedId;
    }

    private static void RunColumnAction(MainViewModel vm, ColumnViewModel column, Action action)
    {
        vm.ActiveColumnId = column.Id;
        action();
    }

    private void ClearSelectionsExcept(string? activeColumnId)
    {
        foreach (var (columnId, editor) in _editorsById)
        {
            if (!string.IsNullOrWhiteSpace(activeColumnId) && string.Equals(columnId, activeColumnId, StringComparison.Ordinal))
                continue;

            editor.ClearSelection(focusEditor: false);
        }
    }

    private void WireEditorEvents(ColumnEditorControl editor, MainViewModel vm, ColumnViewModel column)
    {
        editor.EditorFocused += (_, __) =>
        {
            var selectionChanged = !string.Equals(vm.ActiveColumnId, column.Id, StringComparison.Ordinal);
            vm.ActiveColumnId = column.Id;
            ClearSelectionsExcept(column.Id);
            if (selectionChanged)
                vm.RefreshStatus();
        };

        editor.LockWidthRequested += (_, __) => RunColumnAction(vm, column, () => ToggleColumnWidthLock(editor, vm, column));
        editor.MoveLeftRequested += (_, __) => RunColumnAction(vm, column, () => MoveActiveLeft_Click(this, new RoutedEventArgs()));
        editor.MoveRightRequested += (_, __) => RunColumnAction(vm, column, () => MoveActiveRight_Click(this, new RoutedEventArgs()));
        editor.DeleteRequested += (_, __) => RunColumnAction(vm, column, RemoveActiveWithConfirmation);
        editor.ResetWidthRequested += (_, __) => RunColumnAction(vm, column, vm.ResetActiveColumnWidth);
        editor.ResizeRequested += (_, __) => RunColumnAction(vm, column, ResizeActiveColumn);
        editor.RightEdgeResizeDeltaRequested += (_, args) => ResizeColumnFromRightEdge(editor, vm, column, args.HorizontalChange);
        editor.InsertImageRequested += (_, __) => RunColumnAction(vm, column, InsertImageIntoActiveColumn);
        editor.ImageFileDropped += (_, args) => RunColumnAction(vm, column, () => ImportImageIntoActiveColumn(args.FilePath, args.Left, args.Top));
        editor.RemoveImageRequested += (_, args) => RunColumnAction(vm, column, () => RemoveImageFromActiveColumn(args.Image));
        editor.SetFontFamilyRequested += (_, __) => RunColumnAction(vm, column, SetActiveColumnFontFamily);
        editor.IncreaseFontRequested += (_, __) => RunColumnAction(vm, column, () => AdjustActiveColumnFontSize(+1));
        editor.DecreaseFontRequested += (_, __) => RunColumnAction(vm, column, () => AdjustActiveColumnFontSize(-1));
        editor.ToggleBoldRequested += (_, __) => RunColumnAction(vm, column, ToggleActiveColumnBold);
        editor.ToggleItalicRequested += (_, __) => RunColumnAction(vm, column, ToggleActiveColumnItalic);
        editor.ResetFontRequested += (_, __) => RunColumnAction(vm, column, ResetActiveColumnFont);
    }

    private void ResizeColumnFromRightEdge(ColumnEditorControl editor, MainViewModel vm, ColumnViewModel column, double horizontalChange)
    {
        if (column.IsWidthLocked || Math.Abs(horizontalChange) < 0.1)
            return;

        var gridColumn = Grid.GetColumn(editor);
        if (gridColumn < 0 || gridColumn >= ColumnsHost.ColumnDefinitions.Count)
            return;

        var columnDefinition = ColumnsHost.ColumnDefinitions[gridColumn];
        var currentWidth = columnDefinition.ActualWidth > 0
            ? columnDefinition.ActualWidth
            : column.WidthPx ?? DefaultColumnWidthPx;

        var nextWidth = Math.Clamp(currentWidth + horizontalChange, 220.0, 5000.0);
        columnDefinition.Width = new GridLength(nextWidth, GridUnitType.Pixel);
        column.WidthPx = (int)Math.Round(nextWidth);
        UpdateColumnsHostWidth(vm);

        if (!string.Equals(vm.ActiveColumnId, column.Id, StringComparison.Ordinal))
            vm.ActiveColumnId = column.Id;

        vm.StatusText = $"Set {column.Title} width to {column.WidthPx}px.";
    }

    private static void ToggleColumnWidthLock(ColumnEditorControl editor, MainViewModel vm, ColumnViewModel column)
    {
        if (!column.IsWidthLocked && !column.WidthPx.HasValue && editor.ActualWidth > 0)
            column.WidthPx = (int)Math.Round(editor.ActualWidth);

        vm.ToggleLockActiveWidth();
    }

    private void RebuildColumns()
    {
        var vm = ActiveVm;
        SyncActiveColumnVisualState(vm);
        UpdateColumnsHostScrollState(vm);

        if (ActiveWorkspace is { } workspace && vm.Columns.Count > 1)
            workspace.LastMultiColumnCount = vm.Columns.Count;

        ColumnsHost.ColumnDefinitions.Clear();
        ColumnsHost.Children.Clear();
        _editorsById.Clear();

        for (var i = 0; i < vm.Columns.Count; i++)
        {
            var column = vm.Columns[i];
            column.CanMoveLeft = i > 0;
            column.CanMoveRight = i < vm.Columns.Count - 1;
            column.IsStandaloneDocument = vm.Columns.Count == 1;

            ColumnsHost.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = column.WidthPx.HasValue && column.WidthPx.Value > 0
                    ? new GridLength(column.WidthPx.Value, GridUnitType.Pixel)
                    : new GridLength(1, GridUnitType.Star),
                MinWidth = 220
            });

            var editor = new ColumnEditorControl
            {
                DataContext = column,
                Margin = new Thickness(0)
            };

            WireEditorEvents(editor, vm, column);
            _editorsById[column.Id] = editor;

            Grid.SetColumn(editor, i);
            ColumnsHost.Children.Add(editor);
        }

        UpdateColumnsHostWidth(vm);
    }

    private void UpdateColumnsHostScrollState(MainViewModel vm)
    {
        ColumnsScrollViewer.HorizontalScrollBarVisibility = vm.Columns.Count == 1
            ? ScrollBarVisibility.Disabled
            : ScrollBarVisibility.Auto;
    }

    private void ColumnsScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Workspaces.Count > 0)
            UpdateColumnsHostWidth(ActiveVm);
    }

    private void UpdateColumnsHostWidth(MainViewModel vm)
    {
        const double minimumColumnWidth = 220.0;
        var minimumContentWidth = vm.Columns.Sum(column =>
            column.WidthPx.HasValue && column.WidthPx.Value > 0
                ? column.WidthPx.Value
                : minimumColumnWidth);
        var viewportWidth = ColumnsScrollViewer.ViewportWidth > 0
            ? ColumnsScrollViewer.ViewportWidth
            : ColumnsScrollViewer.ActualWidth;

        ColumnsHost.Width = Math.Max(minimumContentWidth, viewportWidth);
    }

    private void PersistWidthsFromGrid()
    {
        var vm = ActiveVm;

        for (var columnIndex = 0; columnIndex < ColumnsHost.ColumnDefinitions.Count; columnIndex++)
        {
            if (columnIndex >= vm.Columns.Count)
                break;

            var definition = ColumnsHost.ColumnDefinitions[columnIndex];
            if (!vm.Columns[columnIndex].WidthPx.HasValue)
                continue;

            var width = (int)Math.Round(definition.ActualWidth);
            if (width > 0)
                vm.Columns[columnIndex].WidthPx = width;
        }
    }
}
