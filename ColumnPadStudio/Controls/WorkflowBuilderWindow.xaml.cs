using System.IO;
using System.Windows;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;

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
}
