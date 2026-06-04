using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private const double WorkspaceTabScrollStep = 160.0;
    private ScrollViewer? _workspaceTabScrollViewer;

    private void NewWorkspaceTab_Click(object sender, RoutedEventArgs e)
    {
        AddWorkspace();
    }

    private void AddWorkspace()
    {
        var ws = CreateWorkspace(NextWorkspaceName());
        ActiveWorkspace = ws;
        WorkspaceTabs.SelectedItem = ws;
        QueueWorkspaceTabScrollRefresh(scrollSelectedIntoView: true);
    }

    private void CloseWorkspaceTab_Click(object sender, RoutedEventArgs e)
    {
        if (!WorkspaceLifecycleService.CanCloseWorkspace(Workspaces.Count))
        {
            ActiveVm.StatusText = "At least one workspace is required.";
            return;
        }

        var current = ResolveWorkspaceFromSender(sender);
        if (current is null)
            return;

        if (!ConfirmWorkspaceDestructiveAction(current, "Close Workspace", $"Closing {current.Name}"))
            return;

        var currentIndex = Workspaces.IndexOf(current);
        if (currentIndex < 0)
            return;

        var wasActive = ReferenceEquals(current, ActiveWorkspace);
        Workspaces.RemoveAt(currentIndex);

        if (!wasActive)
            return;

        var nextIndex = WorkspaceLifecycleService.NextActiveWorkspaceIndexAfterClose(currentIndex, Workspaces.Count);
        ActiveWorkspace = Workspaces[nextIndex];
        WorkspaceTabs.SelectedItem = ActiveWorkspace;
        QueueWorkspaceTabScrollRefresh(scrollSelectedIntoView: true);
    }

    private void WorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkspaceTabs.SelectedItem is WorkspaceSession ws && !ReferenceEquals(ws, ActiveWorkspace))
            ActiveWorkspace = ws;

        QueueWorkspaceTabScrollRefresh(scrollSelectedIntoView: true);
    }

    private void WorkspaceRename_Click(object sender, RoutedEventArgs e)
    {
        var ws = ResolveWorkspaceFromSender(sender);
        if (ws is null)
            return;

        ActiveWorkspace = ws;
        ws.IsRenaming = true;
    }

    private void WorkspaceAdd_Click(object sender, RoutedEventArgs e)
    {
        AddWorkspace();
    }

    private void WorkspaceTabs_Loaded(object sender, RoutedEventArgs e)
    {
        ResolveWorkspaceTabScrollViewer();
        QueueWorkspaceTabScrollRefresh(scrollSelectedIntoView: true);
    }

    private void WorkspaceTabs_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueWorkspaceTabScrollRefresh();
    }

    private void Workspaces_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueWorkspaceTabScrollRefresh(scrollSelectedIntoView: true);
    }

    private void WorkspaceScrollLeft_Click(object sender, RoutedEventArgs e)
    {
        if (ResolveWorkspaceTabScrollViewer() is not { } viewer)
            return;

        viewer.ScrollToHorizontalOffset(Math.Max(0, viewer.HorizontalOffset - WorkspaceTabScrollStep));
        UpdateWorkspaceTabScrollButtons();
    }

    private void WorkspaceScrollRight_Click(object sender, RoutedEventArgs e)
    {
        if (ResolveWorkspaceTabScrollViewer() is not { } viewer)
            return;

        viewer.ScrollToHorizontalOffset(Math.Min(viewer.ScrollableWidth, viewer.HorizontalOffset + WorkspaceTabScrollStep));
        UpdateWorkspaceTabScrollButtons();
    }

    private void WorkspaceTabScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        UpdateWorkspaceTabScrollButtons();
    }

    private WorkspaceSession? ResolveWorkspaceFromSender(object sender)
    {
        if (sender is FrameworkElement { DataContext: WorkspaceSession ws })
            return ws;

        return ActiveWorkspace;
    }

    private void WorkspaceTabs_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        var tabItem = FindAncestor<TabItem>(source);
        if (tabItem?.DataContext is not WorkspaceSession ws)
            return;

        ActiveWorkspace = ws;
        WorkspaceTabs.SelectedItem = ws;
        QueueWorkspaceTabScrollRefresh(scrollSelectedIntoView: true);
    }

    private void QueueWorkspaceTabScrollRefresh(bool scrollSelectedIntoView = false)
    {
        Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            new Action(() =>
            {
                if (scrollSelectedIntoView)
                    ScrollSelectedWorkspaceIntoView();

                UpdateWorkspaceTabScrollButtons();
            }));
    }

    private void ScrollSelectedWorkspaceIntoView()
    {
        WorkspaceTabs.UpdateLayout();

        if (WorkspaceTabs.SelectedItem is null)
            return;

        if (WorkspaceTabs.ItemContainerGenerator.ContainerFromItem(WorkspaceTabs.SelectedItem) is TabItem tabItem)
            tabItem.BringIntoView();
    }

    private void UpdateWorkspaceTabScrollButtons()
    {
        var viewer = ResolveWorkspaceTabScrollViewer();
        var hasOverflow = viewer is not null && viewer.ScrollableWidth > 0.5;
        var visibility = hasOverflow ? Visibility.Visible : Visibility.Collapsed;

        WorkspaceScrollLeftButton.Visibility = visibility;
        WorkspaceScrollRightButton.Visibility = visibility;

        if (!hasOverflow || viewer is null)
        {
            WorkspaceScrollLeftButton.IsEnabled = false;
            WorkspaceScrollRightButton.IsEnabled = false;
            return;
        }

        WorkspaceScrollLeftButton.IsEnabled = viewer.HorizontalOffset > 0.5;
        WorkspaceScrollRightButton.IsEnabled = viewer.HorizontalOffset < viewer.ScrollableWidth - 0.5;
    }

    private ScrollViewer? ResolveWorkspaceTabScrollViewer()
    {
        WorkspaceTabs.ApplyTemplate();
        var viewer = WorkspaceTabs.Template.FindName("WorkspaceTabHeaderScrollViewer", WorkspaceTabs) as ScrollViewer
                     ?? FindDescendant<ScrollViewer>(WorkspaceTabs);

        if (ReferenceEquals(_workspaceTabScrollViewer, viewer))
            return _workspaceTabScrollViewer;

        if (_workspaceTabScrollViewer is not null)
            _workspaceTabScrollViewer.ScrollChanged -= WorkspaceTabScrollViewer_ScrollChanged;

        _workspaceTabScrollViewer = viewer;

        if (_workspaceTabScrollViewer is not null)
            _workspaceTabScrollViewer.ScrollChanged += WorkspaceTabScrollViewer_ScrollChanged;

        return _workspaceTabScrollViewer;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static T? FindDescendant<T>(DependencyObject? source) where T : DependencyObject
    {
        if (source is null)
            return null;

        var childCount = VisualTreeHelper.GetChildrenCount(source);
        for (var i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(source, i);
            if (child is T match)
                return match;

            var descendant = FindDescendant<T>(child);
            if (descendant is not null)
                return descendant;
        }

        return null;
    }
}
