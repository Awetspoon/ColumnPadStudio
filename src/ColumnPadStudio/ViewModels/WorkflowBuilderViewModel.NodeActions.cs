using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.ViewModels;

public sealed partial class WorkflowBuilderViewModel
{
    public void AddNode()
    {
        if (SelectedWorkflow is null)
            return;

        var nodeIndex = SelectedWorkflow.Nodes.Count + 1;
        var reference = SelectedNode;

        var node = new WorkflowDiagramNode
        {
            Id = $"node-{nodeIndex}",
            Kind = WorkflowNodeKind.Step,
            Title = $"Step {nodeIndex}",
            X = reference?.X ?? 320,
            Y = (reference?.Y ?? 120) + 110
        };

        SelectedWorkflow.Nodes.Add(node);
        SelectedNode = node;
        OnPropertyChanged(nameof(CanCreateLink));
        StatusText = $"Added node {node.Title}.";
    }

    public bool DuplicateSelectedNode()
    {
        if (SelectedWorkflow is null || SelectedNode is null)
            return false;

        var clone = new WorkflowDiagramNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = SelectedNode.Kind,
            Title = $"{SelectedNode.Title} Copy",
            Description = SelectedNode.Description,
            X = SelectedNode.X + 36,
            Y = SelectedNode.Y + 36,
            Width = SelectedNode.Width,
            Height = SelectedNode.Height
        };

        SelectedWorkflow.Nodes.Add(clone);
        SelectedNode = clone;
        OnPropertyChanged(nameof(CanCreateLink));
        StatusText = "Node duplicated.";
        return true;
    }

    public bool RemoveSelectedNode()
    {
        if (SelectedWorkflow is null || SelectedNode is null)
            return false;

        var node = SelectedNode;
        var index = SelectedWorkflow.Nodes.IndexOf(node);
        if (index < 0)
            return false;

        for (var i = SelectedWorkflow.Links.Count - 1; i >= 0; i--)
        {
            var link = SelectedWorkflow.Links[i];
            if (string.Equals(link.FromNodeId, node.Id, StringComparison.Ordinal) ||
                string.Equals(link.ToNodeId, node.Id, StringComparison.Ordinal))
            {
                SelectedWorkflow.Links.RemoveAt(i);
            }
        }

        SelectedWorkflow.Nodes.RemoveAt(index);
        SelectedNode = SelectedWorkflow.Nodes.Count == 0
            ? null
            : SelectedWorkflow.Nodes[Math.Clamp(index, 0, SelectedWorkflow.Nodes.Count - 1)];

        if (SelectedLink is not null &&
            (!SelectedWorkflow.Links.Contains(SelectedLink)))
        {
            SelectedLink = SelectedWorkflow.Links.FirstOrDefault();
        }

        OnPropertyChanged(nameof(CanCreateLink));
        RefreshLinkPreviews();
        StatusText = "Node removed.";
        return true;
    }

    public bool NudgeSelectedNode(double dx, double dy)
    {
        if (SelectedNode is null)
            return false;

        SelectedNode.X = Math.Max(0, SelectedNode.X + dx);
        SelectedNode.Y = Math.Max(0, SelectedNode.Y + dy);
        RefreshLinkPreviews();
        return true;
    }

    public bool AutoLayoutSelectedWorkflow()
    {
        if (SelectedWorkflow is null || SelectedWorkflow.Nodes.Count == 0)
            return false;

        var ordered = SelectedWorkflow.Nodes
            .OrderBy(node => node.Kind == WorkflowNodeKind.Start ? 0 : node.Kind == WorkflowNodeKind.End ? 2 : 1)
            .ThenBy(node => node.Y)
            .ThenBy(node => node.X)
            .ToList();

        var y = 80.0;
        foreach (var node in ordered)
        {
            node.X = 80;
            node.Y = y;
            y += 110;
        }

        RefreshLinkPreviews();
        StatusText = "Auto-layout applied.";
        return true;
    }

    public bool AddLink()
    {
        if (SelectedWorkflow is null || SelectedWorkflow.Nodes.Count < 2)
            return false;

        var fromNode = SelectedNode ?? SelectedWorkflow.Nodes[0];
        var toNode = SelectedWorkflow.Nodes.FirstOrDefault(node => !string.Equals(node.Id, fromNode.Id, StringComparison.Ordinal))
                     ?? SelectedWorkflow.Nodes[0];

        var link = new WorkflowDiagramLink
        {
            FromNodeId = fromNode.Id,
            ToNodeId = toNode.Id
        };

        SelectedWorkflow.Links.Add(link);
        SelectedLink = link;
        RefreshLinkPreviews();
        StatusText = "Connection added.";
        return true;
    }

    public bool RemoveSelectedLink()
    {
        if (SelectedWorkflow is null || SelectedLink is null)
            return false;

        var index = SelectedWorkflow.Links.IndexOf(SelectedLink);
        if (index < 0)
            return false;

        SelectedWorkflow.Links.RemoveAt(index);
        SelectedLink = SelectedWorkflow.Links.Count == 0
            ? null
            : SelectedWorkflow.Links[Math.Clamp(index, 0, SelectedWorkflow.Links.Count - 1)];

        RefreshLinkPreviews();
        StatusText = "Connection removed.";
        return true;
    }
}
