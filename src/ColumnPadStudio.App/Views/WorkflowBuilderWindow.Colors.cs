using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ColumnPadStudio.App.Styling;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.App.Views;

public partial class WorkflowBuilderWindow
{
    private void NodesListBox_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject) is not { DataContext: WorkflowNode node })
        {
            return;
        }

        NodesListBox.SelectedItem = node;
        LinksListBox.SelectedIndex = -1;
        OpenNodeColorMenu(NodesListBox, node);
        e.Handled = true;
    }

    private void OpenNodeColorMenu(FrameworkElement placementTarget, WorkflowNode node)
    {
        var menu = BuildNodeColorMenu(node);
        menu.PlacementTarget = placementTarget;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private ContextMenu BuildNodeColorMenu(WorkflowNode node)
    {
        var menu = new ContextMenu();
        menu.Items.Add(new MenuItem
        {
            Header = $"Node Color: {node.Title}",
            IsEnabled = false
        });
        menu.Items.Add(new Separator());

        foreach (var color in WorkflowNodePalette.AvailableColors)
        {
            var menuItem = new MenuItem
            {
                Header = WorkflowNodePalette.GetDisplayName(color),
                IsCheckable = true,
                IsChecked = node.Color == color,
                Tag = color
            };
            menuItem.Click += NodeColorMenuItem_OnClick;
            menu.Items.Add(menuItem);
        }

        return menu;
    }

    private void NodeColorMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { Tag: WorkflowNodeColor color })
        {
            return;
        }

        var node = SelectedNode;
        if (node is null)
        {
            return;
        }

        node.Color = color;
        NodesListBox.Items.Refresh();
        RefreshLinksList();
        UpdateSelectedNodeAccent();
        RenderWorkflow();
        SetStatus($"Node color set to {WorkflowNodePalette.GetDisplayName(color)} for {node.Title}.");
    }

    private void UpdateSelectedNodeAccent()
    {
        if (SelectedNodePanelBorder is null)
        {
            return;
        }

        if (SelectedNode is null)
        {
            SelectedNodeContentGrid.IsEnabled = false;
            SelectedNodePanelBorder.BorderBrush = GetThemeBrush("BorderBrushStrong");
            SelectedNodePanelBorder.BorderThickness = new Thickness(1);
            SelectedNodeColorBadgeBorder.Background = GetThemeBrush("SurfaceAltBrush");
            SelectedNodeColorBadgeBorder.BorderBrush = GetThemeBrush("BorderBrushStrong");
            SelectedNodeColorTextBlock.Text = "No node";
            SelectedNodeHintTextBlock.Text = "Choose a node from the list or preview to edit it.";
            return;
        }

        var palette = WorkflowNodePalette.GetBrushKeys(SelectedNode);
        SelectedNodeContentGrid.IsEnabled = true;
        SelectedNodePanelBorder.BorderBrush = GetThemeBrush(palette.StrokeKey);
        SelectedNodePanelBorder.BorderThickness = new Thickness(2);
        SelectedNodeColorBadgeBorder.Background = GetThemeBrush(palette.FillKey);
        SelectedNodeColorBadgeBorder.BorderBrush = GetThemeBrush(palette.StrokeKey);
        SelectedNodeColorTextBlock.Text = WorkflowNodePalette.GetDisplayName(SelectedNode.Color);
        SelectedNodeHintTextBlock.Text = "Right-click the preview or node list to change color. Double-click a box or press F2 to rename.";
    }

    private static T? FindAncestor<T>(DependencyObject? dependencyObject) where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T match)
            {
                return match;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }
}
