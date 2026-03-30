namespace ColumnPadStudio.Domain.Models;

public sealed class WorkspaceDocument
{
    public int Version { get; set; } = 14;
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Workspace 1";
    public List<ColumnDocument> Columns { get; set; } = new();
    public int ActiveColumnIndex { get; set; }
    public int LastMultiColumnCount { get; set; } = 3;
    public EditorDefaults Defaults { get; set; } = new();
}
