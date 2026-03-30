using System.Text.Json;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Logic;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.App.Views;

public partial class WorkflowBuilderWindow
{
    private static WorkflowDefinition CreateBlankWorkflow()
    {
        var workflow = new WorkflowDefinition
        {
            Name = "New Workflow",
            Category = "General",
            Trigger = WorkflowTriggerType.Manual,
            ChartStyle = WorkflowChartStyle.Flowchart,
            Description = "",
            Nodes = new List<WorkflowNode>
            {
                new() { Kind = WorkflowNodeKind.Start, Title = "Start", Description = "Begin the flow", X = 48, Y = 72 },
                new() { Kind = WorkflowNodeKind.Step, Title = "Step 1", Description = "Do the next thing", X = 288, Y = 72 },
                new() { Kind = WorkflowNodeKind.End, Title = "End", Description = "Finish the flow", X = 528, Y = 72 }
            },
            Links = new List<WorkflowLink>()
        };

        WorkflowChartLayoutLogic.ApplyAutoLayout(workflow.Nodes, workflow.ChartStyle);
        return workflow;
    }

    private static List<WorkflowDefinition> BuildTemplates()
    {
        return new List<WorkflowDefinition>
        {
            BuildLinearTemplate("Project Planning Kickoff", "Planning", WorkflowTriggerType.Manual, WorkflowChartStyle.Flowchart, "Kick off a new project plan.", ["Start", "Define scope", "Review risks", "Export plan"]),
            BuildLinearTemplate("Sprint Triage Board", "Delivery", WorkflowTriggerType.OnAppStart, WorkflowChartStyle.Kanban, "Triage active sprint work at startup.", ["Start", "Pull tickets", "Rank priority", "Assign owners"]),
            BuildLinearTemplate("Daily Standup Notes", "Meetings", WorkflowTriggerType.Manual, WorkflowChartStyle.VerticalTimeline, "Capture yesterday, today, and blockers.", ["Start", "Yesterday", "Today", "Blockers"]),
            BuildLinearTemplate("Bug Investigation Log", "Quality", WorkflowTriggerType.OnFileOpen, WorkflowChartStyle.Flowchart, "Track a bug from capture through fix.", ["Start", "Capture issue", "Test hypothesis", "Record fix"]),
            BuildLinearTemplate("SOP Builder", "Operations", WorkflowTriggerType.OnFileSave, WorkflowChartStyle.HorizontalTimeline, "Draft and polish a standard operating procedure.", ["Start", "Draft steps", "Review gaps", "Publish SOP"]),
            BuildTemplate("Coding Task Delivery", "Development", WorkflowTriggerType.Manual, WorkflowChartStyle.Flowchart, "Take a coding task from brief to verified result.",
            [
                new TemplateNodeSpec(WorkflowNodeKind.Start, "Start", "Open the task"),
                new TemplateNodeSpec(WorkflowNodeKind.Note, "Locked brief", "Keep the rules and scope visible"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Read codebase", "Inspect the real implementation first"),
                new TemplateNodeSpec(WorkflowNodeKind.Decision, "Need clarification?", "Decide whether to continue or stop and ask"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Patch carefully", "Implement one clean slice"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Run checks", "Build, test, and validate"),
                new TemplateNodeSpec(WorkflowNodeKind.End, "Report outcome", "Summarize what changed")
            ]),
            BuildTemplate("Codex Build Pass", "Codex", WorkflowTriggerType.Manual, WorkflowChartStyle.Swimlane, "A focused Codex-assisted implementation pass.",
            [
                new TemplateNodeSpec(WorkflowNodeKind.Start, "Start", "Read the user request"),
                new TemplateNodeSpec(WorkflowNodeKind.Note, "Preserve behavior", "Do not break existing working paths"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Gather context", "Read the relevant files only"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Implement slice", "Make the smallest safe patch"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Verify", "Build and run targeted checks"),
                new TemplateNodeSpec(WorkflowNodeKind.End, "Hand back", "Explain the result and next step")
            ]),
            BuildTemplate("Bugfix Repro to Release", "Quality", WorkflowTriggerType.OnFileOpen, WorkflowChartStyle.Flowchart, "Move a bug from reproduction through validated fix.",
            [
                new TemplateNodeSpec(WorkflowNodeKind.Start, "Start", "Open the reported issue"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Reproduce", "Confirm the problem exists"),
                new TemplateNodeSpec(WorkflowNodeKind.Decision, "Root cause found?", "Continue digging or move to the fix"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Implement fix", "Patch the real cause"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Regression check", "Confirm no behavior slipped"),
                new TemplateNodeSpec(WorkflowNodeKind.End, "Ready to ship", "Close out the fix")
            ]),
            BuildLinearTemplate("Release Readiness", "Release", WorkflowTriggerType.OnFileSave, WorkflowChartStyle.HorizontalTimeline, "Prepare a release build and notes.", ["Start", "Build release", "Check notes", "Package output", "Publish release"]),
            BuildLinearTemplate("Spec to Structure", "Planning", WorkflowTriggerType.Manual, WorkflowChartStyle.VerticalTimeline, "Turn an idea into a locked build structure.", ["Start", "Break down idea", "Lock spec", "Design structure", "Approve build order"]),
            BuildTemplate("Content Review Loop", "Review", WorkflowTriggerType.Manual, WorkflowChartStyle.RadialMap, "Review, revise, and approve a content draft.",
            [
                new TemplateNodeSpec(WorkflowNodeKind.Start, "Start", "Load the draft"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Review issues", "Find the weak spots"),
                new TemplateNodeSpec(WorkflowNodeKind.Decision, "Needs rewrite?", "Decide whether to revise"),
                new TemplateNodeSpec(WorkflowNodeKind.Step, "Revise", "Improve the draft"),
                new TemplateNodeSpec(WorkflowNodeKind.End, "Approve", "Finalize the version")
            ])
        };
    }

    private static WorkflowDefinition BuildLinearTemplate(string name, string category, WorkflowTriggerType trigger, WorkflowChartStyle chartStyle, string description, IReadOnlyList<string> steps)
    {
        var nodes = new List<TemplateNodeSpec>();
        for (var index = 0; index < steps.Count; index++)
        {
            var kind = index == 0
                ? WorkflowNodeKind.Start
                : index == steps.Count - 1
                    ? WorkflowNodeKind.End
                    : WorkflowNodeKind.Step;
            nodes.Add(new TemplateNodeSpec(kind, steps[index], kind.ToString()));
        }

        return BuildTemplate(name, category, trigger, chartStyle, description, nodes);
    }

    private static WorkflowDefinition BuildTemplate(
        string name,
        string category,
        WorkflowTriggerType trigger,
        WorkflowChartStyle chartStyle,
        string description,
        IReadOnlyList<TemplateNodeSpec> nodes)
    {
        var workflow = new WorkflowDefinition
        {
            Name = name,
            Category = category,
            Trigger = trigger,
            ChartStyle = chartStyle,
            Description = description
        };

        for (var index = 0; index < nodes.Count; index++)
        {
            var spec = nodes[index];
            workflow.Nodes.Add(new WorkflowNode
            {
                Kind = spec.Kind,
                Title = spec.Title,
                Description = spec.Description,
                X = 48 + (index * 220),
                Y = 88,
                Width = WorkflowRules.DefaultNodeWidth,
                Height = 72
            });
        }

        for (var index = 0; index < workflow.Nodes.Count - 1; index++)
        {
            workflow.Links.Add(new WorkflowLink
            {
                FromNodeId = workflow.Nodes[index].Id,
                ToNodeId = workflow.Nodes[index + 1].Id
            });
        }

        WorkflowChartLayoutLogic.ApplyAutoLayout(workflow.Nodes, workflow.ChartStyle);
        return workflow;
    }

    private static WorkflowDefinition CloneWorkflow(WorkflowDefinition workflow)
    {
        var json = JsonSerializer.Serialize(workflow);
        return JsonSerializer.Deserialize<WorkflowDefinition>(json) ?? CreateBlankWorkflow();
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var chars = input.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return new string(chars);
    }

    private readonly record struct TemplateNodeSpec(WorkflowNodeKind Kind, string Title, string Description);
}
