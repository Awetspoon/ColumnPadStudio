namespace ColumnPadStudio.Workflows;

public static partial class WorkflowTemplateCatalog
{
    private sealed record WorkflowTemplateNodePayload(
        string Description = "",
        string Goal = "",
        string Instructions = "",
        string ExpectedOutput = "",
        IReadOnlyList<string>? ChecklistItems = null,
        WorkflowNodeColor Color = WorkflowNodeColor.Auto);

    private static WorkflowTemplateDefinition BuildLinearTemplate(
        string id,
        string name,
        string category,
        string description,
        WorkflowTriggerType trigger,
        IReadOnlyList<string> nodeTitles,
        IReadOnlyDictionary<string, WorkflowTemplateNodePayload>? nodeDetails = null)
    {
        var startDetails = CreateStartDetails(name);
        var nodes = new List<WorkflowTemplateNode>
        {
            new("start", WorkflowNodeKind.Start, "Start", startDetails.Description, 60, 80, 130, 60)
            {
                Goal = startDetails.Goal,
                Instructions = startDetails.Instructions,
                ExpectedOutput = startDetails.ExpectedOutput,
                ChecklistItems = startDetails.ChecklistItems,
                Color = startDetails.Color
            }
        };

        var y = 190.0;
        var stepIndex = 1;
        foreach (var title in nodeTitles)
        {
            var details = GetStepDetails(name, title, nodeDetails);
            nodes.Add(new WorkflowTemplateNode($"step-{stepIndex}", WorkflowNodeKind.Step, title, details.Description, 60, y)
            {
                Goal = details.Goal,
                Instructions = details.Instructions,
                ExpectedOutput = details.ExpectedOutput,
                ChecklistItems = details.ChecklistItems,
                Color = details.Color
            });
            y += 110;
            stepIndex++;
        }

        var endDetails = CreateEndDetails(name);
        nodes.Add(new WorkflowTemplateNode("end", WorkflowNodeKind.End, "End", endDetails.Description, 60, y, 130, 60)
        {
            Goal = endDetails.Goal,
            Instructions = endDetails.Instructions,
            ExpectedOutput = endDetails.ExpectedOutput,
            ChecklistItems = endDetails.ChecklistItems,
            Color = endDetails.Color
        });

        var links = new List<WorkflowTemplateConnection>();
        for (var i = 0; i < nodes.Count - 1; i++)
        {
            links.Add(new WorkflowTemplateConnection(nodes[i].Id, nodes[i + 1].Id));
        }

        return new WorkflowTemplateDefinition
        {
            Id = id,
            Name = name,
            Category = category,
            Description = description,
            Trigger = trigger,
            Nodes = nodes,
            Connections = links
        };
    }

