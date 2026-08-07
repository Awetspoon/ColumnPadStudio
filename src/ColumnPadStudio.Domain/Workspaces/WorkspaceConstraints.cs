namespace ColumnPadStudio.Domain.Workspaces;

public static class WorkspaceConstraints
{
    public const int MinColumns = 1;
    public const int MaxColumns = 9999;
    public const double MinimumColumnWidth = 220.0;
    public const double DefaultColumnWidth = 320.0;
    public const double MaximumColumnWidth = 5000.0;

    public static int ClampColumnCount(int requestedCount)
        => Math.Clamp(requestedCount, MinColumns, MaxColumns);

    public static double ClampColumnWidth(double requestedWidth)
    {
        if (double.IsNaN(requestedWidth) || double.IsInfinity(requestedWidth))
            return DefaultColumnWidth;

        return Math.Clamp(requestedWidth, MinimumColumnWidth, MaximumColumnWidth);
    }

    public static int ClampColumnWidth(int requestedWidth)
        => (int)ClampColumnWidth((double)requestedWidth);
}
