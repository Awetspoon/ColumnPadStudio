using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace ColumnPadStudio.Controls;

public partial class WorkflowBuilderWindow
{
    private void SaveWorkflow_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ViewModel.SaveSelectedWorkflow();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Could not save workflow.\n\n{ex.Message}",
                "Workflow Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void DeleteWorkflow_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedWorkflow;
        if (selected is null)
            return;

        var result = MessageBox.Show(
            this,
            $"Delete workflow \"{selected.Name}\"?",
            "Delete Workflow",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            ViewModel.DeleteSelectedWorkflow();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Could not delete workflow.\n\n{ex.Message}",
                "Workflow Delete Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ImportWorkflowJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Reloadable workflow JSON (*.workflow.json;*.json)|*.workflow.json;*.json|All files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog(this) == true)
            ImportWorkflowJsonFromPath(dialog.FileName);
    }

    public void ImportWorkflowJsonFromPath(string filePath)
    {
        if (!IsLoaded)
        {
            _pendingImportFilePath = filePath;
            return;
        }

        try
        {
            if (ViewModel.ImportWorkflowFromFile(filePath))
                return;

            MessageBox.Show(
                this,
                "The selected JSON file could not be imported as a workflow.",
                "Workflow Import Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Could not import workflow JSON.\n\n{ex.Message}",
                "Workflow Import Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ExportWorkflowMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
            return;

        menu.PlacementTarget = button;
        menu.IsOpen = true;
    }

    private void ExportWorkflowJson_Click(object sender, RoutedEventArgs e)
    {
        ExportWorkflow(
            ".workflow.json",
            "Reloadable workflow JSON (*.workflow.json)|*.workflow.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            "reloadable workflow JSON",
            ViewModel.ExportSelectedWorkflowToFile);
    }

    private void ExportWorkflowText_Click(object sender, RoutedEventArgs e)
    {
        ExportWorkflow(
            ".workflow.txt",
            "Readable workflow text (*.workflow.txt)|*.workflow.txt|Text (*.txt)|*.txt|All files (*.*)|*.*",
            "readable text copy",
            ViewModel.ExportSelectedWorkflowTextToFile);
    }

    private void ExportWorkflow(
        string defaultExtension,
        string filter,
        string exportLabel,
        Func<string, bool> exportAction)
    {
        var selected = ViewModel.SelectedWorkflow;
        if (selected is null)
            return;

        var dialog = new SaveFileDialog
        {
            FileName = BuildWorkflowExportFileName(selected.Name, defaultExtension),
            Filter = filter,
            FilterIndex = 1,
            DefaultExt = defaultExtension,
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            exportAction(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Could not export {exportLabel}.\n\n{ex.Message}",
                "Workflow Export Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string BuildWorkflowExportFileName(string? workflowName, string extension)
    {
        var baseName = string.IsNullOrWhiteSpace(workflowName)
            ? "workflow"
            : workflowName.Trim();

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(baseName.Select(character => invalidChars.Contains(character) ? '-' : character).ToArray());
        sanitized = string.IsNullOrWhiteSpace(sanitized) ? "workflow" : sanitized;

        if (sanitized.EndsWith(".workflow", StringComparison.OrdinalIgnoreCase))
            return $"{sanitized}{extension[".workflow".Length..]}";

        return $"{sanitized}{extension}";
    }
}
