using System.Text;
using ColumnPadStudio.Domain.Lists;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    private static PasteListPreset ParsePastePreset(string? value)
    {
        if (Enum.TryParse<PasteListPreset>(value, ignoreCase: true, out var parsed))
            return parsed;

        return PasteListPreset.None;
    }

    private static LineMarkerMode ParseLineMarkerMode(string? value)
    {
        if (Enum.TryParse<LineMarkerMode>(value, ignoreCase: true, out var parsed))
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
        if (layoutVersion >= CurrentLayoutVersion)
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

    private static string NormalizeLoadedColumnText(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var normalized = text;
        var hasNewLine = normalized.Contains('\n') || normalized.Contains('\r');

        // Legacy files may contain escaped newline text sequences instead of real newlines.
        if (!hasNewLine)
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

    private static string MigrateLegacyInlineTextIfNeeded(int layoutVersion, string text, int? widthPx, double fontSize)
    {
        if (layoutVersion >= CurrentLayoutVersion)
            return text;

        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (text.Contains('\n') || text.Contains('\r'))
            return text;

        if (text.Length < 80)
            return text;

        if (TrySplitArrowChain(text, out var structured))
            return structured;

        return HardWrapAtEstimatedWidth(text, EstimateCharactersPerLine(widthPx, fontSize));
    }

    private static bool TrySplitArrowChain(string text, out string normalized)
    {
        normalized = text;
        if (!text.Contains("->", StringComparison.Ordinal))
            return false;

        var segments = text.Split("->", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 3)
            return false;

        var lines = new List<string>(segments.Length);
        for (var i = 0; i < segments.Length; i++)
        {
            var line = segments[i];
            if (i < segments.Length - 1)
                line += " ->";

            lines.Add(line);
        }

        normalized = string.Join('\n', lines);
        return true;
    }

    private static int EstimateCharactersPerLine(int? widthPx, double fontSize)
    {
        var effectiveWidth = Math.Max(180, widthPx ?? 320) - 72;
        var averageGlyphWidth = Math.Max(6.2, fontSize * 0.58);
        var estimated = (int)Math.Floor(effectiveWidth / averageGlyphWidth);
        return Math.Clamp(estimated, 18, 72);
    }

    private static string HardWrapAtEstimatedWidth(string text, int maxCharsPerLine)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= 1)
            return text;

        var lines = new List<string>();
        var current = new StringBuilder();

        foreach (var word in words)
        {
            if (current.Length == 0)
            {
                current.Append(word);
                continue;
            }

            if (current.Length + 1 + word.Length <= maxCharsPerLine)
            {
                current.Append(' ').Append(word);
                continue;
            }

            lines.Add(current.ToString());
            current.Clear();
            current.Append(word);
        }

        if (current.Length > 0)
            lines.Add(current.ToString());

        return lines.Count <= 1 ? text : string.Join('\n', lines);
    }
}
