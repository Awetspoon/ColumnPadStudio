using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ColumnPadStudio.App.Styling;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.App.Views;

public partial class WorkflowBuilderWindow
{
    private void RenderWorkflow()
    {
        WorkflowCanvas.Children.Clear();
        var canvasMetrics = WorkflowChartLayoutLogic.GetCanvasMetrics(_currentWorkflow.Nodes);
        WorkflowCanvas.Width = canvasMetrics.Width;
        WorkflowCanvas.Height = canvasMetrics.Height;
        RenderChartGuides();

        foreach (var link in _currentWorkflow.Links)
        {
            var from = _currentWorkflow.Nodes.FirstOrDefault(node => node.Id == link.FromNodeId);
            var to = _currentWorkflow.Nodes.FirstOrDefault(node => node.Id == link.ToNodeId);
            if (from is null || to is null)
            {
                continue;
            }

            var connector = BuildLinkShape(from, to, _currentWorkflow.ChartStyle, SelectedLink?.Link.Id == link.Id);
            connector.Tag = link;
            connector.Cursor = Cursors.Hand;
            connector.MouseLeftButtonDown += LinkShape_OnMouseLeftButtonDown;
            Panel.SetZIndex(connector, 1);
            WorkflowCanvas.Children.Add(connector);
        }

        foreach (var node in _currentWorkflow.Nodes)
        {
            var palette = WorkflowNodePalette.GetBrushKeys(node);
            var border = new Border
            {
                Width = node.Width,
                Height = node.Height,
                CornerRadius = new CornerRadius(10),
                Background = GetThemeBrush(palette.FillKey),
                BorderBrush = SelectedNode?.Id == node.Id
                    ? GetThemeBrush("AccentBrush")
                    : GetThemeBrush(palette.StrokeKey),
                BorderThickness = new Thickness(SelectedNode?.Id == node.Id ? 2 : 1),
                Tag = node,
                Cursor = Cursors.Hand,
                Child = new StackPanel
                {
                    Margin = new Thickness(12, 10, 12, 10),
                    Children =
                    {
                        new TextBlock { Text = node.Title, FontWeight = FontWeights.SemiBold, Foreground = GetThemeBrush("WorkflowNodeForegroundBrush") },
                        new TextBlock { Text = string.IsNullOrWhiteSpace(node.Description) ? node.Kind.ToString() : node.Description, FontSize = 11, Foreground = GetThemeBrush("WorkflowNodeMutedForegroundBrush"), TextWrapping = TextWrapping.Wrap }
                    }
                }
            };
            border.MouseLeftButtonDown += NodeBorder_OnMouseLeftButtonDown;
            border.MouseRightButtonDown += NodeBorder_OnMouseRightButtonDown;
            Canvas.SetLeft(border, node.X);
            Canvas.SetTop(border, node.Y);
            Panel.SetZIndex(border, 2);
            WorkflowCanvas.Children.Add(border);
        }
    }

