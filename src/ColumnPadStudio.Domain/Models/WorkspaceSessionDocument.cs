namespace ColumnPadStudio.Domain.Models;

public sealed class WorkspaceSessionDocument
{
    public int SchemaVersion { get; set; } = 1;
    public int ActiveWorkspaceIndex { get; set; }
    public List<WorkspaceDocument> Workspaces { get; set; } = new();
}
