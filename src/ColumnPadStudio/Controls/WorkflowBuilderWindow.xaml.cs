using System.ComponentModel;
using System.IO;
using System.Windows;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Controls;

public partial class WorkflowBuilderWindow : Window
{
    private string? _pendingImportFilePath;

    public WorkflowBuilderViewModel ViewModel { get; }

    public WorkflowBuilderWindow(string? importFilePath = null)
    {
        InitializeComponent();

        ViewModel = new WorkflowBuilderViewModel(new WorkflowService());
        DataContext = ViewModel;
        _pendingImportFilePath = importFilePath;

        Loaded += WorkflowBuilderWindow_Loaded;
        Closing += WorkflowBuilderWindow_Closing;
    }

    private void WorkflowBuilderWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.HasUnsavedChanges)
            return;

        var result = MessageBox.Show(
            this,
            "Save changed workflows before closing?",
            "Unsaved Workflows",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);

        if (result == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            ViewModel.SaveAllChangedWorkflows();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            e.Cancel = true;
            MessageBox.Show(
                this,
                $"Could not save all changed workflows. The window will stay open.\n\n{ex.Message}",
                "Workflow Save Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void WorkflowBuilderWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= WorkflowBuilderWindow_Loaded;
        ViewModel.Load();

        if (string.IsNullOrWhiteSpace(_pendingImportFilePath))
            return;

        var filePath = _pendingImportFilePath;
        _pendingImportFilePath = null;
        ImportWorkflowJsonFromPath(filePath);
    }

    private void AddWorkflow_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddWorkflow();
    }
}