    private void RenderChartGuides()
    {
        foreach (var lane in WorkflowChartLayoutLogic.BuildGuides(_currentWorkflow.Nodes, _currentWorkflow.ChartStyle))
        {
            var laneBorder = new Border
            {
                Width = lane.Width,
                Height = lane.Height,
                Background = GetThemeBrush("SurfaceAltBrush").CloneCurrentValue(),
                BorderBrush = GetThemeBrush("BorderBrushStrong"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Child = new TextBlock
                {
                    Text = lane.Label,
                    Margin = new Thickness(12, 8, 12, 8),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetThemeBrush("ForegroundMutedBrush")
                }
            };

            laneBorder.Opacity = 0.32;
            Canvas.SetLeft(laneBorder, lane.X);
            Canvas.SetTop(laneBorder, lane.Y);
            Panel.SetZIndex(laneBorder, 0);
            WorkflowCanvas.Children.Add(laneBorder);
        }
    }

    private Shape BuildLinkShape(WorkflowNode from, WorkflowNode to, WorkflowChartStyle chartStyle, bool isSelected)
    {
        var stroke = isSelected ? GetThemeBrush("AccentBrush") : GetThemeBrush("WorkflowLinkBrush");
        var thickness = isSelected ? 4 : 3;

        return chartStyle switch
        {
            WorkflowChartStyle.VerticalTimeline => BuildVerticalConnector(from, to, stroke, thickness),
            WorkflowChartStyle.RadialMap => BuildStraightConnector(from, to, stroke, thickness),
            _ => BuildHorizontalConnector(from, to, stroke, thickness)
        };
    }

    private static Polyline BuildHorizontalConnector(WorkflowNode from, WorkflowNode to, Brush stroke, double thickness)
    {
        var start = new Point(from.X + from.Width, from.Y + (from.Height / 2));
        var end = new Point(to.X, to.Y + (to.Height / 2));
        var midX = start.X + ((end.X - start.X) / 2);
        return new Polyline
        {
            Points = new PointCollection
            {
                start,
                new(midX, start.Y),
                new(midX, end.Y),
                end
            },
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            SnapsToDevicePixels = true
        };
    }

    private static Polyline BuildVerticalConnector(WorkflowNode from, WorkflowNode to, Brush stroke, double thickness)
    {
        var start = new Point(from.X + (from.Width / 2), from.Y + from.Height);
        var end = new Point(to.X + (to.Width / 2), to.Y);
        var midY = start.Y + ((end.Y - start.Y) / 2);
        return new Polyline
        {
            Points = new PointCollection
            {
                start,
                new(start.X, midY),
                new(end.X, midY),
                end
            },
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            SnapsToDevicePixels = true
        };
    }

    private static Line BuildStraightConnector(WorkflowNode from, WorkflowNode to, Brush stroke, double thickness)
    {
        return new Line
        {
            X1 = from.X + (from.Width / 2),
            Y1 = from.Y + (from.Height / 2),
            X2 = to.X + (to.Width / 2),
            Y2 = to.Y + (to.Height / 2),
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            SnapsToDevicePixels = true
        };
    }

    private void NodeBorder_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: WorkflowNode node })
        {
            NodesListBox.SelectedItem = node;
            LinksListBox.SelectedIndex = -1;

            if (e.ClickCount >= 2)
            {
                BeginNodeRename(node);
                e.Handled = true;
                return;
            }

            _draggingNode = node;
            _dragStartPoint = e.GetPosition(WorkflowCanvas);
            _dragStartOrigin = new Point(node.X, node.Y);
            _dragMoved = false;
            WorkflowCanvas.CaptureMouse();
            RenderWorkflow();
            SetStatus($"Selected node: {node.Title}. Drag in preview to move it.");
            e.Handled = true;
        }
    }

    private void NodeBorder_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border { Tag: WorkflowNode node })
        {
            return;
        }

        NodesListBox.SelectedItem = node;
        LinksListBox.SelectedIndex = -1;
        OpenNodeColorMenu(WorkflowCanvas, node);
        e.Handled = true;
    }

    private void NodesListBox_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (SelectedNode is null)
        {
            return;
        }

        BeginNodeRename(SelectedNode);
        e.Handled = true;
    }

    private void NodesListBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.F2 || SelectedNode is null)
        {
            return;
        }

        BeginNodeRename(SelectedNode);
        e.Handled = true;
    }

    private void LinkShape_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Shape { Tag: WorkflowLink link })
        {
            return;
        }

        var selected = _links.FirstOrDefault(item => item.Link.Id == link.Id);
        if (selected is not null)
        {
            LinksListBox.SelectedItem = selected;
            var fromTitle = _currentWorkflow.Nodes.FirstOrDefault(node => node.Id == link.FromNodeId)?.Title ?? "Unknown";
            var toTitle = _currentWorkflow.Nodes.FirstOrDefault(node => node.Id == link.ToNodeId)?.Title ?? "Unknown";
            SetStatus($"Selected line: {fromTitle} -> {toTitle}.");
            e.Handled = true;
        }
    }

    private void WorkflowCanvas_OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingNode is null || !WorkflowCanvas.IsMouseCaptured)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            FinishNodeDrag();
            return;
        }

        var currentPoint = e.GetPosition(WorkflowCanvas);
        var dx = currentPoint.X - _dragStartPoint.X;
        var dy = currentPoint.Y - _dragStartPoint.Y;
        if (!_dragMoved && Math.Abs(dx) < 2 && Math.Abs(dy) < 2)
        {
            return;
        }

        _dragMoved = true;
        _draggingNode.X = Math.Max(12, _dragStartOrigin.X + dx);
        _draggingNode.Y = Math.Max(12, _dragStartOrigin.Y + dy);
        RenderWorkflow();
    }

    private void WorkflowCanvas_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        FinishNodeDrag();
    }

    private void FinishNodeDrag()
    {
        if (_draggingNode is null)
        {
            return;
        }

        var draggedNode = _draggingNode;
        var moved = _dragMoved;
        _draggingNode = null;
        _dragMoved = false;

        if (WorkflowCanvas.IsMouseCaptured)
        {
            WorkflowCanvas.ReleaseMouseCapture();
        }

        RenderWorkflow();
        SetStatus(moved
            ? $"Moved node: {draggedNode.Title}."
            : $"Selected node: {draggedNode.Title}. Drag in preview to move it.");
    }

    private void BeginNodeRename(WorkflowNode node)
    {
        NodesListBox.SelectedItem = node;
        _draggingNode = null;
        _dragMoved = false;
        if (WorkflowCanvas.IsMouseCaptured)
        {
            WorkflowCanvas.ReleaseMouseCapture();
        }

        UpdateSelectedNodeUi();
        NodeTitleTextBox.Focus();
        NodeTitleTextBox.SelectAll();
        SetStatus($"Rename node: {node.Title}");
    }
}
