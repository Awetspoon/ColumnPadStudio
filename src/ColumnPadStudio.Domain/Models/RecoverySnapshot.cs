namespace ColumnPadStudio.Domain.Models;

public sealed class RecoverySnapshot
{
    public WorkspaceSessionDocument Session { get; set; } = new();
    public DateTime SavedAtLocal { get; set; }
    public string? SessionPath { get; set; }
    public bool SessionSaveAsRequired { get; set; } = true;
    public List<RecoveryWorkspaceState> WorkspaceStates { get; set; } = new();
}
