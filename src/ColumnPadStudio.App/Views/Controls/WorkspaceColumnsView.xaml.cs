using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using ColumnPadStudio.App.ViewModels;
using ColumnPadStudio.Domain.Logic;

namespace ColumnPadStudio.App.Views.Controls;

public partial class WorkspaceColumnsView : UserControl
{
    private const double MinimumColumnViewportHeight = 620;
    private const double SplitterWidth = 8;
    private WorkspaceViewModel? _workspace;
    private bool _rebuildPending;

    public WorkspaceColumnsView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => UpdateLayoutWidth();
        WorkspaceScrollViewer.SizeChanged += (_, _) => UpdateLayoutWidth();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeWorkspace(_workspace);
        SubscribeWorkspace(DataContext as WorkspaceViewModel);
        RebuildColumns();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeWorkspace(_workspace);
        _workspace = null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UnsubscribeWorkspace(e.OldValue as WorkspaceViewModel);
        SubscribeWorkspace(e.NewValue as WorkspaceViewModel);
        RebuildColumns();
    }

    private void SubscribeWorkspace(WorkspaceViewModel? workspace)
    {
        _workspace = workspace;
        if (_workspace is null)
        {
            return;
        }

        _workspace.Columns.CollectionChanged += ColumnsOnCollectionChanged;
        foreach (var column in _workspace.Columns)
        {
            column.PropertyChanged += ColumnOnPropertyChanged;
        }
    }

    private void UnsubscribeWorkspace(WorkspaceViewModel? workspace)
    {
        if (workspace is null)
        {
            return;
        }

        workspace.Columns.CollectionChanged -= ColumnsOnCollectionChanged;
        foreach (var column in workspace.Columns)
        {
            column.PropertyChanged -= ColumnOnPropertyChanged;
        }
    }

    private void ColumnsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (ColumnViewModel column in e.OldItems)
            {
                column.PropertyChanged -= ColumnOnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (ColumnViewModel column in e.NewItems)
            {
                column.PropertyChanged += ColumnOnPropertyChanged;
            }
        }

        ScheduleRebuild();
    }

    private void ColumnOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ColumnViewModel column)
        {
            return;
        }

        if (e.PropertyName == nameof(ColumnViewModel.IsWidthLocked) && column.IsWidthLocked && !column.HasStoredWidth)
        {
            CaptureCurrentWidth(column);
        }

        if (e.PropertyName is nameof(ColumnViewModel.StoredWidth) or nameof(ColumnViewModel.IsWidthLocked))
        {
            ScheduleRebuild();
        }
    }

    private void ScheduleRebuild()
    {
        if (_rebuildPending)
        {
            return;
        }

        _rebuildPending = true;
        Dispatcher.InvokeAsync(() =>
        {
            _rebuildPending = false;
            RebuildColumns();
        });
    }

    private void RebuildColumns()
    {
        ColumnsGrid.Children.Clear();
        ColumnsGrid.ColumnDefinitions.Clear();

        if (_workspace is null || _workspace.Columns.Count == 0)
        {
            ColumnsGrid.Width = 0;
            return;
        }

        for (var index = 0; index < _workspace.Columns.Count; index++)
        {
            var column = _workspace.Columns[index];
            ColumnsGrid.ColumnDefinitions.Add(CreateColumnDefinition(column));

            var editor = new ColumnEditorView
            {
                DataContext = column,
                MinHeight = MinimumColumnViewportHeight,
                VerticalAlignment = VerticalAlignment.Top
            };

            Grid.SetColumn(editor, index * 2);
            ColumnsGrid.Children.Add(editor);

            if (index >= _workspace.Columns.Count - 1)
            {
                continue;
            }

            ColumnsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(SplitterWidth, GridUnitType.Pixel) });
            var splitter = new Thumb
            {
                Style = (Style)Resources["ColumnSplitterStyle"],
                Tag = index,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 8, 0, 8),
                IsEnabled = !column.IsWidthLocked && !_workspace.Columns[index + 1].IsWidthLocked
            };
            splitter.DragStarted += SplitterOnDragStarted;
            splitter.DragDelta += SplitterOnDragDelta;
            splitter.DragCompleted += SplitterOnDragCompleted;
            Grid.SetColumn(splitter, index * 2 + 1);
            ColumnsGrid.Children.Add(splitter);
        }

        UpdateLayoutWidth();
    }

    private void UpdateLayoutWidth()
    {
        if (_workspace is null || _workspace.Columns.Count == 0)
        {
            return;
        }

        var viewportWidth = Math.Max(
            WorkspaceScrollViewer.ViewportWidth,
            Math.Max(0, WorkspaceScrollViewer.ActualWidth - WorkspaceScrollViewer.Padding.Left - WorkspaceScrollViewer.Padding.Right));

        ColumnsGrid.Width = ColumnWidthLogic.GetDesiredWorkspaceWidth(
            _workspace.Columns.Select(column => column.StoredWidth),
            viewportWidth,
            SplitterWidth);
    }

    private void SplitterOnDragStarted(object sender, DragStartedEventArgs e)
    {
        if (_workspace is null || sender is not Thumb thumb || thumb.Tag is not int leftIndex)
        {
            return;
        }

        _workspace.SelectColumn(_workspace.Columns[leftIndex]);
        EnsurePixelWidths(leftIndex);
    }

    private void SplitterOnDragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_workspace is null || sender is not Thumb thumb || thumb.Tag is not int leftIndex || !thumb.IsEnabled)
        {
            return;
        }

        var leftDefinition = GetColumnDefinition(leftIndex);
        var rightDefinition = GetColumnDefinition(leftIndex + 1);
        if (leftDefinition is null || rightDefinition is null)
        {
            return;
        }

        EnsurePixelWidths(leftIndex);
        var (leftWidth, rightWidth) = ColumnWidthLogic.ApplySplitterDelta(leftDefinition.ActualWidth, rightDefinition.ActualWidth, e.HorizontalChange);
        leftDefinition.Width = new GridLength(leftWidth, GridUnitType.Pixel);
        rightDefinition.Width = new GridLength(rightWidth, GridUnitType.Pixel);
    }

    private void SplitterOnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_workspace is null || sender is not Thumb thumb || thumb.Tag is not int leftIndex)
        {
            return;
        }

        var leftDefinition = GetColumnDefinition(leftIndex);
        var rightDefinition = GetColumnDefinition(leftIndex + 1);
        if (leftDefinition is null || rightDefinition is null)
        {
            return;
        }

        _workspace.Columns[leftIndex].StoredWidth = leftDefinition.ActualWidth;
        _workspace.Columns[leftIndex + 1].StoredWidth = rightDefinition.ActualWidth;
    }

    private void EnsurePixelWidths(int leftIndex)
    {
        var leftDefinition = GetColumnDefinition(leftIndex);
        var rightDefinition = GetColumnDefinition(leftIndex + 1);
        if (leftDefinition is null || rightDefinition is null)
        {
            return;
        }

        leftDefinition.Width = new GridLength(Math.Max(WorkspaceRules.MinColumnWidth, leftDefinition.ActualWidth), GridUnitType.Pixel);
        rightDefinition.Width = new GridLength(Math.Max(WorkspaceRules.MinColumnWidth, rightDefinition.ActualWidth), GridUnitType.Pixel);
    }

    private ColumnDefinition? GetColumnDefinition(int columnIndex)
    {
        var definitionIndex = columnIndex * 2;
        return definitionIndex >= 0 && definitionIndex < ColumnsGrid.ColumnDefinitions.Count
            ? ColumnsGrid.ColumnDefinitions[definitionIndex]
            : null;
    }

    private void CaptureCurrentWidth(ColumnViewModel column)
    {
        if (_workspace is null)
        {
            return;
        }

        var columnIndex = _workspace.Columns.IndexOf(column);
        var columnDefinition = GetColumnDefinition(columnIndex);
        if (columnDefinition is null || columnDefinition.ActualWidth <= 0)
        {
            return;
        }

        column.StoredWidth = columnDefinition.ActualWidth;
    }

    private static ColumnDefinition CreateColumnDefinition(ColumnViewModel column)
    {
        return new ColumnDefinition
        {
            MinWidth = WorkspaceRules.MinColumnWidth,
            Width = column.StoredWidth.HasValue
                ? new GridLength(column.StoredWidth.Value, GridUnitType.Pixel)
                : new GridLength(1, GridUnitType.Star)
        };
    }
}
