namespace ColumnPadStudio.Domain.Workspaces;

public static class WorkspaceConstraints
{
    public const int MinColumns = 1;
    public const int MaxColumns = 9999;

    public static int ClampColumnCount(int requestedCount)
        => Math.Clamp(requestedCount, MinColumns, MaxColumns);
}
