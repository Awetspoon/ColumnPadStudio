using ColumnPadStudio.Domain.Lists;

namespace ColumnPadStudio.Services;

public static class ClipboardTextService
{
    public static string FormatPastedText(string source, PasteListPreset preset)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        var normalized = NormalizeClipboardText(source);
        return ApplyPastePreset(normalized, preset);
    }

    public static int CountLineBreaks(string text)
    {
        var count = 0;
        foreach (var ch in text)
        {
            if (ch == '\n')
                count++;
        }

        return count;
    }

    public static string NormalizeClipboardText(string source)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        while (source.Contains("\r\r\n", StringComparison.Ordinal))
            source = source.Replace("\r\r\n", "\r\n", StringComparison.Ordinal);

        source = source
            .Replace("\u2028", "\n", StringComparison.Ordinal)
            .Replace("\u2029", "\n", StringComparison.Ordinal)
            .Replace("\n\r", "\n", StringComparison.Ordinal);

        var normalized = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        normalized = CollapseAlternatingBlankClipboardLines(normalized);
        return normalized.Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    public static string ApplyPastePreset(string source, PasteListPreset preset)
    {
        if (preset == PasteListPreset.None || string.IsNullOrEmpty(source))
            return source;

        var normalized = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        var lines = normalized.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]) || ListMarkerRules.HasOrderedListPrefix(lines[i]))
                continue;

            var parsed = ListMarkerRules.ParseLineMarker(lines[i]);
            var bodyStart = parsed.Kind == ListMarkerKind.None
                ? parsed.LeadingWhitespaceLength
                : parsed.LeadingWhitespaceLength + parsed.Prefix.Length;

            var leading = lines[i][..parsed.LeadingWhitespaceLength];
            var body = lines[i][bodyStart..];

            lines[i] = preset switch
            {
                PasteListPreset.Bullets => $"{leading}{ListMarkerRules.MarkdownBulletPrefix}{body}",
                PasteListPreset.Checklist when parsed.Kind == ListMarkerKind.ChecklistChecked => $"{leading}{ListMarkerRules.MarkdownChecklistCheckedPrefix}{body}",
                PasteListPreset.Checklist => $"{leading}{ListMarkerRules.MarkdownChecklistUncheckedPrefix}{body}",
                _ => lines[i]
            };
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string CollapseAlternatingBlankClipboardLines(string text)
    {
        var lines = text.Split('\n');
        if (lines.Length < 6)
            return text;

        for (var i = 0; i < lines.Length - 1; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]) && !string.IsNullOrWhiteSpace(lines[i + 1]))
                return text;
        }

        var evenCount = 0;
        var oddCount = 0;
        var evenBlank = 0;
        var oddBlank = 0;
        var evenContent = 0;
        var oddContent = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var isBlank = string.IsNullOrWhiteSpace(lines[i]);
            if ((i & 1) == 0)
            {
                evenCount++;
                if (isBlank)
                    evenBlank++;
                else
                    evenContent++;
            }
            else
            {
                oddCount++;
                if (isBlank)
                    oddBlank++;
                else
                    oddContent++;
            }
        }

        var collapseOdd = oddCount > 0 &&
                          oddBlank >= (int)Math.Ceiling(oddCount * 0.85) &&
                          evenContent >= 3 &&
                          evenBlank <= 1;
        var collapseEven = evenCount > 0 &&
                           evenBlank >= (int)Math.Ceiling(evenCount * 0.85) &&
                           oddContent >= 3 &&
                           oddBlank <= 1;

        if (!collapseOdd && !collapseEven)
            return text;

        var blankParityToRemove = collapseOdd ? 1 : 0;
        var filtered = lines
            .Where((line, index) => !((index & 1) == blankParityToRemove && string.IsNullOrWhiteSpace(line)))
            .ToArray();

        return string.Join('\n', filtered);
    }
}
