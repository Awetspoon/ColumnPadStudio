using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.Domain.Logic;

public static class MarkerFormatter
{
    private const string BulletGlyph = "\u2022";
    private const string UncheckedGlyph = "\u2610";
    private const string CheckedGlyph = "\u2611";

    public static string BuildGutter(MarkerMode mode, string? text, IReadOnlySet<int>? checkedLines = null, bool showLineNumbers = true)
    {
        var lineCount = TextMetrics.GetLineCount(text);
        var lines = new List<string>(lineCount);

        for (var i = 0; i < lineCount; i++)
        {
            var lineNumber = i + 1;
            lines.Add(mode switch
            {
                MarkerMode.Numbers => showLineNumbers ? lineNumber.ToString() : string.Empty,
                MarkerMode.Bullets => BulletGlyph,
                MarkerMode.Checklist => checkedLines is not null && checkedLines.Contains(i) ? CheckedGlyph : UncheckedGlyph,
                _ => lineNumber.ToString()
            });
        }

        return string.Join(Environment.NewLine, lines);
    }
}
