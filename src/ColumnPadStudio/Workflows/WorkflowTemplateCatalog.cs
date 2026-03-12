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
    double Height = 72);

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
    public WorkflowTriggerType Trigger { get; init; } = WorkflowTriggerType.Manual;
    public IReadOnlyList<WorkflowTemplateNode> Nodes { get; init; } = Array.Empty<WorkflowTemplateNode>();
    public IReadOnlyList<WorkflowTemplateConnection> Connections { get; init; } = Array.Empty<WorkflowTemplateConnection>();

    public string DisplayName => $"{Category}: {Name}";

    public WorkflowDefinition CreateWorkflowInstance(string? customName = null)
    {
        var instanceNodes = Nodes.Select(node => new WorkflowDiagramNode
        {
            Id = string.IsNullOrWhiteSpace(node.Id) ? Guid.NewGuid().ToString("N") : node.Id.Trim(),
            Kind = node.Kind,
            Title = node.Title,
            Description = node.Description,
            X = node.X,
            Y = node.Y,
            Width = node.Width,
            Height = node.Height
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
            Trigger = Trigger,
            Nodes = new ObservableCollection<WorkflowDiagramNode>(instanceNodes),
            Links = new ObservableCollection<WorkflowDiagramLink>(instanceLinks),
        };
    }
}

public static class WorkflowTemplateCatalog
{
    public static IReadOnlyList<WorkflowTemplateDefinition> Templates { get; } = BuildTemplates();

    private static IReadOnlyList<WorkflowTemplateDefinition> BuildTemplates()
    {
        return
        [
            BuildLinearTemplate(
                id: "project-planning-kickoff",
                name: "Project Planning Kickoff",
                category: "Project Management",
                description: "Set up a clean planning board with scope, milestones, risks, and delivery notes.",
                trigger: WorkflowTriggerType.Manual,
                nodeTitles:
                [
                    "Define scope",
                    "Capture milestones",
                    "Map risks",
                    "Lock decisions"
                ]),
            BuildLinearTemplate(
                id: "sprint-triage-board",
                name: "Sprint Triage Board",
                category: "Engineering",
                description: "Create a triage-ready layout for backlog grooming and release readiness checks.",
                trigger: WorkflowTriggerType.Manual,
                nodeTitles:
                [
                    "Collect inbox",
                    "Prioritize ready",
                    "Track in-progress",
                    "Review",
                    "Done"
                ]),
            BuildLinearTemplate(
                id: "daily-standup-notes",
                name: "Daily Standup Notes",
                category: "Team Ops",
                description: "Capture yesterday/today/blockers quickly with repeatable structure.",
                trigger: WorkflowTriggerType.OnAppStart,
                nodeTitles:
                [
                    "Yesterday",
                    "Today",
                    "Blockers"
                ]),
            BuildDecisionTemplate(
                id: "bug-investigation-log",
                name: "Bug Investigation Log",
                category: "Engineering",
                description: "Track repro steps, hypotheses, evidence, and fixes in a repeatable flow.",
                trigger: WorkflowTriggerType.Manual,
                startTitle: "Capture repro",
                decisionTitle: "Hypothesis confirmed?",
                yesTitle: "Implement fix",
                noTitle: "Gather more evidence",
                endTitle: "Verify + document"),
            BuildLinearTemplate(
                id: "sop-builder",
                name: "SOP Builder",
                category: "Operations",
                description: "Draft standard operating procedures with reusable sections and checklists.",
                trigger: WorkflowTriggerType.Manual,
                nodeTitles:
                [
                    "Purpose",
                    "Procedure steps",
                    "QA checks",
                    "Notes"
                ])
        ];
    }

    private static WorkflowTemplateDefinition BuildLinearTemplate(
        string id,
        string name,
        string category,
        string description,
        WorkflowTriggerType trigger,
        IReadOnlyList<string> nodeTitles)
    {
        var nodes = new List<WorkflowTemplateNode>
        {
            new("start", WorkflowNodeKind.Start, "Start", string.Empty, 60, 80, 130, 60)
        };

        var y = 190.0;
        var stepIndex = 1;
        foreach (var title in nodeTitles)
        {
            nodes.Add(new WorkflowTemplateNode($"step-{stepIndex}", WorkflowNodeKind.Step, title, string.Empty, 60, y));
            y += 110;
            stepIndex++;
        }

        nodes.Add(new WorkflowTemplateNode("end", WorkflowNodeKind.End, "End", string.Empty, 60, y, 130, 60));

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
        return new WorkflowTemplateDefinition
        {
            Id = id,
            Name = name,
            Category = category,
            Description = description,
            Trigger = trigger,
            Nodes =
            [
                new WorkflowTemplateNode("start", WorkflowNodeKind.Start, startTitle, string.Empty, 60, 90, 150, 60),
                new WorkflowTemplateNode("decision", WorkflowNodeKind.Decision, decisionTitle, string.Empty, 330, 90, 190, 80),
                new WorkflowTemplateNode("yes", WorkflowNodeKind.Step, yesTitle, string.Empty, 640, 40, 190, 72),
                new WorkflowTemplateNode("no", WorkflowNodeKind.Step, noTitle, string.Empty, 640, 180, 190, 72),
                new WorkflowTemplateNode("end", WorkflowNodeKind.End, endTitle, string.Empty, 910, 110, 150, 60)
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
}

