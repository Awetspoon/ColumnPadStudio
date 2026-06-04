using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColumnPadStudio.Services;
using ColumnPadStudio.ViewModels;
using ColumnPadStudio.Workflows;
using Microsoft.Win32;

namespace ColumnPadStudio.Controls;

public partial class WorkflowBuilderWindow : Window
{
    private WorkflowDiagramNode? _draggedNode;
    private Point _dragStartPoint;
    private double _dragStartX;
    private double _dragStartY;

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

    private void AddWorkflow_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddWorkflow();
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

    private void AddNodeOfKind_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string kindName })
            return;

        if (!Enum.TryParse<WorkflowNodeKind>(kindName, ignoreCase: true, out var kind))
            return;

        ViewModel.AddNode(kind);
    }

    private void DuplicateNode_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DuplicateSelectedNode();
    }

    private void RemoveNode_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveSelectedNode();
    }

    private void AutoLayout_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AutoLayoutSelectedWorkflow();
    }

    private void AddLink_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddLink();
    }

    private void RemoveLink_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.RemoveSelectedLink();
    }

    private void NudgeNodeLeft_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NudgeSelectedNode(-16, 0);
    }

    private void NudgeNodeRight_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NudgeSelectedNode(16, 0);
    }

    private void NudgeNodeUp_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NudgeSelectedNode(0, -16);
    }

    private void NudgeNodeDown_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.NudgeSelectedNode(0, 16);
    }

    private void UseStarter_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CreateWorkflowFromSelectedTemplate();
    }

    private void StarterList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.HasSelectedTemplate)
            ViewModel.CreateWorkflowFromSelectedTemplate();
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

    private void WorkflowNode_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: WorkflowDiagramNode node } element)
            return;

        ViewModel.SelectedNode = node;
        _draggedNode = node;
        _dragStartPoint = e.GetPosition(WorkflowDiagramSurface);
        _dragStartX = node.X;
        _dragStartY = node.Y;
        element.CaptureMouse();
        e.Handled = true;
    }

    private void WorkflowNode_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedNode is null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var currentPoint = e.GetPosition(WorkflowDiagramSurface);
        _draggedNode.X = _dragStartX + currentPoint.X - _dragStartPoint.X;
        _draggedNode.Y = _dragStartY + currentPoint.Y - _dragStartPoint.Y;
        ViewModel.RefreshLinkPreviews();
        e.Handled = true;
    }

    private void WorkflowNode_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is UIElement element && element.IsMouseCaptured)
            element.ReleaseMouseCapture();

        _draggedNode = null;
        e.Handled = true;
    }

    private void WorkflowNodeColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: string colorName } menuItem)
            return;

        if (!Enum.TryParse<WorkflowNodeColor>(colorName, ignoreCase: true, out var color))
            return;

        if ((menuItem.Parent as ContextMenu)?.PlacementTarget is not FrameworkElement { DataContext: WorkflowDiagramNode node })
            return;

        node.Color = color;
        ViewModel.SelectedNode = node;
        e.Handled = true;
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
