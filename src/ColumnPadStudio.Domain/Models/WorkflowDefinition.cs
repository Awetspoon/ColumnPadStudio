using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.Domain.Models;

public sealed class WorkflowDefinition
{
    public int SchemaVersion { get; set; } = 1;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New Workflow";
    public string Category { get; set; } = "General";
    public string Description { get; set; } = string.Empty;
    public WorkflowTriggerType Trigger { get; set; } = WorkflowTriggerType.Manual;
    public WorkflowChartStyle ChartStyle { get; set; } = WorkflowChartStyle.Flowchart;
    public string? FilePath { get; set; }
    public List<WorkflowNode> Nodes { get; set; } = new();
    public List<WorkflowLink> Links { get; set; } = new();

    public override string ToString() => Name;
}
