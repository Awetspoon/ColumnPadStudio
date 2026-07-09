namespace ColumnPadStudio.Workflows;

public static partial class WorkflowTemplateCatalog
{
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

}

