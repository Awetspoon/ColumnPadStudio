using System.Collections.ObjectModel;

namespace ColumnPadStudio.Workflows;

public sealed record WorkflowTemplateNode(
    string Id,
    WorkflowNodeKind Kind,
    string Title,
    string Description = "",
    double X = 80,
    double Y = 80,
    double Width = 180,
    double Height = 72,
    WorkflowNodeColor Color = WorkflowNodeColor.Auto,
    string Goal = "",
    string Instructions = "",
    string ExpectedOutput = "",
    IReadOnlyList<string>? ChecklistItems = null);

public sealed record WorkflowTemplateConnection(
    string FromNodeId,
    string ToNodeId,
    string Label = "");

public sealed class WorkflowTemplateDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public IReadOnlyList<WorkflowTemplateNode> Nodes { get; init; } = Array.Empty<WorkflowTemplateNode>();
    public IReadOnlyList<WorkflowTemplateConnection> Connections { get; init; } = Array.Empty<WorkflowTemplateConnection>();

    public WorkflowDefinition CreateWorkflowInstance(string? customName = null)
    {
        var instanceNodes = Nodes.Select(node => new WorkflowDiagramNode
        {
            Id = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString("N") : node.Id.Trim(),
            Kind = node.Kind,
            Title = node.Title,
            Description = node.Description,
            Goal = node.Goal,
            Instructions = node.Instructions,
            ExpectedOutput = node.ExpectedOutput,
            ChecklistItems = CreateChecklistItems(node.ChecklistItems),
            X = node.X,
            Y = node.Y,
            Width = node.Width,
            Height = node.Height,
            Color = node.Color
        }).ToList();

        var idMap = instanceNodes.ToDictionary(n => n.Id, n => n.Id, StringComparer.Ordinal);

        var instanceLinks = new List<WorkflowDiagramLink>();
        if (Connections.Count > 0)
        {
            foreach (var link in Connections)
            {
                if (!idMap.ContainsKey(link.FromNodeId) || !idMap.ContainsKey(link.ToNodeId))
                    continue;

                instanceLinks.Add(new WorkflowDiagramLink
                {
                    FromNodeId = link.FromNodeId,
                    ToNodeId = link.ToNodeId,
                    Label = link.Label
                });
            }
        }
        else
        {
            for (var i = 0; i < instanceNodes.Count - 1; i++)
            {
                instanceLinks.Add(new WorkflowDiagramLink
                {
                    FromNodeId = instanceNodes[i].Id,
                    ToNodeId = instanceNodes[i + 1].Id
                });
            }
        }

        return new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(customName) ? Name : customName.Trim(),
            Category = Category,
            Description = Description,
            Nodes = new ObservableCollection<WorkflowDiagramNode>(instanceNodes),
            Links = new ObservableCollection<WorkflowDiagramLink>(instanceLinks),
        };
    }

    private static ObservableCollection<WorkflowChecklistItem> CreateChecklistItems(IReadOnlyList<string>? checklistItems)
    {
        return new ObservableCollection<WorkflowChecklistItem>(
            (checklistItems ?? Array.Empty<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => new WorkflowChecklistItem { Text = item.Trim() }));
    }
}
