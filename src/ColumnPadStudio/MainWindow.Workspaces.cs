using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private void NewWorkspaceTab_Click(object sender, RoutedEventArgs e)
    {
        AddWorkspace();
    }

    private void AddWorkspace()
    {
        var ws = CreateWorkspace(NextWorkspaceName());
        ActiveWorkspace = ws;
        WorkspaceTabs.SelectedItem = ws;
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
    }

    private void WorkspaceTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkspaceTabs.SelectedItem is WorkspaceSession ws && !ReferenceEquals(ws, ActiveWorkspace))
            ActiveWorkspace = ws;
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
}
