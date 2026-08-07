using ColumnPadStudio.Controls;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.Models;
using ColumnPadStudio.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private readonly WorkspaceColumnEditorCache _columnEditorCache = new();
    private readonly Dictionary<string, ColumnEditorControl> _editorsById = new(StringComparer.Ordinal);

    private bool UseFixedColumnStrip(MainViewModel vm)
    {
        return WorkspaceColumnLayout.UsesFixedColumnStrip(
            vm.Columns.Count,
            _appPreferences.FitColumnsToWindow);
    }

    private bool UseColumnGaps(MainViewModel vm)
    {
        return _appPreferences.SnapAllColumnsEnabled && vm.Columns.Count > 1;
    }

    private static int GetColumnGridIndex(int columnIndex, bool useColumnGaps)
    {
        return useColumnGaps ? columnIndex * 2 : columnIndex;
    }

    private void UpdateSnapAllColumns(bool enabled)
    {
        if (_appPreferences.SnapAllColumnsEnabled == enabled)
            return;

        _appPreferences = _appPreferences with { SnapAllColumnsEnabled = enabled };
        PersistAppPreferences();
        RaisePropertyChanged(nameof(SnapAllColumnsEnabled));

        if (Workspaces.Count == 0)
            return;

        RebuildColumns();
        ActiveVm.StatusText = enabled
            ? $"All columns snapped with a {_appPreferences.ColumnSpacingPx}px gap. Saved pixel widths were kept."
            : "All columns unsnapped. Saved pixel widths were kept.";
    }

    private void UpdateFitColumnsToWindow(bool enabled)
    {
        if (_appPreferences.FitColumnsToWindow == enabled)
        {
            RefreshColumnWidthPreferenceBindings();
            return;
        }

        _appPreferences = _appPreferences with { FitColumnsToWindow = enabled };
        PersistAppPreferences();
        RefreshColumnWidthPreferenceBindings();

        if (Workspaces.Count == 0)
            return;

        RebuildColumns();
        if (ActiveVm.Columns.Count <= 1)
        {
            ActiveVm.StatusText = enabled
                ? "Single Text Mode already fills the window. Fit Columns to Window will apply when Column Mode is restored."
                : "Single Text Mode still fills the window. Standard or Custom widths will apply when Column Mode is restored.";
            return;
        }

        ActiveVm.StatusText = enabled
            ? "Columns now fit the window equally. Saved pixel widths were kept."
            : $"Columns restored to their saved widths or the default {_appPreferences.DefaultColumnWidthPx}px width.";
    }

    private void UpdateDefaultColumnWidth(int widthPx)
    {
        var normalizedWidth = WorkspaceConstraints.ClampColumnWidth(widthPx);
        var changed = _appPreferences.DefaultColumnWidthPx != normalizedWidth || _appPreferences.FitColumnsToWindow;
        _appPreferences = _appPreferences with
        {
            DefaultColumnWidthPx = normalizedWidth,
            FitColumnsToWindow = false
        };

        if (changed)
            PersistAppPreferences();

        RefreshColumnWidthPreferenceBindings();
        if (Workspaces.Count == 0)
            return;

        RebuildColumns();
        ActiveVm.StatusText = normalizedWidth == (int)WorkspaceConstraints.DefaultColumnWidth
            ? $"Standard column width restored to {normalizedWidth}px. Individually resized columns were kept."
            : $"Default column width set to {normalizedWidth}px. Individually resized columns were kept.";
    }

    private void RefreshColumnWidthPreferenceBindings()
    {
        RaisePropertyChanged(nameof(FitColumnsToWindow));
        RaisePropertyChanged(nameof(IsStandardColumnWidthSelected));
        RaisePropertyChanged(nameof(IsCustomColumnWidthSelected));
        RaisePropertyChanged(nameof(CustomColumnWidthMenuHeader));
        RaisePropertyChanged(nameof(CanManageColumnWidths));
    }

    private void UpdateColumnSpacing(int columnSpacingPx)
    {
        var normalizedSpacing = AppPreferences.NormalizeColumnSpacing(columnSpacingPx);
        if (_appPreferences.ColumnSpacingPx == normalizedSpacing)
        {
            return;
        }

        _appPreferences = _appPreferences with
        {
            ColumnSpacingPx = normalizedSpacing
        };

        PersistAppPreferences();
        RaisePropertyChanged(nameof(ColumnSpacingMenuHeader));

        if (Workspaces.Count > 0)
        {
            RebuildColumns();
            ActiveVm.StatusText = _appPreferences.SnapAllColumnsEnabled
                ? $"Column gap set to {normalizedSpacing}px for all snapped columns."
                : $"Column gap saved as {normalizedSpacing}px. It will apply when all columns are snapped.";
        }
    }

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

    private static string GetWidthManagementUnavailableStatus(MainViewModel vm, string action)
    {
        return vm.Columns.Count <= 1
            ? $"Single Text Mode fills the window. Switch to Column Mode before {action}."
            : $"Choose Standard or Custom column width before {action}.";
    }

    private void ResetSelectedColumnToDefault(MainViewModel vm)
    {
        vm.ResetActiveColumnWidth(_appPreferences.DefaultColumnWidthPx);
        if (_appPreferences.FitColumnsToWindow)
            vm.StatusText += " It will be visible when Standard or Custom sizing is selected.";
    }

    private void ResetAllColumnsToDefault(MainViewModel vm)
    {
        vm.ResetAllColumnWidths(_appPreferences.DefaultColumnWidthPx);
        if (_appPreferences.FitColumnsToWindow)
            vm.StatusText += " It will be visible when Standard or Custom sizing is selected.";
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

    private void ActivateColumn(MainViewModel vm, ColumnViewModel column)
    {
        var selectionChanged = !string.Equals(vm.ActiveColumnId, column.Id, StringComparison.Ordinal);
        vm.ActiveColumnId = column.Id;
        ClearSelectionsExcept(column.Id);
        if (selectionChanged)
            vm.RefreshStatus();
    }

    private void WireEditorEvents(ColumnEditorControl editor, MainViewModel vm, ColumnViewModel column)
    {
        editor.EditorFocused += (_, __) => ActivateColumn(vm, column);
        editor.ColumnActionsOpening += (_, __) => ActivateColumn(vm, column);

        editor.LockWidthRequested += (_, __) => RunColumnAction(vm, column, () => ToggleColumnWidthLock(editor, vm, column));
        editor.MoveLeftRequested += (_, __) => RunColumnAction(vm, column, () => MoveActiveLeft_Click(this, new RoutedEventArgs()));
        editor.MoveRightRequested += (_, __) => RunColumnAction(vm, column, () => MoveActiveRight_Click(this, new RoutedEventArgs()));
        editor.DeleteRequested += (_, __) => RunColumnAction(vm, column, RemoveActiveWithConfirmation);
        editor.ResetWidthRequested += (_, __) => RunColumnAction(
            vm,
            column,
            () => ResetSelectedColumnToDefault(vm));
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
        editor.SetTextColorRequested += (_, args) => RunColumnAction(vm, column, () => SetActiveColumnTextColor(args.Value));
        editor.SetCustomTextColorRequested += (_, __) => RunColumnAction(vm, column, SetActiveColumnCustomTextColor);
    }

    private void ResizeColumnFromRightEdge(ColumnEditorControl editor, MainViewModel vm, ColumnViewModel column, double horizontalChange)
    {
        if (!column.IsWidthManagementEnabled || column.IsWidthLocked || Math.Abs(horizontalChange) < 0.1)
            return;

        var gridColumn = Grid.GetColumn(editor);
        if (gridColumn < 0 || gridColumn >= ColumnsHost.ColumnDefinitions.Count)
            return;

        var columnDefinition = ColumnsHost.ColumnDefinitions[gridColumn];
        var currentWidth = columnDefinition.ActualWidth > 0
            ? columnDefinition.ActualWidth
            : column.WidthPx ?? _appPreferences.DefaultColumnWidthPx;

        var nextWidth = WorkspaceConstraints.ClampColumnWidth(currentWidth + horizontalChange);
        columnDefinition.Width = new GridLength(nextWidth, GridUnitType.Pixel);
        column.WidthPx = (int)Math.Round(nextWidth);
        UpdateColumnsHostWidth(vm);

        if (!string.Equals(vm.ActiveColumnId, column.Id, StringComparison.Ordinal))
            vm.ActiveColumnId = column.Id;

        vm.StatusText = $"Set {column.Title} width to {column.WidthPx}px.";
    }

    private void ToggleColumnWidthLock(ColumnEditorControl editor, MainViewModel vm, ColumnViewModel column)
    {
        if (!column.IsWidthManagementEnabled)
        {
            vm.StatusText = GetWidthManagementUnavailableStatus(vm, "freezing a column width");
            return;
        }

        if (!column.IsWidthLocked && !column.WidthPx.HasValue)
        {
            var resolvedWidth = editor.ActualWidth > 0
                ? editor.ActualWidth
                : _appPreferences.DefaultColumnWidthPx;
            column.WidthPx = WorkspaceConstraints.ClampColumnWidth((int)Math.Round(resolvedWidth));
        }

        vm.ToggleLockActiveWidth();
    }

    private void RebuildColumns()
    {
        if (ActiveWorkspace is not { } workspace)
        {
            ColumnsHost.ColumnDefinitions.Clear();
            ColumnsHost.Children.Clear();
            _editorsById.Clear();
            RaisePropertyChanged(nameof(CanManageColumnWidths));
            return;
        }

        var vm = workspace.Vm;
        var useFixedColumnStrip = UseFixedColumnStrip(vm);
        var useColumnGaps = UseColumnGaps(vm);
        SyncActiveColumnVisualState(vm);

        if (vm.Columns.Count > 1)
            workspace.LastMultiColumnCount = vm.Columns.Count;

        var currentColumns = vm.Columns.ToDictionary(column => column.Id, StringComparer.Ordinal);
        DetachCachedEditors(_columnEditorCache.RemoveColumnsExcept(workspace, currentColumns));

        ColumnsHost.ColumnDefinitions.Clear();
        _editorsById.Clear();
        var desiredEditors = new List<ColumnEditorControl>(vm.Columns.Count);

        for (var i = 0; i < vm.Columns.Count; i++)
        {
            var column = vm.Columns[i];
            column.CanMoveLeft = i > 0;
            column.CanMoveRight = i < vm.Columns.Count - 1;
            column.IsStandaloneDocument = vm.Columns.Count == 1;
            column.IsWidthManagementEnabled = useFixedColumnStrip;

            ColumnsHost.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = useFixedColumnStrip
                    ? new GridLength(
                        WorkspaceColumnLayout.ResolveColumnWidth(
                            column.WidthPx,
                            _appPreferences.DefaultColumnWidthPx),
                        GridUnitType.Pixel)
                    : new GridLength(1, GridUnitType.Star),
                MinWidth = WorkspaceConstraints.MinimumColumnWidth
            });

            if (useColumnGaps && i < vm.Columns.Count - 1)
            {
                ColumnsHost.ColumnDefinitions.Add(new ColumnDefinition
                {
                    Width = new GridLength(_appPreferences.ColumnSpacingPx, GridUnitType.Pixel)
                });
            }

            var editor = _columnEditorCache.GetOrCreate(
                workspace,
                column.Id,
                column,
                () =>
                {
                    var newEditor = new ColumnEditorControl
                    {
                        DataContext = column,
                        Margin = new Thickness(0)
                    };

                    WireEditorEvents(newEditor, vm, column);
                    return newEditor;
                },
                out var replacedEditor);

            if (replacedEditor is not null)
                DetachCachedEditors([replacedEditor]);

            desiredEditors.Add(editor);
            _editorsById[column.Id] = editor;
            Grid.SetColumn(editor, GetColumnGridIndex(i, useColumnGaps));
        }

        var desiredEditorSet = new HashSet<ColumnEditorControl>(desiredEditors);
        for (var childIndex = ColumnsHost.Children.Count - 1; childIndex >= 0; childIndex--)
        {
            if (ColumnsHost.Children[childIndex] is not ColumnEditorControl editor
                || !desiredEditorSet.Contains(editor))
            {
                ColumnsHost.Children.RemoveAt(childIndex);
            }
        }

        foreach (var editor in desiredEditors)
        {
            if (!ColumnsHost.Children.Contains(editor))
                ColumnsHost.Children.Add(editor);
        }

        UpdateColumnsHostWidth(vm);
        RaisePropertyChanged(nameof(CanManageColumnWidths));
    }

    private void DetachCachedEditors(IEnumerable<ColumnEditorControl> editors)
    {
        foreach (var editor in editors.Distinct())
        {
            if (ColumnsHost.Children.Contains(editor))
                ColumnsHost.Children.Remove(editor);

            var activeIds = _editorsById
                .Where(entry => ReferenceEquals(entry.Value, editor))
                .Select(entry => entry.Key)
                .ToArray();

            foreach (var activeId in activeIds)
                _editorsById.Remove(activeId);
        }
    }

    private void ColumnsScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (Workspaces.Count > 0)
            UpdateColumnsHostWidth(ActiveVm);
    }

    private void UpdateColumnsHostWidth(MainViewModel vm)
    {
        var viewportWidth = ColumnsScrollViewer.ViewportWidth > 0
            ? ColumnsScrollViewer.ViewportWidth
            : ColumnsScrollViewer.ActualWidth;

        ColumnsHost.Width = WorkspaceColumnLayout.CalculateHostWidth(
            vm.Columns.Select(column => column.WidthPx).ToArray(),
            viewportWidth,
            _appPreferences.ColumnSpacingPx,
            _appPreferences.SnapAllColumnsEnabled,
            _appPreferences.FitColumnsToWindow,
            _appPreferences.DefaultColumnWidthPx);
    }

    private void PersistWidthsFromGrid()
    {
        var vm = ActiveVm;
        if (!UseFixedColumnStrip(vm))
            return;

        var useColumnGaps = UseColumnGaps(vm);

        for (var columnIndex = 0; columnIndex < vm.Columns.Count; columnIndex++)
        {
            var gridColumnIndex = GetColumnGridIndex(columnIndex, useColumnGaps);
            if (gridColumnIndex >= ColumnsHost.ColumnDefinitions.Count)
                break;

            var definition = ColumnsHost.ColumnDefinitions[gridColumnIndex];
            if (!vm.Columns[columnIndex].WidthPx.HasValue)
                continue;

            var width = (int)Math.Round(definition.ActualWidth);
            if (width > 0)
                vm.Columns[columnIndex].WidthPx = width;
        }
    }
}
