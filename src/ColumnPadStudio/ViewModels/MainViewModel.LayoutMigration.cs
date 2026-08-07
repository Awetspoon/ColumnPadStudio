using ColumnPadStudio.Domain.Lists;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    private const int StructuredTextLayoutVersion = 14;

    private static PasteListPreset ParsePastePreset(string? value)
    {
        if (Enum.TryParse<PasteListPreset>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
            return parsed;

        return PasteListPreset.None;
    }

    private static LineMarkerMode ParseLineMarkerMode(string? value)
    {
        if (Enum.TryParse<LineMarkerMode>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
            return parsed;

        return LineMarkerMode.Numbers;
    }

    private static List<int> NormalizeCheckedChecklistLineIndexes(IEnumerable<int>? lineIndexes)
    {
        var normalized = new SortedSet<int>();
        if (lineIndexes is not null)
        {
            foreach (var lineIndex in lineIndexes)
            {
                if (lineIndex >= 0)
                    normalized.Add(lineIndex);
            }
        }

        return normalized.ToList();
    }

    private static (string Text, LineMarkerMode Mode, List<int> CheckedChecklistLineIndexes) MigrateLegacyLineMarkersIfNeeded(
        int layoutVersion,
        string text,
        LineMarkerMode persistedMode,
        IReadOnlyList<int>? persistedCheckedChecklistLineIndexes)
    {
        var normalizedIndexes = NormalizeCheckedChecklistLineIndexes(persistedCheckedChecklistLineIndexes);
        if (layoutVersion >= StructuredTextLayoutVersion)
            return (text, persistedMode, normalizedIndexes);

        if (string.IsNullOrWhiteSpace(text))
            return (text, persistedMode, normalizedIndexes);

        var lines = text.Split('\n');
        var contentLineCount = 0;
        var bulletLineCount = 0;
        var checklistLineCount = 0;
        var checkedLineIndexes = new List<int>();

        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            contentLineCount++;
            var marker = ListMarkerRules.ParseLineMarker(lines[i]);
            switch (marker.Kind)
            {
                case ListMarkerKind.Bullet:
                    bulletLineCount++;
                    break;
                case ListMarkerKind.ChecklistUnchecked:
                    checklistLineCount++;
                    break;
                case ListMarkerKind.ChecklistChecked:
                    checklistLineCount++;
                    checkedLineIndexes.Add(i);
                    break;
            }
        }

        if (contentLineCount == 0)
            return (text, persistedMode, normalizedIndexes);

        if (checklistLineCount == contentLineCount)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var marker = ListMarkerRules.ParseLineMarker(lines[i]);
                if (marker.Kind is ListMarkerKind.ChecklistUnchecked or ListMarkerKind.ChecklistChecked)
                    lines[i] = ListMarkerRules.RemoveMarker(lines[i], marker);
            }

            return (string.Join('\n', lines), LineMarkerMode.Checklist, checkedLineIndexes);
        }

        if (bulletLineCount == contentLineCount)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                var marker = ListMarkerRules.ParseLineMarker(lines[i]);
                if (marker.Kind == ListMarkerKind.Bullet)
                    lines[i] = ListMarkerRules.RemoveMarker(lines[i], marker);
            }

            return (string.Join('\n', lines), LineMarkerMode.Bullets, []);
        }

        return (text, persistedMode, normalizedIndexes);
    }

    private static string NormalizeLoadedColumnText(int layoutVersion, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = text;
        var hasNewLine = normalized.Contains('\n') || normalized.Contains('\r');

        // A short-lived legacy writer escaped every newline. Require both the old
        // schema and its CRLF signature so ordinary code containing "\\n" is untouched.
        if (layoutVersion < StructuredTextLayoutVersion &&
            !hasNewLine &&
            normalized.Contains("\\r\\n", StringComparison.Ordinal))
        {
            normalized = normalized
                .Replace("\\r\\n", "\n", StringComparison.Ordinal)
                .Replace("\\n", "\n", StringComparison.Ordinal)
                .Replace("\\r", "\n", StringComparison.Ordinal);
        }

        return normalized
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);
    }
}
