using ColumnPadStudio.Domain.Text;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Services;

public sealed partial class WorkflowService
{
    private static List<WorkflowDiagramNode> GetReadableNodeOrder(WorkflowDefinition workflow)
    {
        var nodes = workflow.Nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .ToList();
        var nodeById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var outgoingLinks = workflow.Links
            .Where(link => nodeById.ContainsKey(link.FromNodeId) && nodeById.ContainsKey(link.ToNodeId))
            .GroupBy(link => link.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

        var ordered = new List<WorkflowDiagramNode>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var startNodes = nodes
            .Where(node => node.Kind == WorkflowNodeKind.Start)
            .OrderBy(node => node.X)
            .ThenBy(node => node.Y)
            .ToList();

        if (startNodes.Count == 0)
        {
            var incomingIds = workflow.Links
                .Where(link => nodeById.ContainsKey(link.ToNodeId))
                .Select(link => link.ToNodeId)
                .ToHashSet(StringComparer.Ordinal);

            startNodes = nodes
                .Where(node => !incomingIds.Contains(node.Id))
                .OrderBy(node => node.X)
                .ThenBy(node => node.Y)
                .ToList();
        }

        if (startNodes.Count == 0)
        {
            startNodes = nodes
                .OrderBy(node => node.X)
                .ThenBy(node => node.Y)
                .ToList();
        }

        foreach (var node in startNodes)
            AddConnectedNodes(node);

        foreach (var node in nodes.OrderBy(node => node.X).ThenBy(node => node.Y))
            AddConnectedNodes(node);

        return ordered;

        void AddConnectedNodes(WorkflowDiagramNode firstNode)
        {
            var queue = new Queue<WorkflowDiagramNode>();
            queue.Enqueue(firstNode);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                if (!visited.Add(node.Id))
                    continue;

                ordered.Add(node);
                if (!outgoingLinks.TryGetValue(node.Id, out var links))
                    continue;

                foreach (var link in links
                             .Select(link => new { Link = link, Target = nodeById[link.ToNodeId] })
                             .OrderBy(item => item.Target.X)
                             .ThenBy(item => item.Target.Y)
                             .ThenBy(item => item.Link.Label, StringComparer.OrdinalIgnoreCase))
                {
                    queue.Enqueue(link.Target);
                }
            }
        }
    }

    private static Dictionary<string, int> BuildNodeIndexes(IReadOnlyList<WorkflowDiagramNode> orderedNodes)
    {
        return orderedNodes
            .Select((node, index) => new { node.Id, Number = index + 1 })
            .ToDictionary(item => item.Id, item => item.Number, StringComparer.Ordinal);
    }

    private static IEnumerable<WorkflowDiagramLink> GetOutgoingLinks(WorkflowDefinition workflow, string nodeId)
    {
        var nodesById = workflow.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return workflow.Links
            .Where(link => string.Equals(link.FromNodeId, nodeId, StringComparison.Ordinal) &&
                           nodesById.ContainsKey(link.ToNodeId))
            .OrderBy(link => nodesById[link.ToNodeId].X)
            .ThenBy(link => nodesById[link.ToNodeId].Y)
            .ThenBy(link => link.Label, StringComparer.OrdinalIgnoreCase);
    }

    private static string BuildTextNodeReference(
        WorkflowDiagramNode node,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        var title = CleanSingleLine(node.Title, WorkflowDiagramNode.DefaultTitleForKind(node.Kind));
        return nodeIndexes.TryGetValue(node.Id, out var number)
            ? $"{number}. {title}"
            : title;
    }

    private static string CleanSingleLine(string? value, string fallback)
        => DisplayTextRules.CleanSingleLineLabel(value, fallback);

    private static string CleanMultiline(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }
}
