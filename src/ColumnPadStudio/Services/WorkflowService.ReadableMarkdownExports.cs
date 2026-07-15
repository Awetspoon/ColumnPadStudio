using System.Text;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Services;

public sealed partial class WorkflowService
{
    public string BuildMarkdownExport(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        Normalize(workflow, fallbackName: null);
        var export = Snapshot(workflow);
        var orderedNodes = GetReadableNodeOrder(export);
        var nodeIndexes = BuildNodeIndexes(orderedNodes);

        var builder = new StringBuilder();
        builder.AppendLine(MarkdownExportMarker);
        builder.AppendLine();
        builder.AppendLine($"# {EscapeMarkdownInline(CleanSingleLine(export.Name, "New Workflow"))}");
        builder.AppendLine();
        builder.AppendLine($"**Category:** {EscapeMarkdownInline(CleanSingleLine(export.Category, "Custom"))}");
        builder.AppendLine();
        builder.AppendLine($"**Trigger:** {export.Trigger}");
        AppendMarkdownBlock(builder, "Description", export.Description);

        builder.AppendLine();
        builder.AppendLine("## Steps");

        foreach (var node in orderedNodes)
        {
            builder.AppendLine();
            builder.AppendLine($"### {nodeIndexes[node.Id]}. {node.Kind}: {EscapeMarkdownInline(CleanSingleLine(node.Title, WorkflowDiagramNode.DefaultTitleForKind(node.Kind)))}");
            AppendMarkdownBlock(builder, "Description", node.Description);
            AppendMarkdownBlock(builder, "Goal", node.Goal);
            AppendMarkdownBlock(builder, "Instructions", node.Instructions);
            AppendMarkdownBlock(builder, "Expected output", node.ExpectedOutput);
            AppendMarkdownChecklist(builder, node);
            AppendMarkdownNextSteps(builder, export, node, nodeIndexes);
        }

        AppendMarkdownConnections(builder, export, nodeIndexes);
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendMarkdownBlock(StringBuilder builder, string label, string? value)
    {
        var text = CleanMultiline(value);
        if (text.Length == 0)
            return;

        builder.AppendLine();
        builder.AppendLine($"**{label}:**");
        builder.AppendLine();
        builder.AppendLine(text);
    }

    private static void AppendMarkdownChecklist(StringBuilder builder, WorkflowDiagramNode node)
    {
        var items = node.ChecklistItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToList();

        if (items.Count == 0)
            return;

        builder.AppendLine();
        builder.AppendLine("**Checklist:**");
        builder.AppendLine();
        foreach (var item in items)
        {
            var marker = item.IsDone ? "[x]" : "[ ]";
            builder.AppendLine($"- {marker} {item.Text.Trim()}");
        }
    }

    private static void AppendMarkdownNextSteps(
        StringBuilder builder,
        WorkflowDefinition workflow,
        WorkflowDiagramNode node,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        var links = GetOutgoingLinks(workflow, node.Id).ToList();
        if (links.Count == 0)
            return;

        var nodesById = workflow.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        builder.AppendLine();
        builder.AppendLine("**Next:**");
        builder.AppendLine();

        foreach (var link in links)
        {
            if (!nodesById.TryGetValue(link.ToNodeId, out var target))
                continue;

            var label = string.IsNullOrWhiteSpace(link.Label)
                ? string.Empty
                : $" ({EscapeMarkdownInline(CleanSingleLine(link.Label, string.Empty))})";
            builder.AppendLine($"- {EscapeMarkdownInline(BuildTextNodeReference(target, nodeIndexes))}{label}");
        }
    }

    private static void AppendMarkdownConnections(
        StringBuilder builder,
        WorkflowDefinition workflow,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        var nodesById = workflow.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var links = workflow.Links
            .Where(link => nodesById.ContainsKey(link.FromNodeId) && nodesById.ContainsKey(link.ToNodeId))
            .ToList();

        builder.AppendLine();
        builder.AppendLine("## Connections");
        builder.AppendLine();

        if (links.Count == 0)
        {
            builder.AppendLine("No connections.");
            return;
        }

        foreach (var link in links)
        {
            var from = EscapeMarkdownInline(BuildTextNodeReference(nodesById[link.FromNodeId], nodeIndexes));
            var to = EscapeMarkdownInline(BuildTextNodeReference(nodesById[link.ToNodeId], nodeIndexes));
            var label = string.IsNullOrWhiteSpace(link.Label)
                ? string.Empty
                : $" ({EscapeMarkdownInline(CleanSingleLine(link.Label, string.Empty))})";
            builder.AppendLine($"- **{from}** -> **{to}**{label}");
        }
    }

    private static string EscapeMarkdownInline(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal);
    }
}