    private static WorkflowTemplateDefinition BuildDecisionTemplate(
        string id,
        string name,
        string category,
        string description,
        WorkflowTriggerType trigger,
        string startTitle,
        string decisionTitle,
        string yesTitle,
        string noTitle,
        string endTitle)
    {
        var startDetails = Details(
            "Define the question or situation before the branch starts.",
            $"Make the starting point for {name} unambiguous.",
            "Write the source issue, the evidence already known, and what decision needs to be made.",
            "A clear starting brief.",
            ["Write the question", "List known facts", "Name the decision owner"]);
        var decisionDetails = Details(
            "The branch point that decides which path should be taken.",
            "Make the condition testable.",
            "Write the rule in yes/no form and note what evidence proves either side.",
            "A decision condition that another person could follow.",
            ["Define yes condition", "Define no condition", "List evidence needed"],
            WorkflowNodeColor.Amber);
        var yesDetails = Details(
            "The action to take when the condition is true.",
            "Capture the positive path clearly.",
            "Write the action, owner, and any follow-up notes for this route.",
            "A usable action note for the yes path.",
            ["Record action", "Assign owner", "Check follow-up"]);
        var noDetails = Details(
            "The action to take when the condition is false.",
            "Capture the alternate path clearly.",
            "Write what needs more work, who needs to review it, or what should happen next.",
            "A usable action note for the no path.",
            ["Record alternate action", "Identify gap", "Check follow-up"]);
        var endDetails = Details(
            "Close the branch with a final decision or outcome.",
            $"Leave {name} with a clear result.",
            "Summarize the selected path, why it was chosen, and the next action.",
            "A decision note that can be read later.",
            ["Write outcome", "Note reason", "Add next action"]);

        return new WorkflowTemplateDefinition
        {
            Id = id,
            Name = name,
            Category = category,
            Description = description,
            Trigger = trigger,
            Nodes =
            [
                new WorkflowTemplateNode("start", WorkflowNodeKind.Start, startTitle, startDetails.Description, 60, 90, 150, 60)
                {
                    Goal = startDetails.Goal,
                    Instructions = startDetails.Instructions,
                    ExpectedOutput = startDetails.ExpectedOutput,
                    ChecklistItems = startDetails.ChecklistItems
                },
                new WorkflowTemplateNode("decision", WorkflowNodeKind.Decision, decisionTitle, decisionDetails.Description, 330, 90, 190, 80)
                {
                    Goal = decisionDetails.Goal,
                    Instructions = decisionDetails.Instructions,
                    ExpectedOutput = decisionDetails.ExpectedOutput,
                    ChecklistItems = decisionDetails.ChecklistItems,
                    Color = decisionDetails.Color
                },
                new WorkflowTemplateNode("yes", WorkflowNodeKind.Step, yesTitle, yesDetails.Description, 640, 40, 190, 72)
                {
                    Goal = yesDetails.Goal,
                    Instructions = yesDetails.Instructions,
                    ExpectedOutput = yesDetails.ExpectedOutput,
                    ChecklistItems = yesDetails.ChecklistItems
                },
                new WorkflowTemplateNode("no", WorkflowNodeKind.Step, noTitle, noDetails.Description, 640, 180, 190, 72)
                {
                    Goal = noDetails.Goal,
                    Instructions = noDetails.Instructions,
                    ExpectedOutput = noDetails.ExpectedOutput,
                    ChecklistItems = noDetails.ChecklistItems
                },
                new WorkflowTemplateNode("end", WorkflowNodeKind.End, endTitle, endDetails.Description, 910, 110, 150, 60)
                {
                    Goal = endDetails.Goal,
                    Instructions = endDetails.Instructions,
                    ExpectedOutput = endDetails.ExpectedOutput,
                    ChecklistItems = endDetails.ChecklistItems
                }
            ],
            Connections =
            [
                new WorkflowTemplateConnection("start", "decision"),
                new WorkflowTemplateConnection("decision", "yes", "Yes"),
                new WorkflowTemplateConnection("decision", "no", "No"),
                new WorkflowTemplateConnection("yes", "end"),
                new WorkflowTemplateConnection("no", "end")
            ]
        };
    }

    private static WorkflowTemplateNodePayload Details(
        string description,
        string goal,
        string instructions,
        string expectedOutput,
        IReadOnlyList<string>? checklistItems = null,
        WorkflowNodeColor color = WorkflowNodeColor.Auto)
    {
        return new WorkflowTemplateNodePayload(description, goal, instructions, expectedOutput, checklistItems, color);
    }

    private static IReadOnlyDictionary<string, WorkflowTemplateNodePayload> DetailsByTitle(
        params (string Title, WorkflowTemplateNodePayload Details)[] items)
    {
        return items.ToDictionary(item => item.Title, item => item.Details, StringComparer.OrdinalIgnoreCase);
    }

    private static WorkflowTemplateNodePayload GetStepDetails(
        string workflowName,
        string title,
        IReadOnlyDictionary<string, WorkflowTemplateNodePayload>? nodeDetails)
    {
        if (nodeDetails is not null && nodeDetails.TryGetValue(title, out var details))
            return details;

        return Details(
            $"Capture the {title.ToLowerInvariant()} part of the workflow.",
            $"Complete {title.ToLowerInvariant()} for {workflowName}.",
            "Write the useful notes, decisions, blockers, and next action for this step.",
            "A clear note that lets the next step continue without guessing.",
            ["Capture notes", "Mark blockers", "Write next action"]);
    }

    private static WorkflowTemplateNodePayload CreateStartDetails(string workflowName)
    {
        return Details(
            $"Entry point for {workflowName}.",
            "Clarify why this workflow is being started.",
            "Write the trigger, source material, or question that belongs at the beginning.",
            "A clear start brief.",
            ["Name the trigger", "Add source material", "Define success"]);
    }

    private static WorkflowTemplateNodePayload CreateEndDetails(string workflowName)
    {
        return Details(
            $"Close point for {workflowName}.",
            "Finish with an outcome that can be understood later.",
            "Summarize the result, final decision, or next follow-up.",
            "A finished outcome note.",
            ["Write outcome", "Add follow-up", "Check nothing is missing"]);
    }
}
