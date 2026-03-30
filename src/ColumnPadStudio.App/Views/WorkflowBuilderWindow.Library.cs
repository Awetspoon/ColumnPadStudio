using System.Windows;
using ColumnPadStudio.Domain.Models;
using Microsoft.Win32;

namespace ColumnPadStudio.App.Views;

public partial class WorkflowBuilderWindow
{
    private async void WorkflowBuilderWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadTemplates();
        await ReloadLibraryAsync();
        ApplyWorkflowToUi(_library.FirstOrDefault() is { } first ? CloneWorkflow(first) : CreateBlankWorkflow());
    }

    private async Task ReloadLibraryAsync()
    {
        _library.Clear();
        var items = await _workflowStore.LoadAllAsync();
        foreach (var item in items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase))
        {
            _library.Add(item);
        }
    }

    private void LoadTemplates()
    {
        _templates.Clear();
        foreach (var template in BuildTemplates())
        {
            _templates.Add(template);
        }

        if (_templates.Count > 0)
        {
            TemplateComboBox.SelectedIndex = 0;
        }
    }

    private async void SaveWorkflowButton_OnClick(object sender, RoutedEventArgs e)
    {
        WorkflowDetails_OnChanged(sender, e);
        NodeDetails_OnChanged(sender, e);
        var path = await _workflowStore.SaveAsync(_currentWorkflow, _currentWorkflow.FilePath);
        _currentWorkflow.FilePath = path;
        await ReloadLibraryAsync();
        SelectLibraryItemByPath(path);
        SetStatus($"Saved workflow: {System.IO.Path.GetFileName(path)}");
    }

    private async void DeleteWorkflowButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentWorkflow.FilePath))
        {
            SetStatus("Current workflow is not saved yet.");
            return;
        }

        if (MessageBox.Show($"Delete {_currentWorkflow.Name}?", "Delete Workflow", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        await _workflowStore.DeleteAsync(_currentWorkflow.FilePath);
        await ReloadLibraryAsync();
        ApplyWorkflowToUi(_library.FirstOrDefault() is { } first ? CloneWorkflow(first) : CreateBlankWorkflow());
        SetStatus("Deleted workflow.");
    }

    private void NewWorkflowButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyWorkflowToUi(CreateBlankWorkflow());
        WorkflowLibraryListBox.SelectedIndex = -1;
        SetStatus("Started new workflow.");
    }

    private async void ImportWorkflowButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Workflow JSON (*.json;*.workflow.json)|*.json;*.workflow.json|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var imported = await _workflowStore.LoadAsync(dialog.FileName);
        if (imported is null)
        {
            SetStatus("Could not load workflow JSON.");
            return;
        }

        imported.FilePath = null;
        ApplyWorkflowToUi(imported);
        SetStatus($"Imported: {System.IO.Path.GetFileName(dialog.FileName)}");
    }

    private async void ExportWorkflowButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Workflow JSON (*.workflow.json)|*.workflow.json|JSON Files (*.json)|*.json",
            DefaultExt = ".workflow.json",
            FileName = SanitizeFileName(_currentWorkflow.Name) + ".workflow.json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        await _workflowStore.SaveAsync(CloneWorkflow(_currentWorkflow), dialog.FileName);
        SetStatus($"Exported: {System.IO.Path.GetFileName(dialog.FileName)}");
    }

    private void UseTemplateButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (TemplateComboBox.SelectedItem is not WorkflowDefinition template)
        {
            return;
        }

        var workflow = CloneWorkflow(template);
        workflow.FilePath = null;
        workflow.Name += " Copy";
        ApplyWorkflowToUi(workflow);
        SetStatus($"Loaded starter template: {template.Name}");
    }
}
