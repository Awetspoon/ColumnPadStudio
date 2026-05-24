namespace ColumnPadStudio.Workflows;

public static class WorkflowDefaults
{
    public static WorkflowDefinition CreateDefault(string? name = null)
    {
        var workflow = new WorkflowDefinition
        {
            Name = string.IsNullOrWhiteSpace(name) ? "New Workflow" : name.Trim(),
            Category = "Custom",
            Trigger = WorkflowTriggerType.Manual,
            Description = string.Empty
        };

        PopulateStarterDiagram(workflow);
        return workflow;
    }

    public static void PopulateStarterDiagram(WorkflowDefinition workflow)
    {
        ArgumentNullException.ThrowIfNull(workflow);

        var startNode = new WorkflowDiagramNode
        {
            Id = "start",
            Kind = WorkflowNodeKind.Start,
            Title = "Start",
            X = 80,
            Y = 90,
            Width = 130,
            Height = 60
        };
        var stepNode = new WorkflowDiagramNode
        {
            Id = "step-1",
            Kind = WorkflowNodeKind.Step,
            Title = "Step",
            X = 80,
            Y = 220
        };
        var endNode = new WorkflowDiagramNode
        {
            Id = "end",
            Kind = WorkflowNodeKind.End,
            Title = "End",
            X = 80,
            Y = 350,
            Width = 130,
            Height = 60
        };

        workflow.Nodes.Add(startNode);
        workflow.Nodes.Add(stepNode);
        workflow.Nodes.Add(endNode);
        workflow.Links.Add(new WorkflowDiagramLink { FromNodeId = startNode.Id, ToNodeId = stepNode.Id });
        workflow.Links.Add(new WorkflowDiagramLink { FromNodeId = stepNode.Id, ToNodeId = endNode.Id });
    }
}
