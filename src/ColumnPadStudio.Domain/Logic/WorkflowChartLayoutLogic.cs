using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Domain.Logic;

public static class WorkflowChartLayoutLogic
{
    public static void ApplyAutoLayout(IList<WorkflowNode> nodes, WorkflowChartStyle chartStyle)
    {
        if (nodes.Count == 0)
        {
            return;
        }

        switch (chartStyle)
        {
            case WorkflowChartStyle.HorizontalTimeline:
                ApplyHorizontalTimeline(nodes);
                break;
            case WorkflowChartStyle.VerticalTimeline:
                ApplyVerticalTimeline(nodes);
                break;
            case WorkflowChartStyle.Swimlane:
                ApplySwimlane(nodes);
                break;
            case WorkflowChartStyle.Kanban:
                ApplyKanban(nodes);
                break;
            case WorkflowChartStyle.RadialMap:
                ApplyRadialMap(nodes);
                break;
            default:
                ApplyFlowchart(nodes);
                break;
        }
    }

    public static WorkflowCanvasMetrics GetCanvasMetrics(IReadOnlyList<WorkflowNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return new WorkflowCanvasMetrics(1100, 640);
        }

        var maxX = nodes.Max(node => node.X + node.Width);
        var maxY = nodes.Max(node => node.Y + node.Height);
        return new WorkflowCanvasMetrics(
            Math.Max(1100, maxX + 120),
            Math.Max(640, maxY + 120));
    }

    public static IReadOnlyList<WorkflowLane> BuildGuides(IReadOnlyList<WorkflowNode> nodes, WorkflowChartStyle chartStyle)
    {
        return chartStyle switch
        {
            WorkflowChartStyle.Swimlane => BuildSwimlaneGuides(nodes),
            WorkflowChartStyle.Kanban => BuildKanbanGuides(nodes),
            _ => Array.Empty<WorkflowLane>()
        };
    }

    private static void ApplyFlowchart(IList<WorkflowNode> nodes)
    {
        const double startX = 48;
        const double startY = 72;
        const double xGap = 208;
        const double yGap = 124;

        for (var index = 0; index < nodes.Count; index++)
        {
            var row = index / 3;
            var column = index % 3;
            PositionNode(nodes[index], startX + column * xGap, startY + row * yGap);
        }
    }

    private static void ApplyHorizontalTimeline(IList<WorkflowNode> nodes)
    {
        const double startX = 56;
        const double centerY = 220;
        const double xGap = 220;

        for (var index = 0; index < nodes.Count; index++)
        {
            PositionNode(nodes[index], startX + index * xGap, centerY - (nodes[index].Height / 2));
        }
    }

    private static void ApplyVerticalTimeline(IList<WorkflowNode> nodes)
    {
        const double startX = 360;
        const double startY = 56;
        const double yGap = 126;

        for (var index = 0; index < nodes.Count; index++)
        {
            PositionNode(nodes[index], startX, startY + index * yGap);
        }
    }

    private static void ApplySwimlane(IList<WorkflowNode> nodes)
    {
        const double startX = 72;
        const double startY = 56;
        const double xGap = 220;
        const double yGap = 116;

        for (var index = 0; index < nodes.Count; index++)
        {
            var lane = GetLaneIndex(nodes[index].Kind);
            PositionNode(nodes[index], startX + index * xGap, startY + lane * yGap);
        }
    }

    private static void ApplyKanban(IList<WorkflowNode> nodes)
    {
        const double startX = 48;
        const double startY = 84;
        const double columnGap = 228;
        const double rowGap = 108;

        var groupedCounts = new Dictionary<WorkflowNodeKind, int>();
        foreach (var node in nodes)
        {
            var lane = GetLaneIndex(node.Kind);
            var row = groupedCounts.TryGetValue(node.Kind, out var current) ? current : 0;
            groupedCounts[node.Kind] = row + 1;
            PositionNode(node, startX + lane * columnGap, startY + row * rowGap);
        }
    }

    private static void ApplyRadialMap(IList<WorkflowNode> nodes)
    {
        const double centerX = 520;
        const double centerY = 280;
        const double minRadius = 210;

        if (nodes.Count == 1)
        {
            PositionNode(nodes[0], centerX - (nodes[0].Width / 2), centerY - (nodes[0].Height / 2));
            return;
        }

        PositionNode(nodes[0], centerX - (nodes[0].Width / 2), centerY - (nodes[0].Height / 2));
        var orbitCount = nodes.Count - 1;
        var radius = Math.Max(minRadius, 120 + orbitCount * 12);

        for (var index = 1; index < nodes.Count; index++)
        {
            var angle = ((index - 1) / (double)orbitCount) * Math.PI * 2;
            var node = nodes[index];
            var x = centerX + Math.Cos(angle) * radius - (node.Width / 2);
            var y = centerY + Math.Sin(angle) * (radius * 0.7) - (node.Height / 2);
            PositionNode(node, x, y);
        }
    }

    private static IReadOnlyList<WorkflowLane> BuildSwimlaneGuides(IReadOnlyList<WorkflowNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return Array.Empty<WorkflowLane>();
        }

        var lanes = new List<WorkflowLane>();
        var canvasWidth = GetCanvasMetrics(nodes).Width - 48;
        foreach (var kind in LaneOrder)
        {
            var laneIndex = GetLaneIndex(kind);
            var top = 40 + laneIndex * 116;
            lanes.Add(new WorkflowLane(GetLaneLabel(kind), 24, top, canvasWidth, 92));
        }

        return lanes;
    }

    private static IReadOnlyList<WorkflowLane> BuildKanbanGuides(IReadOnlyList<WorkflowNode> nodes)
    {
        if (nodes.Count == 0)
        {
            return Array.Empty<WorkflowLane>();
        }

        var lanes = new List<WorkflowLane>();
        var canvasHeight = GetCanvasMetrics(nodes).Height - 48;
        foreach (var kind in LaneOrder)
        {
            var laneIndex = GetLaneIndex(kind);
            var left = 24 + laneIndex * 228;
            lanes.Add(new WorkflowLane(GetLaneLabel(kind), left, 24, 196, canvasHeight));
        }

        return lanes;
    }

    private static void PositionNode(WorkflowNode node, double x, double y)
    {
        node.X = Math.Max(12, x);
        node.Y = Math.Max(12, y);
    }

    private static int GetLaneIndex(WorkflowNodeKind kind)
    {
        for (var index = 0; index < LaneOrder.Length; index++)
        {
            if (LaneOrder[index] == kind)
            {
                return index;
            }
        }

        return 0;
    }

    private static string GetLaneLabel(WorkflowNodeKind kind)
    {
        return kind switch
        {
            WorkflowNodeKind.Start => "Start",
            WorkflowNodeKind.Step => "Steps",
            WorkflowNodeKind.Decision => "Decisions",
            WorkflowNodeKind.Note => "Notes",
            WorkflowNodeKind.End => "End",
            _ => kind.ToString()
        };
    }

    private static readonly WorkflowNodeKind[] LaneOrder =
    [
        WorkflowNodeKind.Start,
        WorkflowNodeKind.Step,
        WorkflowNodeKind.Decision,
        WorkflowNodeKind.Note,
        WorkflowNodeKind.End
    ];
}

public readonly record struct WorkflowLane(string Label, double X, double Y, double Width, double Height);
public readonly record struct WorkflowCanvasMetrics(double Width, double Height);
