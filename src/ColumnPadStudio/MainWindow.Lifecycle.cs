using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Windows;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private bool TryOfferAutoRecovery()
    {
        try
        {
            if (!WorkspaceRecoveryStore.TryLoad(out var snapshot))
                return false;

            var localTime = snapshot.SavedUtc.ToLocalTime();
            var workspaceText = snapshot.Workspaces.Count == 1
                ? "1 workspace"
                : $"{snapshot.Workspaces.Count} workspaces";
            var msg = $"Auto-recovery data for {workspaceText} from {localTime:yyyy-MM-dd HH:mm:ss} was found.{Environment.NewLine}{Environment.NewLine}Restore it now?";
            var result = MessageBox.Show(this, msg, "ColumnPad Recovery", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes && RestoreAutoRecovery(snapshot))
                return true;

            WorkspaceRecoveryStore.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WorkspaceRecoveryStore.Clear();
        }

        return false;
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            PersistWidthsFromGrid();
            SaveAutoRecoverySnapshot();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Auto-save should never interrupt editing.
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _autoSaveTimer.Stop();

        if (TryConfirmSaveBeforeExit())
            return;

        e.Cancel = true;
        _autoSaveTimer.Start();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _workflowBuilderWindow?.Close();
        _workflowBuilderWindow = null;

        try
        {
            WorkspaceRecoveryStore.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup.
        }
    }

    private bool RestoreAutoRecovery(WorkspaceRecoverySnapshot snapshot)
    {
        Workspaces.Clear();

        foreach (var workspace in snapshot.Workspaces)
        {
            var vm = new MainViewModel();
            ApplyAppThemePreference(vm);
            if (!vm.LoadRecoverySnapshot(workspace, preserveCurrentTheme: true))
                continue;

            CreateWorkspace(workspace.Name, vm);
        }

        if (Workspaces.Count == 0)
            return false;

        var activeIndex = Math.Clamp(snapshot.ActiveWorkspaceIndex, 0, Workspaces.Count - 1);
        ActiveWorkspace = Workspaces[activeIndex];
        WorkspaceTabs.SelectedItem = ActiveWorkspace;
        ActiveVm.StatusText = Workspaces.Count == 1
            ? "Recovered 1 workspace."
            : $"Recovered {Workspaces.Count} workspaces.";
        return true;
    }

    private void SaveAutoRecoverySnapshot()
    {
        if (Workspaces.Count == 0)
            return;

        var recoveryWorkspaces = Workspaces
            .Select(workspace => new WorkspaceRecoveryWorkspace(
                workspace.Name,
                workspace.Vm.ToLayoutJson(),
                workspace.Vm.CurrentFilePath,
                workspace.Vm.CurrentFileKind,
                workspace.Vm.IsDirty,
                workspace.Vm.RequiresSaveAsBeforeOverwrite))
            .ToList();

        var activeIndex = ActiveWorkspace is null ? 0 : Math.Max(0, Workspaces.IndexOf(ActiveWorkspace));
        WorkspaceRecoveryStore.Save(recoveryWorkspaces, activeIndex);
    }
}
