using ColumnPadStudio.Workflows;

namespace ColumnPadStudio.ViewModels;

public sealed partial class WorkflowBuilderViewModel
{
    private const double NodePlacementGap = 32;

    public void AddNode(WorkflowNodeKind kind)
    {
        if (SelectedWorkflow is null)
            return;

        var reference = SelectedNode;

        var node = new WorkflowDiagramNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = kind,
            Title = CreateUniqueNodeTitle(
                SelectedWorkflow,
                WorkflowDiagramNode.DefaultTitleForKind(kind)),
            Goal = DefaultGoalForKind(kind),
            Instructions = DefaultInstructionsForKind(kind),
            ExpectedOutput = DefaultExpectedOutputForKind(kind)
        };

        (node.X, node.Y) = FindAvailableNodePosition(
            SelectedWorkflow,
            node.Width,
            node.Height,
            reference);

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
            Title = CreateUniqueCopyTitle(SelectedWorkflow, SelectedNode.Title),
            Description = SelectedNode.Description,
            Goal = SelectedNode.Goal,
            Instructions = SelectedNode.Instructions,
            ExpectedOutput = SelectedNode.ExpectedOutput,
            ChecklistItems = new(
                SelectedNode.ChecklistItems.Select(item => new WorkflowChecklistItem
                {
                    Text = item.Text,
                    IsDone = item.IsDone
                })),
            Width = SelectedNode.Width,
            Height = SelectedNode.Height,
            Color = SelectedNode.Color
        };

        (clone.X, clone.Y) = FindAvailableNodePosition(
            SelectedWorkflow,
            clone.Width,
            clone.Height,
            SelectedNode);

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
            y += node.Height + NodePlacementGap;
        }

        RefreshLinkPreviews();
        StatusText = "Positions tidied.";
        return true;
    }

    public bool AddLink()
        => AddConnectionFromDraft();

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

    private static string CreateUniqueNodeTitle(WorkflowDefinition workflow, string baseTitle)
    {
        var existingTitles = workflow.Nodes
            .Select(node => node.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existingTitles.Contains(baseTitle))
            return baseTitle;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseTitle} {suffix}";
            if (!existingTitles.Contains(candidate))
                return candidate;
        }
    }

    private static string CreateUniqueCopyTitle(WorkflowDefinition workflow, string sourceTitle)
    {
        const string copyMarker = " Copy";
        var baseTitle = sourceTitle;
        var markerIndex = sourceTitle.LastIndexOf(copyMarker, StringComparison.OrdinalIgnoreCase);

        if (markerIndex > 0)
        {
            var suffix = sourceTitle[(markerIndex + copyMarker.Length)..];
            if (suffix.Length == 0 ||
                (suffix.StartsWith(' ') && int.TryParse(suffix.AsSpan(1), out var copyNumber) && copyNumber >= 2))
            {
                baseTitle = sourceTitle[..markerIndex];
            }
        }

        return CreateUniqueNodeTitle(workflow, $"{baseTitle}{copyMarker}");
    }

    private static (double X, double Y) FindAvailableNodePosition(
        WorkflowDefinition workflow,
        double width,
        double height,
        WorkflowDiagramNode? reference)
    {
        var x = Math.Max(0, reference?.X ?? 80);
        var y = Math.Max(0, reference is null
            ? 80
            : reference.Y + reference.Height + NodePlacementGap);

        while (true)
        {
            var nextY = y;
            foreach (var node in workflow.Nodes)
            {
                if (!NodeAreasConflict(x, y, width, height, node))
                    continue;

                nextY = Math.Max(nextY, node.Y + node.Height + NodePlacementGap);
            }

            if (nextY == y)
                return (x, y);

            y = nextY;
        }
    }

    private static bool NodeAreasConflict(
        double x,
        double y,
        double width,
        double height,
        WorkflowDiagramNode existing)
        => x < existing.X + existing.Width + NodePlacementGap &&
           x + width + NodePlacementGap > existing.X &&
           y < existing.Y + existing.Height + NodePlacementGap &&
           y + height + NodePlacementGap > existing.Y;

    private static string DefaultGoalForKind(WorkflowNodeKind kind)
        => kind switch
        {
            WorkflowNodeKind.Start => "Define what starts this workflow.",
            WorkflowNodeKind.Decision => "Make the branch condition clear.",
            WorkflowNodeKind.End => "Close the workflow with a clear outcome.",
            WorkflowNodeKind.Note => "Capture supporting context.",
            _ => "Complete this workflow step."
        };

    private static string DefaultInstructionsForKind(WorkflowNodeKind kind)
        => kind switch
        {
            WorkflowNodeKind.Start => "Write the trigger, source material, or opening question.",
            WorkflowNodeKind.Decision => "Write the yes/no rule and the evidence needed to choose a path.",
            WorkflowNodeKind.End => "Summarize the final result and any follow-up.",
            WorkflowNodeKind.Note => "Add context, references, warnings, or reminders that support nearby steps.",
            _ => "Write the useful notes, decisions, blockers, and next action for this step."
        };

    private static string DefaultExpectedOutputForKind(WorkflowNodeKind kind)
        => kind switch
        {
            WorkflowNodeKind.Start => "A clear start brief.",
            WorkflowNodeKind.Decision => "A testable branch condition.",
            WorkflowNodeKind.End => "A finished outcome note.",
            WorkflowNodeKind.Note => "Helpful context for the workflow.",
            _ => "A clear note that lets the next step continue."
        };
}
