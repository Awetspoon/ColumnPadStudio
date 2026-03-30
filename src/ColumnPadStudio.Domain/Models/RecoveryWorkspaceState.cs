using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.Domain.Models;

public sealed class RecoveryWorkspaceState
{
    public Guid WorkspaceId { get; set; }
    public string? FilePath { get; set; }
    public WorkspaceFileKind FileKind { get; set; } = WorkspaceFileKind.Layout;
    public bool SaveAsRequired { get; set; } = true;
}
