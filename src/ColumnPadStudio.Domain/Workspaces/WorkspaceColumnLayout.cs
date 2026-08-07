namespace ColumnPadStudio.Domain.Workspaces;

public static class WorkspaceColumnLayout
{
    public static bool UsesFixedColumnStrip(int columnCount, bool fitColumnsToWindow)
        => columnCount > 1 && !fitColumnsToWindow;

    public static double ResolveColumnWidth(int? widthPx, int defaultColumnWidthPx)
    {
        return widthPx is > 0
            ? WorkspaceConstraints.ClampColumnWidth(widthPx.Value)
            : WorkspaceConstraints.ClampColumnWidth(defaultColumnWidthPx);
    }

    public static double CalculateHostWidth(
        IReadOnlyList<int?> columnWidths,
        double viewportWidth,
        int columnSpacingPx,
        bool snapAllColumnsEnabled,
        bool fitColumnsToWindow,
        int defaultColumnWidthPx)
    {
        ArgumentNullException.ThrowIfNull(columnWidths);

        var safeViewportWidth = double.IsFinite(viewportWidth)
            ? Math.Max(0, viewportWidth)
            : 0;

        if (columnWidths.Count == 0)
            return safeViewportWidth;

        if (columnWidths.Count == 1)
            return safeViewportWidth;

        var spacingWidth = snapAllColumnsEnabled
            ? (double)Math.Max(0, columnSpacingPx) * (columnWidths.Count - 1)
            : 0;

        if (!UsesFixedColumnStrip(columnWidths.Count, fitColumnsToWindow))
            return Math.Max(
                (WorkspaceConstraints.MinimumColumnWidth * columnWidths.Count) + spacingWidth,
                safeViewportWidth);

        var contentWidth = columnWidths.Sum(widthPx => ResolveColumnWidth(widthPx, defaultColumnWidthPx));
        contentWidth += spacingWidth;
        return Math.Max(contentWidth, safeViewportWidth);
    }
}
