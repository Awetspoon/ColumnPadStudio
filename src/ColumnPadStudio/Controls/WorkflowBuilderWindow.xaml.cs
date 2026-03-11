using System.IO;
using System.Windows;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using Microsoft.Win32;

namespace ColumnPadStudio.Controls;

public partial class WorkflowBuilderWindow : Window
{
    public WorkflowBuilderViewModel ViewModel { get; }

    public WorkflowBuilderWindow()
    {
        InitializeComponent();

        ViewModel = new WorkflowBuilderViewModel(new WorkflowService());
        DataContext = ViewModel;

        Loaded += WorkflowBuilderWindow_Loaded;
    }

    private void WorkflowBuilderWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= WorkflowBuilderWindow_Loaded;
        ViewModel.Load();
    }

    private void NewWorkflow_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NewWorkflow();
    }

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

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddStep();
    }

    private void RemoveStep_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveSelectedStep();
    }

    private void MoveStepUp_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveSelectedStepUp();
    }

    private void MoveStepDown_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.MoveSelectedStepDown();
    }

    private void UseTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.CreateWorkflowFromSelectedTemplate())
            return;
    }

    private void ImportWorkflowJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Workflow JSON (*.workflow.json;*.json)|*.workflow.json;*.json|All files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            if (ViewModel.ImportWorkflowFromFile(dialog.FileName))
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

    private void ExportWorkflowJson_Click(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedWorkflow;
        if (selected is null)
            return;

        var suggestedName = BuildWorkflowExportFileName(selected.Name);
        var dialog = new SaveFileDialog
        {
            FileName = suggestedName,
            Filter = "Workflow JSON (*.workflow.json)|*.workflow.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            FilterIndex = 1,
            DefaultExt = ".workflow.json",
            AddExtension = true
        };

        if (dialog.ShowDialog(this) != true)
            return;

        try
        {
            ViewModel.ExportSelectedWorkflowToFile(dialog.FileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                $"Could not export workflow JSON.\n\n{ex.Message}",
                "Workflow Export Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string BuildWorkflowExportFileName(string? workflowName)
    {
        var baseName = string.IsNullOrWhiteSpace(workflowName)
            ? "workflow"
            : workflowName.Trim();

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(baseName.Select(ch => invalidChars.Contains(ch) ? '-' : ch).ToArray());
        sanitized = string.IsNullOrWhiteSpace(sanitized) ? "workflow" : sanitized;

        if (sanitized.EndsWith(".workflow", StringComparison.OrdinalIgnoreCase))
            return $"{sanitized}.json";

        return $"{sanitized}.workflow.json";
    }
}

