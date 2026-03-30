using ColumnPadStudio.Domain.Logic;
using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.Domain.Models;

public sealed class WorkflowNode
{
    private double _width = WorkflowRules.DefaultNodeWidth;

    public Guid Id { get; set; } = Guid.NewGuid();
    public WorkflowNodeKind Kind { get; set; } = WorkflowNodeKind.Step;
    public WorkflowNodeColor Color { get; set; } = WorkflowNodeColor.Automatic;
    public string Title { get; set; } = "Step";
    public string Description { get; set; } = string.Empty;
    public double X { get; set; } = 48;
    public double Y { get; set; } = 48;
    public double Width
    {
        get => _width;
        set => _width = WorkflowRules.ClampNodeWidth(value <= 0 ? WorkflowRules.DefaultNodeWidth : value);
    }

    public double Height { get; set; } = 72;

    public override string ToString() => Title;
}
