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
    public WorkflowTriggerType Trigger { get; init; } = WorkflowTriggerType.Manual;
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
            Trigger = Trigger,
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

public static class WorkflowTemplateCatalog
{
    private sealed record WorkflowTemplateNodePayload(
        string Description = "",
        string Goal = "",
        string Instructions = "",
        string ExpectedOutput = "",
        IReadOnlyList<string>? ChecklistItems = null,
        WorkflowNodeColor Color = WorkflowNodeColor.Auto);

    public static IReadOnlyList<WorkflowTemplateDefinition> Templates { get; } = BuildTemplates();

    private static IReadOnlyList<WorkflowTemplateDefinition> BuildTemplates()
    {
        return
        [
            BuildLinearTemplate(
                id: "essay-plan",
                name: "Essay Plan",
                category: "Writing",
                description: "Shape a writing piece from thesis through evidence, structure, draft, and review.",
                trigger: WorkflowTriggerType.Manual,
                nodeTitles:
                [
                    "Define thesis",
                    "Collect evidence",
                    "Outline sections",
                    "Draft columns",
                    "Review and polish"
                ],
                nodeDetails: DetailsByTitle(
                    ("Define thesis", Details(
                        "Pin down the main argument before collecting material.",
                        "Create a one-sentence thesis that can guide every section.",
                        "Write the topic, claim, audience, and tone. Keep rewriting it until it is specific enough to argue.",
                        "A clear thesis sentence plus two or three support points.",
                        ["Write the claim", "Name the audience", "List support points"])),
                    ("Collect evidence", Details(
                        "Gather the facts, quotes, and examples that support the thesis.",
                        "Separate useful evidence from background noise.",
                        "Paste source notes into columns, mark the strongest examples, and note anything that needs checking.",
                        "A short evidence pool grouped by point.",
                        ["Add source names", "Mark strongest quotes", "Flag weak evidence"])),
                    ("Outline sections", Details(
                        "Turn the thesis and evidence into a readable structure.",
                        "Decide the order of sections before drafting.",
                        "Group notes by argument, then write a rough heading for each section.",
                        "A section-by-section outline.",
                        ["Order sections", "Assign evidence", "Check flow"])),
                    ("Draft columns", Details(
                        "Write the draft using the outline as the spine.",
                        "Move from notes to readable paragraphs.",
                        "Draft quickly first, then use columns to compare rough sections side by side.",
                        "A complete rough draft.",
                        ["Draft intro", "Draft body sections", "Draft conclusion"])),
                    ("Review and polish", Details(
                        "Clean up the argument, clarity, and wording.",
                        "Make the final piece tighter and easier to read.",
                        "Check each section against the thesis, remove weak lines, and fix repeated wording.",
                        "A polished final version.",
                        ["Check thesis match", "Remove repeats", "Proofread final text"])))),
            BuildLinearTemplate(
                id: "research-notes",
                name: "Research Notes",
                category: "Writing",
                description: "Capture sources, key claims, quotes, gaps, and follow-up questions.",
                trigger: WorkflowTriggerType.Manual,
                nodeTitles:
                [
                    "Collect sources",
                    "Extract key claims",
                    "Capture useful quotes",
                    "Mark gaps",
                    "Write next questions"
                ]),
            BuildLinearTemplate(
                id: "content-draft-pipeline",
                name: "Content Draft Pipeline",
                category: "Writing",
                description: "Move a content idea from inbox to outline, draft, edit, and publish notes.",
                trigger: WorkflowTriggerType.Manual,
                nodeTitles:
                [
                    "Idea inbox",
                    "Angle",
                    "Outline",
                    "Draft",
                    "Edit",
                    "Publish notes"
                ]),
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
                id: "release-checklist",
                name: "Release Checklist",
                category: "Engineering",
                description: "Run a release through build, smoke checks, notes, packaging, and final upload.",
                trigger: WorkflowTriggerType.Manual,
                nodeTitles:
                [
                    "Build",
                    "Run smoke tests",
                    "Review notes",
                    "Package release",
                    "Upload and verify"
                ],
                nodeDetails: DetailsByTitle(
                    ("Build", Details(
                        "Create a clean app build before release work starts.",
                        "Confirm the current code compiles from the solution.",
                        "Run the build command, read warnings, and fix only release-blocking problems here.",
                        "A clean build output.",
                        ["Build solution", "Check warnings", "Confirm app output exists"])),
                    ("Run smoke tests", Details(
                        "Check the app's main wiring before packaging.",
                        "Catch broken startup, resource, and workflow wiring early.",
                        "Run the smoke suite and note any failing check with the area it belongs to.",
                        "A pass/fail smoke result with notes.",
                        ["Run domain tests", "Run smoke tests", "Launch app once"])),
                    ("Review notes", Details(
                        "Prepare the human-readable update summary.",
                        "Turn the work into release notes without over-explaining.",
                        "List user-visible fixes, cleanup, and known limitations.",
                        "Short release notes ready for GitHub.",
                        ["Write update bullets", "Mention fixes", "List remaining risks"])),
                    ("Package release", Details(
                        "Create the distributable app files.",
                        "Make sure the executable matches the current code.",
                        "Publish/package from the checked build output and keep only needed files.",
                        "A release folder or archive.",
                        ["Publish app", "Open output folder", "Remove junk files"])),
                    ("Upload and verify", Details(
                        "Finish the release after upload.",
                        "Confirm the uploaded release is usable by someone else.",
                        "Check the GitHub release page, asset name, notes, and download result.",
                        "A verified release upload.",
                        ["Attach executable/archive", "Check release notes", "Download-test asset"])))),
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
            BuildDecisionTemplate(
                id: "compare-ideas",
                name: "Compare Ideas",
                category: "Thinking",
                description: "Compare options, decide whether one is strong enough, then capture next action.",
                trigger: WorkflowTriggerType.Manual,
                startTitle: "List options",
                decisionTitle: "Clear winner?",
                yesTitle: "Commit to winner",
                noTitle: "Collect more evidence",
                endTitle: "Write decision note"),
            BuildDecisionTemplate(
                id: "decision-tree",
                name: "Decision Tree",
                category: "Thinking",
                description: "Start with a question, branch possible answers, and close with an action.",
                trigger: WorkflowTriggerType.Manual,
                startTitle: "Define question",
                decisionTitle: "Condition met?",
                yesTitle: "Take path A",
                noTitle: "Take path B",
                endTitle: "Record outcome"),
            BuildLinearTemplate(
                id: "meeting-notes",
                name: "Meeting Notes",
                category: "Team Ops",
                description: "Prepare agenda, capture decisions, assign actions, and review follow-up.",
                trigger: WorkflowTriggerType.Manual,
                nodeTitles:
                [
                    "Agenda",
                    "Discussion notes",
                    "Decisions",
                    "Actions",
                    "Follow-up"
                ]),
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

