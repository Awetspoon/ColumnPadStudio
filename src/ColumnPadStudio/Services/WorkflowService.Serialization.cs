using System.Collections.ObjectModel;
using System.Text.Json;
using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.Services;

public sealed partial class WorkflowService
{
    public static bool IsWorkflowDefinitionJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var root = document.RootElement;
            if (TryGetPropertyIgnoreCase(root, nameof(WorkflowDefinition.FileType), out var fileType) &&
                fileType.ValueKind == JsonValueKind.String &&
                !string.Equals(fileType.GetString(), WorkflowDefinition.WorkflowFileType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return TryGetPropertyIgnoreCase(root, nameof(WorkflowDefinition.Nodes), out var nodes) &&
                   nodes.ValueKind == JsonValueKind.Array &&
                   TryGetPropertyIgnoreCase(root, nameof(WorkflowDefinition.Links), out var links) &&
                   links.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static WorkflowDefinition Snapshot(WorkflowDefinition source)
    {
        return new WorkflowDefinition
        {
            SchemaVersion = source.SchemaVersion,
            Id = source.Id,
            Name = source.Name,
            Category = source.Category,
            Description = source.Description,
            Trigger = source.Trigger,
            Nodes = new ObservableCollection<WorkflowDiagramNode>(
                source.Nodes.Select(node => new WorkflowDiagramNode
                {
                    Id = node.Id,
                    Kind = node.Kind,
                    Title = node.Title,
                    Description = node.Description,
                    Goal = node.Goal,
                    Instructions = node.Instructions,
                    ExpectedOutput = node.ExpectedOutput,
                    ChecklistItems = CopyChecklistItems(node.ChecklistItems),
                    X = node.X,
                    Y = node.Y,
                    Width = node.Width,
                    Height = node.Height,
                    Color = node.Color
                })),
            Links = new ObservableCollection<WorkflowDiagramLink>(
                source.Links.Select(link => new WorkflowDiagramLink
                {
                    Id = link.Id,
                    FromNodeId = link.FromNodeId,
                    ToNodeId = link.ToNodeId,
                    Label = link.Label
                }))
        };
    }

    private static void Normalize(WorkflowDefinition workflow, string? fallbackName)
    {
        workflow.SchemaVersion = Math.Max(3, workflow.SchemaVersion);
        workflow.Id = string.IsNullOrWhiteSpace(workflow.Id)
            ? Guid.NewGuid().ToString("N")
            : workflow.Id.Trim();
        workflow.Name = string.IsNullOrWhiteSpace(workflow.Name)
            ? string.IsNullOrWhiteSpace(fallbackName) ? "New Workflow" : fallbackName.Trim()
            : workflow.Name.Trim();
        workflow.Category = string.IsNullOrWhiteSpace(workflow.Category)
            ? "Custom"
            : workflow.Category.Trim();

        workflow.Description ??= string.Empty;
        workflow.Nodes ??= [];
        workflow.Links ??= [];

        if (workflow.Nodes.Count == 0)
            WorkflowDefaults.PopulateStarterDiagram(workflow);

        EnsureUniqueNodeIds(workflow.Nodes);
        NormalizeNodeContent(workflow.Nodes);
        EnsureUniqueLinkIds(workflow.Links);

        var nodeIds = new HashSet<string>(workflow.Nodes.Select(node => node.Id), StringComparer.Ordinal);
        for (var index = workflow.Links.Count - 1; index >= 0; index--)
        {
            var link = workflow.Links[index];
            if (string.IsNullOrWhiteSpace(link.FromNodeId) ||
                string.IsNullOrWhiteSpace(link.ToNodeId) ||
                !nodeIds.Contains(link.FromNodeId) ||
                !nodeIds.Contains(link.ToNodeId))
            {
                workflow.Links.RemoveAt(index);
            }
        }
    }

    private static ObservableCollection<WorkflowChecklistItem> CopyChecklistItems(
        IEnumerable<WorkflowChecklistItem>? items)
    {
        return new ObservableCollection<WorkflowChecklistItem>(
            (items ?? Array.Empty<WorkflowChecklistItem>())
            .Where(item => !string.IsNullOrWhiteSpace(item.Text))
            .Select(item => new WorkflowChecklistItem
            {
                Text = item.Text.Trim(),
                IsDone = item.IsDone
            }));
    }

    private static void EnsureUniqueNodeIds(IEnumerable<WorkflowDiagramNode> nodes)
    {
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var candidate = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString("N") : node.Id.Trim();
            while (!usedIds.Add(candidate))
                candidate = Guid.NewGuid().ToString("N");

            node.Id = candidate;
            if (string.IsNullOrWhiteSpace(node.Title))
                node.Title = WorkflowDiagramNode.DefaultTitleForKind(node.Kind);
        }
    }

    private static void NormalizeNodeContent(IEnumerable<WorkflowDiagramNode> nodes)
    {
        foreach (var node in nodes)
            node.ChecklistItems = CopyChecklistItems(node.ChecklistItems);
    }

    private static void EnsureUniqueLinkIds(IEnumerable<WorkflowDiagramLink> links)
    {
        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in links)
        {
            var candidate = string.IsNullOrWhiteSpace(link.Id) ? Guid.NewGuid().ToString("N") : link.Id.Trim();
            while (!usedIds.Add(candidate))
                candidate = Guid.NewGuid().ToString("N");

            link.Id = candidate;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                continue;

            value = property.Value;
            return true;
        }

        value = default;
        return false;
    }
}
