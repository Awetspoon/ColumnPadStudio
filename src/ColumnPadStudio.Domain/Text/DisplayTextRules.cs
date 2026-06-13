namespace ColumnPadStudio.Domain.Text;

public static class DisplayTextRules
{
    public static string CleanSingleLineLabel(string? value, string fallback)
    {
        var cleaned = NormalizeWhitespace(value);
        return cleaned.Length == 0 ? fallback : cleaned;
    }

    private static string NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var chars = value
            .Trim()
            .Select(ch => char.IsControl(ch) || char.IsWhiteSpace(ch) ? ' ' : ch)
            .ToArray();

        var cleaned = new string(chars);
        while (cleaned.Contains("  ", StringComparison.Ordinal))
            cleaned = cleaned.Replace("  ", " ", StringComparison.Ordinal);

        return cleaned.Trim();
    }
}
