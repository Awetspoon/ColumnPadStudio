using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;

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

            WorkspaceRecoveryStore.TryClear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            WorkspaceRecoveryStore.TryClear();
        }

        return false;
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        try
        {
            PersistWidthsFromGrid();
            var snapshot = CaptureAutoRecoverySnapshot();
            if (snapshot is not null)
                _autoRecoveryWriter.Queue(snapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            UpdateAutoRecoveryStatus(ex);
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowCloseAfterRecoveryShutdown || IsCrashRecoveryPreservationRequested())
            return;

        e.Cancel = true;
        if (_closeAttemptInProgress)
            return;

        _closeAttemptInProgress = true;
        _autoSaveTimer.Stop();
        var pauseTask = _autoRecoveryWriter.PauseAsync();

        if (!TryConfirmSaveBeforeExit())
        {
            await pauseTask;
            if (TryResumeAutoRecoveryAfterCancelledClose())
                _autoSaveTimer.Start();

            _closeAttemptInProgress = false;
            return;
        }

        CancellationToken clearCancellationToken;
        lock (_recoveryLifecycleGate)
        {
            if (_recoveryLifecycleState == RecoveryLifecycleState.PreserveForCrash)
            {
                _closeAttemptInProgress = false;
                e.Cancel = false;
                return;
            }

            _recoveryLifecycleState = RecoveryLifecycleState.CleanClose;
            _recoveryClearCancellation = new CancellationTokenSource();
            clearCancellationToken = _recoveryClearCancellation.Token;
        }

        await pauseTask;
        if (IsCrashRecoveryPreservationRequested())
            return;

        await Task.Run(() => WorkspaceRecoveryStore.TryClear(cancellationToken: clearCancellationToken));

        lock (_recoveryLifecycleGate)
        {
            if (_recoveryLifecycleState == RecoveryLifecycleState.PreserveForCrash)
                return;

            _recoveryClearCancellation?.Dispose();
            _recoveryClearCancellation = null;
            _autoRecoveryWriter.Dispose();
            _allowCloseAfterRecoveryShutdown = true;
            _closeAttemptInProgress = false;
        }

        Close();
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _workflowBuilderWindow?.Close();
        _workflowBuilderWindow = null;
        DetachCachedEditors(_columnEditorCache.Clear());
        _editorsById.Clear();
        _autoRecoveryWriter.StopAcceptingWithoutCancellation();
    }

    internal void PreserveRecoveryForAbnormalShutdown()
    {
        CancellationTokenSource? clearCancellation;
        lock (_recoveryLifecycleGate)
        {
            _recoveryLifecycleState = RecoveryLifecycleState.PreserveForCrash;
            clearCancellation = _recoveryClearCancellation;
        }

        clearCancellation?.Cancel();
        _autoRecoveryWriter.StopAcceptingWithoutCancellation();
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

            var restoredWorkspace = CreateWorkspace(workspace.Name, vm);
            restoredWorkspace.LastMultiColumnCount = workspace.LastMultiColumnCount;
            restoredWorkspace.MarkSessionClean();
            if (workspace.HasSessionChanges)
                restoredWorkspace.ForceSessionDirty();
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

    private CapturedRecoverySnapshot? CaptureAutoRecoverySnapshot()
    {
        Dispatcher.VerifyAccess();
        if (Workspaces.Count == 0)
            return null;

        var recoveryWorkspaces = Workspaces
            .Select(workspace => new CapturedRecoveryWorkspace(
                workspace.Name,
                workspace.Vm.CaptureRecoveryLayoutSnapshot(),
                workspace.Vm.CurrentFilePath,
                workspace.Vm.CurrentFileKind,
                workspace.Vm.IsDirty,
                workspace.Vm.RequiresSaveAsBeforeOverwrite,
                workspace.LastMultiColumnCount,
                workspace.HasSessionChanges))
            .ToList()
            .AsReadOnly();

        var activeIndex = ActiveWorkspace is null ? 0 : Math.Max(0, Workspaces.IndexOf(ActiveWorkspace));
        return new CapturedRecoverySnapshot(activeIndex, recoveryWorkspaces);
    }

    private static Task WriteCapturedRecoveryAsync(
        CapturedRecoverySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var workspaces = new List<WorkspaceRecoveryWorkspace>(snapshot.Workspaces.Count);
        foreach (var workspace in snapshot.Workspaces)
        {
            cancellationToken.ThrowIfCancellationRequested();
            workspaces.Add(new WorkspaceRecoveryWorkspace(
                workspace.Name,
                MainViewModel.SerializeLayoutSnapshot(workspace.LayoutSnapshot),
                workspace.CurrentFilePath,
                workspace.CurrentFileKind,
                workspace.IsDirty,
                workspace.RequiresSaveAsBeforeOverwrite,
                workspace.LastMultiColumnCount,
                workspace.HasSessionChanges));
        }

        WorkspaceRecoveryStore.Save(
            workspaces,
            snapshot.ActiveWorkspaceIndex,
            cancellationToken: cancellationToken);
        return Task.CompletedTask;
    }

    private void ReportAutoRecoveryWriteResult(Exception? error)
    {
        if (Dispatcher.CheckAccess())
        {
            UpdateAutoRecoveryStatus(error);
            return;
        }

        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            return;

        try
        {
            Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => UpdateAutoRecoveryStatus(error)));
        }
        catch (InvalidOperationException)
        {
            // The dispatcher can finish shutting down between the check and the queued callback.
        }
    }

    private void UpdateAutoRecoveryStatus(Exception? error)
    {
        if (Workspaces.Count == 0)
            return;

        if (error is null)
        {
            if (!_autoRecoveryWarningShown)
                return;

            _autoRecoveryWarningShown = false;
            ActiveVm.StatusText = "Auto-recovery is available again.";
            return;
        }

        if (_autoRecoveryWarningShown)
            return;

        _autoRecoveryWarningShown = true;
        ActiveVm.StatusText = "Auto-recovery is unavailable. Keep this window open or save your work manually.";
    }

    private bool TryResumeAutoRecoveryAfterCancelledClose()
    {
        lock (_recoveryLifecycleGate)
        {
            if (_recoveryLifecycleState != RecoveryLifecycleState.Running)
                return false;

            _autoRecoveryWriter.Resume();
            return true;
        }
    }

    private bool IsCrashRecoveryPreservationRequested()
    {
        lock (_recoveryLifecycleGate)
            return _recoveryLifecycleState == RecoveryLifecycleState.PreserveForCrash;
    }

    private enum RecoveryLifecycleState
    {
        Running,
        CleanClose,
        PreserveForCrash
    }

    private sealed record CapturedRecoverySnapshot(
        int ActiveWorkspaceIndex,
        IReadOnlyList<CapturedRecoveryWorkspace> Workspaces);

    private sealed record CapturedRecoveryWorkspace(
        string Name,
        MainViewModel.LayoutFile LayoutSnapshot,
        string? CurrentFilePath,
        Models.SaveFileKind CurrentFileKind,
        bool IsDirty,
        bool RequiresSaveAsBeforeOverwrite,
        int LastMultiColumnCount,
        bool HasSessionChanges);
}
