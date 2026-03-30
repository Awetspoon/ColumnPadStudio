using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.Domain.Logic;

public static class ChecklistMetrics
{
    private const string UncheckedGlyph = "\u2610 ";
    private const string CheckedGlyph = "\u2611 ";
    private const string MarkdownUnchecked = "- [ ] ";
    private const string MarkdownChecked = "- [x] ";

    public static (int Total, int Done) GetMetrics(MarkerMode mode, string? text, IReadOnlySet<int>? checkedLines = null)
    {
        var lines = TextMetrics.GetNormalizedLines(text);

        if (mode == MarkerMode.Checklist)
        {
            var visibleLineIndexes = lines
                .Select((line, index) => new { line, index })
                .Where(x => !string.IsNullOrWhiteSpace(x.line))
                .Select(x => x.index)
                .ToList();

            var total = visibleLineIndexes.Count;
            var done = checkedLines is null
                ? 0
                : visibleLineIndexes.Count(i => checkedLines.Contains(i));

            return (total, done);
        }

        var inlineChecked = 0;
        var inlineTotal = 0;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith(UncheckedGlyph, StringComparison.Ordinal) ||
                trimmed.StartsWith(CheckedGlyph, StringComparison.Ordinal) ||
                trimmed.StartsWith(MarkdownUnchecked, StringComparison.Ordinal) ||
                trimmed.StartsWith(MarkdownChecked, StringComparison.OrdinalIgnoreCase))
            {
                inlineTotal++;
                if (trimmed.StartsWith(CheckedGlyph, StringComparison.Ordinal) ||
                    trimmed.StartsWith(MarkdownChecked, StringComparison.OrdinalIgnoreCase))
                {
                    inlineChecked++;
                }
            }
        }

        return (inlineTotal, inlineChecked);
    }
}
