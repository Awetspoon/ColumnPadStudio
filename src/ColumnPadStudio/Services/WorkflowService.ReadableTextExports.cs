using System.Globalization;
using System.Text;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Services;

public sealed partial class WorkflowService
{
    public string BuildTextExport(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        Normalize(workflow, fallbackName: null);
        var export = Snapshot(workflow);
        var orderedNodes = GetReadableNodeOrder(export);
        var nodeIndexes = BuildNodeIndexes(orderedNodes);

        var builder = new StringBuilder();
        builder.AppendLine(TextExportMarker);
        builder.AppendLine(TextExportFormatLine);
        builder.AppendLine("Readable copy only; import the .workflow.json file to continue editing.");
        builder.AppendLine();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Workflow: {CleanSingleLine(export.Name, "New Workflow")}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Category: {CleanSingleLine(export.Category, "Custom")}");
        AppendTextBlock(builder, "Description", export.Description);

        builder.AppendLine();
        builder.AppendLine("Steps");
        builder.AppendLine("-----");

        foreach (var node in orderedNodes)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"{nodeIndexes[node.Id]}. [{node.Kind}] {CleanSingleLine(node.Title, WorkflowDiagramNode.DefaultTitleForKind(node.Kind))}");
            AppendTextBlock(builder, "Description", node.Description);
            AppendTextBlock(builder, "Goal", node.Goal);
            AppendTextBlock(builder, "Instructions", node.Instructions);
            AppendTextBlock(builder, "Expected output", node.ExpectedOutput);
            AppendTextChecklist(builder, node);
            AppendTextNextSteps(builder, export, node, nodeIndexes);
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static void AppendTextBlock(StringBuilder builder, string label, string? value)
    {
        var text = CleanMultiline(value);
        if (text.Length == 0)
            return;

        builder.AppendLine(CultureInfo.InvariantCulture, $"{label}:");
        foreach (var line in text.Split('\n'))
            builder.AppendLine(CultureInfo.InvariantCulture, $"  {line}");
    }

    private static void AppendTextChecklist(StringBuilder builder, WorkflowDiagramNode node)
    {
        var items = node.ChecklistItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .ToList();

        if (items.Count == 0)
            return;

        builder.AppendLine("Checklist:");
        foreach (var item in items)
        {
            var marker = item.IsDone ? "[x]" : "[ ]";
            builder.AppendLine(CultureInfo.InvariantCulture, $"  - {marker} {CleanSingleLine(item.Text, "Checklist item")}");
        }
    }

    private static void AppendTextNextSteps(
        StringBuilder builder,
        WorkflowDefinition workflow,
        WorkflowDiagramNode node,
        IReadOnlyDictionary<string, int> nodeIndexes)
    {
        var links = GetOutgoingLinks(workflow, node.Id).ToList();
        if (links.Count == 0)
            return;

        var nodesById = workflow.Nodes.ToDictionary(item => item.Id, StringComparer.Ordinal);
        builder.AppendLine("Next:");

        foreach (var link in links)
        {
            if (!nodesById.TryGetValue(link.ToNodeId, out var target))
                continue;

            var label = string.IsNullOrWhiteSpace(link.Label)
                ? string.Empty
                : $" ({CleanSingleLine(link.Label, string.Empty)})";
            builder.AppendLine(CultureInfo.InvariantCulture, $"  - {BuildTextNodeReference(target, nodeIndexes)}{label}");
        }
    }

}
