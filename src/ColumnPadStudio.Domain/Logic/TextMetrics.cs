namespace ColumnPadStudio.Domain.Logic;

public static class TextMetrics
{
    public static int GetLineCount(string? text)
    {
        return GetNormalizedLines(text).Length;
    }

    public static int GetWordCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Normalize(text)
            .Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    public static string Normalize(string text)
        => text.Replace("\r\n", "\n").Replace('\r', '\n');

    public static string[] GetNormalizedLines(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new[] { string.Empty };
        }

        return Normalize(text).Split('\n');
    }
}
