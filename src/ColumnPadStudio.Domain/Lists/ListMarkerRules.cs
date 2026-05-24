namespace ColumnPadStudio.Domain.Lists;

public static class ListMarkerRules
{
    public const string BulletPrefix = "\u2022 ";
    public const string ChecklistUncheckedPrefix = "\u2610 ";
    public const string ChecklistCheckedPrefix = "\u2611 ";
    public const string MarkdownBulletPrefix = "- ";
    public const string MarkdownChecklistUncheckedPrefix = "- [ ] ";
    public const string MarkdownChecklistCheckedPrefix = "- [x] ";
    public const string MarkdownChecklistCheckedUpperPrefix = "- [X] ";

    private static readonly string[] BulletPrefixes = [BulletPrefix, MarkdownBulletPrefix];
    private static readonly string[] ChecklistUncheckedPrefixes = [ChecklistUncheckedPrefix, MarkdownChecklistUncheckedPrefix];
    private static readonly string[] ChecklistCheckedPrefixes = [ChecklistCheckedPrefix, MarkdownChecklistCheckedPrefix, MarkdownChecklistCheckedUpperPrefix];

    public static LineMarkerInfo ParseLineMarker(string? line)
    {
        var source = line ?? string.Empty;
        var leadingWhitespaceLength = CountLeadingWhitespace(source);
        var marker = ParseMarker(source[leadingWhitespaceLength..]);
        return new LineMarkerInfo(marker.Kind, leadingWhitespaceLength, marker.Prefix);
    }

    public static bool ShouldAutoContinue(LineMarkerInfo marker)
    {
        return marker.Prefix == BulletPrefix ||
               marker.Prefix == ChecklistUncheckedPrefix ||
               marker.Prefix == ChecklistCheckedPrefix;
    }

    public static bool IsChecklistUncheckedLine(string? line)
        => ParseLineMarker(line).Kind == ListMarkerKind.ChecklistUnchecked;

    public static bool IsChecklistCheckedLine(string? line)
        => ParseLineMarker(line).Kind == ListMarkerKind.ChecklistChecked;

    public static bool HasOrderedListPrefix(string? line)
    {
        var source = line ?? string.Empty;
        var leadingWhitespaceLength = CountLeadingWhitespace(source);
        return HasOrderedListPrefix(source.AsSpan(leadingWhitespaceLength));
    }

    public static string RemoveMarker(string? line, LineMarkerInfo marker)
    {
        var source = line ?? string.Empty;
        if (marker.Kind == ListMarkerKind.None)
            return source;

        var prefixStart = marker.LeadingWhitespaceLength;
        var bodyStart = prefixStart + marker.Prefix.Length;
        return string.Concat(source.AsSpan(0, prefixStart), source.AsSpan(bodyStart));
    }

    public static string UpsertMarker(string? line, string prefix)
    {
        var source = line ?? string.Empty;
        var marker = ParseLineMarker(source);
        var bodyStart = marker.Kind == ListMarkerKind.None
            ? marker.LeadingWhitespaceLength
            : marker.LeadingWhitespaceLength + marker.Prefix.Length;

        var leading = source[..marker.LeadingWhitespaceLength];
        var body = source[bodyStart..];
        return $"{leading}{prefix}{body}";
    }

    private static int CountLeadingWhitespace(string line)
    {
        var i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i]))
            i++;
        return i;
    }

    private static MarkerInfo ParseMarker(string line)
    {
        foreach (var prefix in ChecklistUncheckedPrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return new MarkerInfo(ListMarkerKind.ChecklistUnchecked, prefix);
        }

        foreach (var prefix in ChecklistCheckedPrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return new MarkerInfo(ListMarkerKind.ChecklistChecked, prefix);
        }

        foreach (var prefix in BulletPrefixes)
        {
            if (line.StartsWith(prefix, StringComparison.Ordinal))
                return new MarkerInfo(ListMarkerKind.Bullet, prefix);
        }

        return new MarkerInfo(ListMarkerKind.None, string.Empty);
    }

    private static bool HasOrderedListPrefix(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
            return false;

        var index = 0;
        while (index < text.Length && char.IsDigit(text[index]))
            index++;

        if (index == 0 || index >= text.Length)
            return false;

        var markerChar = text[index];
        if (markerChar is not ('.' or ')'))
            return false;

        var whitespaceStart = index + 1;
        return whitespaceStart < text.Length && char.IsWhiteSpace(text[whitespaceStart]);
    }

    private readonly record struct MarkerInfo(ListMarkerKind Kind, string Prefix);
}



