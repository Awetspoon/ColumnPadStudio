namespace ColumnPadStudio.Domain.Models;

public sealed class WorkflowLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public string Label { get; set; } = string.Empty;
}
