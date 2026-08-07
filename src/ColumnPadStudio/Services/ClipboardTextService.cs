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

    public static string NormalizeClipboardText(string source)
    {
        if (string.IsNullOrEmpty(source))
            return string.Empty;

        source = source
            .Replace("\u2028", "\n", StringComparison.Ordinal)
            .Replace("\u2029", "\n", StringComparison.Ordinal);

        var normalized = source
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

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
}
