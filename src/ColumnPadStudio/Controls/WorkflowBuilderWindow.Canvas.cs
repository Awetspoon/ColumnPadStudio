using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Controls;

public partial class WorkflowBuilderWindow
{
    private WorkflowDiagramNode? _draggedNode;
    private Point _dragStartPoint;
    private double _dragStartX;
    private double _dragStartY;

    private void AddNodeOfKind_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string kindName } ||
            !Enum.TryParse<WorkflowNodeKind>(kindName, ignoreCase: true, out var kind))
        {
            return;
        }

        ViewModel.AddNode(kind);
    }

    private void DuplicateNode_Click(object sender, RoutedEventArgs e)
        => ViewModel.DuplicateSelectedNode();

    private void RemoveNode_Click(object sender, RoutedEventArgs e)
        => ViewModel.RemoveSelectedNode();

    private void AutoLayout_Click(object sender, RoutedEventArgs e)
        => ViewModel.AutoLayoutSelectedWorkflow();

    private void AddLink_Click(object sender, RoutedEventArgs e)
        => ViewModel.AddLink();

    private void RemoveLink_Click(object sender, RoutedEventArgs e)
        => ViewModel.RemoveSelectedLink();

    private void NudgeNodeLeft_Click(object sender, RoutedEventArgs e)
        => ViewModel.NudgeSelectedNode(-16, 0);

    private void NudgeNodeRight_Click(object sender, RoutedEventArgs e)
        => ViewModel.NudgeSelectedNode(16, 0);

    private void NudgeNodeUp_Click(object sender, RoutedEventArgs e)
        => ViewModel.NudgeSelectedNode(0, -16);

    private void NudgeNodeDown_Click(object sender, RoutedEventArgs e)
        => ViewModel.NudgeSelectedNode(0, 16);

    private void UseStarter_Click(object sender, RoutedEventArgs e)
        => ViewModel.CreateWorkflowFromSelectedTemplate();

    private void StarterList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.HasSelectedTemplate)
            ViewModel.CreateWorkflowFromSelectedTemplate();
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
        if (sender is not MenuItem { Tag: string colorName } menuItem ||
            !Enum.TryParse<WorkflowNodeColor>(colorName, ignoreCase: true, out var color) ||
            (menuItem.Parent as ContextMenu)?.PlacementTarget is not FrameworkElement { DataContext: WorkflowDiagramNode node })
        {
            return;
        }

        node.Color = color;
        ViewModel.SelectedNode = node;
        e.Handled = true;
    }
}
