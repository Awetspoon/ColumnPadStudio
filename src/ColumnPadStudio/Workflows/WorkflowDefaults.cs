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
            Goal = "Define what starts this workflow.",
            Instructions = "Write the trigger, source material, or question that makes this workflow worth running.",
            ExpectedOutput = "A clear starting point.",
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
            Goal = "Capture the main work for this workflow.",
            Instructions = "Use this step to collect the notes, decisions, or actions that move the workflow forward.",
            ExpectedOutput = "Enough detail for the next step to be obvious.",
            X = 80,
            Y = 220
        };
        var endNode = new WorkflowDiagramNode
        {
            Id = "end",
            Kind = WorkflowNodeKind.End,
            Title = "End",
            Goal = "Close the workflow cleanly.",
            Instructions = "Record the final decision, output, or follow-up action.",
            ExpectedOutput = "A finished note or clear next action.",
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
