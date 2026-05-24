using ColumnPadStudio.ViewModels;
using System.IO;
using System.Windows;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private bool TryConfirmSaveBeforeExit()
    {
        PersistWidthsFromGrid();

        var dirtyWorkspaces = Workspaces.Where(workspace => workspace.Vm.IsDirty).ToList();
        if (dirtyWorkspaces.Count == 0)
            return true;

        var message = BuildSaveBeforeExitMessage(dirtyWorkspaces);
        var result = MessageBox.Show(
            this,
            message,
            "Save Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.No)
            return true;

        if (ShouldSaveWorkspaceSession())
        {
            var sessionPath = GetDirectWorkspaceSessionPath();
            if (string.IsNullOrWhiteSpace(sessionPath))
            {
                var sessionDialog = CreateWorkspaceSessionSaveDialog();
                if (sessionDialog.ShowDialog() != true)
                    return false;

                return TryRunFileAction("Save Failed", $"save {Path.GetFileName(sessionDialog.FileName)}", () => SaveWorkspaceSessionToPath(sessionDialog.FileName));
            }

            return TryRunFileAction("Save Failed", $"save {Path.GetFileName(sessionPath)}", () => SaveWorkspaceSessionToPath(sessionPath));
        }

        foreach (var workspace in dirtyWorkspaces)
        {
            if (!TrySaveWorkspaceBeforeExit(workspace))
                return false;
        }

        return true;
    }

    private static string BuildSaveBeforeExitMessage(IReadOnlyList<WorkspaceSession> dirtyWorkspaces)
    {
        if (dirtyWorkspaces.Count == 1)
            return $"Save changes to {dirtyWorkspaces[0].Name} before closing?";

        var names = dirtyWorkspaces.Take(3).Select(workspace => $"- {workspace.Name}");
        var remainder = dirtyWorkspaces.Count > 3
            ? $"\n- and {dirtyWorkspaces.Count - 3} more"
            : string.Empty;

        return $"Save changes to {dirtyWorkspaces.Count} workspaces before closing?\n\n{string.Join("\n", names)}{remainder}";
    }

    private bool TrySaveWorkspaceBeforeExit(WorkspaceSession workspace)
    {
        try
        {
            if (workspace.Vm.SaveCurrentFile())
                return true;

            var dlg = CreateSaveDialog(workspace.Vm);
            if (dlg.ShowDialog() != true)
                return false;

            workspace.Vm.SaveToPath(dlg.FileName, workspace.Vm.CurrentFileKind);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Could not save {workspace.Name}.\n\n{ex.Message}",
                "Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }
}
