namespace ColumnPadStudio.Domain.Lists;

public readonly record struct ChecklistMetrics(int Total, int Done);

public static class ChecklistMetricsCalculator
{
    public static ChecklistMetrics Compute(string? text)
    {
        var source = text ?? string.Empty;
        var total = 0;
        var done = 0;

        var logicalLines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var line in logicalLines)
        {
            if (ListMarkerRules.IsChecklistUncheckedLine(line))
            {
                total++;
                continue;
            }

            if (ListMarkerRules.IsChecklistCheckedLine(line))
            {
                total++;
                done++;
            }
        }

        return new ChecklistMetrics(total, done);
    }
}
