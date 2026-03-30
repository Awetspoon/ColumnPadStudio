using System.Windows;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.App.Views;

public partial class WorkflowBuilderWindow
{
    private void AddNodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        var nextIndex = _currentWorkflow.Nodes.Count + 1;
        var node = new WorkflowNode
        {
            Kind = WorkflowNodeKind.Step,
            Title = $"Step {nextIndex}",
            X = 48 + (_currentWorkflow.Nodes.Count * 210),
            Y = 60 + ((_currentWorkflow.Nodes.Count % 2) * 140),
            Width = WorkflowRules.DefaultNodeWidth,
            Height = 72
        };

        _currentWorkflow.Nodes.Add(node);
        _nodes.Add(node);
        NodesListBox.SelectedItem = node;
        UpdateWorkflowSummaryUi();
        RenderWorkflow();
        SetStatus($"Added node: {node.Title}");
    }

    private void DuplicateNodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedNode is null)
        {
            SetStatus("Select a node to duplicate.");
            return;
        }

        var copy = new WorkflowNode
        {
            Kind = SelectedNode.Kind,
            Color = SelectedNode.Color,
            Title = SelectedNode.Title + " Copy",
            Description = SelectedNode.Description,
            X = SelectedNode.X + 36,
            Y = SelectedNode.Y + 36,
            Width = SelectedNode.Width,
            Height = SelectedNode.Height
        };

        _currentWorkflow.Nodes.Add(copy);
        _nodes.Add(copy);
        NodesListBox.SelectedItem = copy;
        UpdateWorkflowSummaryUi();
        RenderWorkflow();
        SetStatus($"Duplicated node: {copy.Title}");
    }

    private void RemoveNodeButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedNode is null)
        {
            SetStatus("Select a node to remove.");
            return;
        }

        var removedTitle = SelectedNode.Title;
        _currentWorkflow.Nodes.Remove(SelectedNode);
        _currentWorkflow.Links.RemoveAll(link => link.FromNodeId == SelectedNode.Id || link.ToNodeId == SelectedNode.Id);
        _nodes.Remove(SelectedNode);
        RefreshLinksList();
        NodesListBox.SelectedItem = _nodes.FirstOrDefault();
        RenderWorkflow();
        SetStatus($"Removed node: {removedTitle}");
    }

    private void AddLinkButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedNode is null || LinkTargetComboBox.SelectedItem is not WorkflowNode target || target.Id == SelectedNode.Id)
        {
            SetStatus("Select a node and a different connection target.");
            return;
        }

        if (_currentWorkflow.Links.Any(link => link.FromNodeId == SelectedNode.Id && link.ToNodeId == target.Id))
        {
            SetStatus("That line already exists.");
            return;
        }

        var link = new WorkflowLink
        {
            FromNodeId = SelectedNode.Id,
            ToNodeId = target.Id,
            Label = string.Empty
        };
        _currentWorkflow.Links.Add(link);
        RefreshLinksList();
        LinksListBox.SelectedIndex = _links.Count - 1;
        RenderWorkflow();
        SetStatus($"Added line from {SelectedNode.Title} to {target.Title}.");
    }

    private void RemoveLinkButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedLink is null)
        {
            SetStatus("Select a line to remove.");
            return;
        }

        _currentWorkflow.Links.Remove(SelectedLink.Link);
        RefreshLinksList();
        RenderWorkflow();
        SetStatus("Removed line.");
    }

    private void AutoLayoutButton_OnClick(object sender, RoutedEventArgs e)
    {
        ApplyCurrentChartStyleLayout($"Arranged flow as {GetChartStyleDisplayName(_currentWorkflow.ChartStyle)}.");
    }

    private void NudgeUpButton_OnClick(object sender, RoutedEventArgs e) => NudgeSelectedNode(0, -24, "Nudged node up.");
    private void NudgeDownButton_OnClick(object sender, RoutedEventArgs e) => NudgeSelectedNode(0, 24, "Nudged node down.");
    private void NudgeLeftButton_OnClick(object sender, RoutedEventArgs e) => NudgeSelectedNode(-24, 0, "Nudged node left.");
    private void NudgeRightButton_OnClick(object sender, RoutedEventArgs e) => NudgeSelectedNode(24, 0, "Nudged node right.");

    private void NudgeSelectedNode(double dx, double dy, string status)
    {
        if (SelectedNode is null)
        {
            SetStatus("Select a node to move.");
            return;
        }

        SelectedNode.X = Math.Max(12, SelectedNode.X + dx);
        SelectedNode.Y = Math.Max(12, SelectedNode.Y + dy);
        RenderWorkflow();
        SetStatus(status);
    }

    private void ApplyCurrentChartStyleLayout(string status)
    {
        WorkflowChartLayoutLogic.ApplyAutoLayout(_currentWorkflow.Nodes, _currentWorkflow.ChartStyle);
        NodesListBox.Items.Refresh();
        RefreshLinksList();
        RenderWorkflow();
        SetStatus(status);
    }
}
