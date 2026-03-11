using System.Collections.ObjectModel;

namespace ColumnPadStudio.Workflows;

public sealed record WorkflowTemplateStep(
    WorkflowStepKind Kind,
    string Argument = "",
    string Notes = "");

public sealed class WorkflowTemplateDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Description { get; init; }
    public WorkflowTriggerType Trigger { get; init; } = WorkflowTriggerType.Manual;
    public IReadOnlyList<WorkflowTemplateStep> Steps { get; init; } = Array.Empty<WorkflowTemplateStep>();

    public string DisplayName => $"{Category}: {Name}";

    public WorkflowDefinition CreateWorkflowInstance(string? customName = null)
    {
        var workflow = new WorkflowDefinition
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(customName) ? Name : customName.Trim(),
            Category = Category,
            Description = Description,
            Trigger = Trigger,
            Steps = new ObservableCollection<WorkflowStepDefinition>(
                Steps.Select(step => new WorkflowStepDefinition
                {
                    Kind = step.Kind,
                    Argument = step.Argument,
                    Notes = step.Notes
                }))
        };

        return workflow;
    }
}

public static class WorkflowTemplateCatalog
{
    public static IReadOnlyList<WorkflowTemplateDefinition> Templates { get; } = BuildTemplates();

    private static IReadOnlyList<WorkflowTemplateDefinition> BuildTemplates()
    {
        return new List<WorkflowTemplateDefinition>
        {
            new()
            {
                Id = "project-planning-kickoff",
                Name = "Project Planning Kickoff",
                Category = "Project Management",
                Description = "Set up a clean planning board with scope, milestones, risks, and delivery notes.",
                Trigger = WorkflowTriggerType.Manual,
                Steps =
                [
                    new WorkflowTemplateStep(WorkflowStepKind.SetColumnCount, "4", "Scope | Milestones | Risks | Decisions"),
                    new WorkflowTemplateStep(WorkflowStepKind.ToggleLineNumbers, "On", "Keep numbered planning notes."),
                    new WorkflowTemplateStep(WorkflowStepKind.ToggleWordWrap, "On", "Wrap long planning items."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetTheme, "Default Mode", "Use the warm planning preset.")
                ]
            },
            new()
            {
                Id = "sprint-triage-board",
                Name = "Sprint Triage Board",
                Category = "Engineering",
                Description = "Create a triage-ready layout for backlog grooming and release readiness checks.",
                Trigger = WorkflowTriggerType.Manual,
                Steps =
                [
                    new WorkflowTemplateStep(WorkflowStepKind.SetColumnCount, "5", "Inbox | Ready | In Progress | Review | Done"),
                    new WorkflowTemplateStep(WorkflowStepKind.ToggleLineNumbers, "Off", "Cleaner kanban list view."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetLinedPaper, "Off", "Prefer card-style columns."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetTheme, "Dark Mode", "High-contrast planning sessions.")
                ]
            },
            new()
            {
                Id = "daily-standup-notes",
                Name = "Daily Standup Notes",
                Category = "Team Ops",
                Description = "Capture yesterday/today/blockers quickly with repeatable structure.",
                Trigger = WorkflowTriggerType.OnAppStart,
                Steps =
                [
                    new WorkflowTemplateStep(WorkflowStepKind.SetColumnCount, "3", "Yesterday | Today | Blockers"),
                    new WorkflowTemplateStep(WorkflowStepKind.SetLinedPaper, "On", "Notebook feel for fast updates."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetSpellCheck, "On", "Catch quick typos while typing."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetEditorLanguage, "en-US", "Default team language.")
                ]
            },
            new()
            {
                Id = "sop-builder",
                Name = "SOP Builder",
                Category = "Operations",
                Description = "Draft standard operating procedures with reusable sections and checklists.",
                Trigger = WorkflowTriggerType.Manual,
                Steps =
                [
                    new WorkflowTemplateStep(WorkflowStepKind.SetColumnCount, "4", "Purpose | Steps | QA Checks | Notes"),
                    new WorkflowTemplateStep(WorkflowStepKind.ToggleWordWrap, "On", "Readable long instructions."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetLinedPaper, "On", "Procedure-writing notebook mode."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetTheme, "Light Mode", "Clean print-friendly look.")
                ]
            },
            new()
            {
                Id = "content-calendar",
                Name = "Content Calendar Planner",
                Category = "Marketing",
                Description = "Plan content topics, drafts, publish dates, and distribution notes.",
                Trigger = WorkflowTriggerType.Manual,
                Steps =
                [
                    new WorkflowTemplateStep(WorkflowStepKind.SetColumnCount, "4", "Ideas | Drafting | Scheduled | Published"),
                    new WorkflowTemplateStep(WorkflowStepKind.SetSpellCheck, "On", "Writing quality guardrail."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetEditorLanguage, "en-GB", "UK copy tone default."),
                    new WorkflowTemplateStep(WorkflowStepKind.ToggleLineNumbers, "Off", "Less visual noise for copywork.")
                ]
            },
            new()
            {
                Id = "onboarding-plan",
                Name = "Employee Onboarding Plan",
                Category = "HR",
                Description = "Track onboarding stages, owners, and completion notes in one workspace.",
                Trigger = WorkflowTriggerType.Manual,
                Steps =
                [
                    new WorkflowTemplateStep(WorkflowStepKind.SetColumnCount, "4", "Pre-Start | Week 1 | Month 1 | Handover"),
                    new WorkflowTemplateStep(WorkflowStepKind.SetLinedPaper, "On", "Structured checklist handwriting look."),
                    new WorkflowTemplateStep(WorkflowStepKind.ToggleLineNumbers, "On", "Reference items by line quickly."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetTheme, "Default Mode", "Comfortable onboarding reading tone.")
                ]
            },
            new()
            {
                Id = "research-synthesis",
                Name = "Research Synthesis",
                Category = "Product",
                Description = "Organize findings, themes, decisions, and follow-up actions after interviews.",
                Trigger = WorkflowTriggerType.Manual,
                Steps =
                [
                    new WorkflowTemplateStep(WorkflowStepKind.SetColumnCount, "4", "Raw Notes | Themes | Insights | Actions"),
                    new WorkflowTemplateStep(WorkflowStepKind.ToggleWordWrap, "On", "Long observations stay readable."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetSpellCheck, "On", "Reduce noise in insight docs."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetTheme, "Light Mode", "Presentation-friendly output.")
                ]
            },
            new()
            {
                Id = "bug-investigation-log",
                Name = "Bug Investigation Log",
                Category = "Engineering",
                Description = "Track repro steps, hypotheses, evidence, and fixes in a repeatable JSON workflow.",
                Trigger = WorkflowTriggerType.Manual,
                Steps =
                [
                    new WorkflowTemplateStep(WorkflowStepKind.SetColumnCount, "4", "Repro | Hypothesis | Evidence | Fix"),
                    new WorkflowTemplateStep(WorkflowStepKind.ToggleLineNumbers, "On", "Precise troubleshooting references."),
                    new WorkflowTemplateStep(WorkflowStepKind.SetLinedPaper, "Off", "Dense technical notes layout."),
                    new WorkflowTemplateStep(WorkflowStepKind.SaveCurrentFile, "", "Persist investigation trail early.")
                ]
            }
        };
    }
}
