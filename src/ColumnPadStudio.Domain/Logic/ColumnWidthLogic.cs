namespace ColumnPadStudio.Domain.Logic;

public static class ColumnWidthLogic
{
    public static double? ClampStoredWidth(double? value)
    {
        return value.HasValue ? WorkspaceRules.ClampColumnWidth(value.Value) : null;
    }

    public static double GetDesiredWorkspaceWidth(IEnumerable<double?> storedWidths, double viewportWidth, double splitterWidth)
    {
        var widths = storedWidths.Select(ClampStoredWidth).ToArray();
        var explicitWidthTotal = widths.Where(width => width.HasValue).Sum(width => width!.Value);
        var flexibleColumnCount = widths.Count(width => !width.HasValue);
        var totalSplitterWidth = Math.Max(0, widths.Length - 1) * Math.Max(0, splitterWidth);

        if (flexibleColumnCount == 0)
        {
            return explicitWidthTotal + totalSplitterWidth;
        }

        var preferredFlexibleWidth = flexibleColumnCount * WorkspaceRules.PreferredFlexibleColumnWidth;
        return Math.Max(
            Math.Max(0, viewportWidth),
            explicitWidthTotal + totalSplitterWidth + preferredFlexibleWidth);
    }

    public static (double Left, double Right) ApplySplitterDelta(double leftWidth, double rightWidth, double delta)
    {
        var minimumWidth = WorkspaceRules.MinColumnWidth;
        var totalWidth = Math.Max(minimumWidth * 2, leftWidth + rightWidth);
        var nextLeftWidth = Math.Clamp(leftWidth + delta, minimumWidth, totalWidth - minimumWidth);
        return (nextLeftWidth, totalWidth - nextLeftWidth);
    }
}
