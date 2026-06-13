using ColumnPadStudio.ViewModels;
using System.IO;
using System.Windows;

namespace ColumnPadStudio;

public partial class MainWindow
{
    private bool ConfirmWorkspaceDestructiveAction(WorkspaceSession? workspace, string dialogTitle, string actionText)
    {
        if (workspace is null)
            return true;

        var editedColumns = workspace.Vm.Columns.Where(HasEditedColumnData).ToList();
        if (editedColumns.Count == 0)
        {
            if (!workspace.Vm.IsDirty)
                return true;

            var genericMessage = $"{actionText} will permanently discard unsaved changes in {workspace.Name}.";
            return ConfirmDestructiveAction(dialogTitle, genericMessage + "\n\nAre you sure you want to continue?");
        }

        var message = editedColumns.Count == 1
            ? $"{actionText} will permanently discard the edited contents of \"{editedColumns[0].Title}\"."
            : $"{actionText} will permanently discard edited contents from {editedColumns.Count} columns.";

        return ConfirmDestructiveAction(dialogTitle, message + "\n\nAre you sure you want to continue?");
    }

    private bool ConfirmDestructiveAction(string dialogTitle, string message)
    {
        return MessageBox.Show(
            this,
            message,
            dialogTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No) == MessageBoxResult.Yes;
    }

    private bool TryRunFileAction(string dialogTitle, string actionText, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidDataException)
        {
            MessageBox.Show(
                this,
                $"Could not {actionText}.\n\n{ex.Message}",
                dialogTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return false;
        }
    }
}
