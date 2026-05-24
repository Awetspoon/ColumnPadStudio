using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using Microsoft.Win32;
using System.IO;
using System.Text;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private bool ShouldSaveWorkspaceSession()
    {
        return WorkspaceSessionFileService.ShouldSaveWorkspaceSession(BuildWorkspaceSessionSaveCandidates());
    }

    private string? GetDirectWorkspaceSessionPath()
    {
        return WorkspaceSessionFileService.GetDirectWorkspaceSessionPath(BuildWorkspaceSessionSaveCandidates());
    }

    private IReadOnlyList<WorkspaceSessionSaveCandidate> BuildWorkspaceSessionSaveCandidates()
    {
        return Workspaces
            .Select(workspace => new WorkspaceSessionSaveCandidate(
                workspace.Vm.CurrentFilePath,
                workspace.Vm.CurrentFileKind,
                workspace.Vm.RequiresSaveAsBeforeOverwrite))
            .ToList();
    }

    private SaveFileDialog CreateWorkspaceSessionSaveDialog()
    {
        var preferredPath = GetDirectWorkspaceSessionPath() ?? Workspaces
            .Select(workspace => workspace.Vm.CurrentFilePath)
            .FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));

        var definition = FileWorkflowService.BuildWorkspaceSessionSaveDialog(preferredPath);
        return CreateSaveFileDialog(definition);
    }

    private void SaveWorkspaceSessionToPath(string path)
    {
        var workspaces = Workspaces
            .Select(workspace => new WorkspaceSessionEntryData(
                workspace.Name,
                workspace.Vm.ToLayoutJson(),
                workspace.LastMultiColumnCount))
            .ToList();

        var activeIndex = ActiveWorkspace is null ? 0 : Math.Max(0, Workspaces.IndexOf(ActiveWorkspace));
        var json = WorkspaceSessionFileService.SerializeSession(workspaces, activeIndex);
        File.WriteAllText(path, json, Encoding.UTF8);

        foreach (var workspace in Workspaces)
            workspace.Vm.SetExternalFileReference(path, SaveFileKind.Layout, requiresSaveAs: false, markClean: true);

        ActiveVm.StatusText = $"Saved: {Path.GetFileName(path)}";
    }

    private bool TryLoadWorkspaceSession(string json, string? sourceLabel = null, string? sourcePath = null)
    {
        if (!WorkspaceSessionFileService.TryParseSession(json, out var session))
            return false;

        var loaded = new List<(WorkspaceSessionEntryData Entry, MainViewModel Vm)>(session.Workspaces.Count);
        foreach (var entry in session.Workspaces)
        {
            var vm = new MainViewModel();
            ApplyAppThemePreference(vm);
            if (!vm.LoadFromJson(entry.LayoutJson, entry.Name, sourcePath, preserveCurrentTheme: true))
                return false;

            vm.SetExternalFileReference(sourcePath, SaveFileKind.Layout, requiresSaveAs: false, markClean: true);
            loaded.Add((entry, vm));
        }

        if (loaded.Count == 0)
            return false;

        Workspaces.Clear();
        foreach (var (entry, vm) in loaded)
        {
            var name = string.IsNullOrWhiteSpace(entry.Name) ? NextWorkspaceName() : entry.Name.Trim();
            var workspaceSession = CreateWorkspace(name, vm);
            workspaceSession.LastMultiColumnCount = Math.Max(2, entry.LastMultiColumnCount);
        }

        var activeIndex = Math.Clamp(session.ActiveWorkspaceIndex, 0, Workspaces.Count - 1);
        ActiveWorkspace = Workspaces[activeIndex];
        WorkspaceTabs.SelectedItem = ActiveWorkspace;
        ActiveVm.StatusText = sourceLabel is null ? "Workspace session loaded." : $"Opened: {sourceLabel}";
        return true;
    }
}
