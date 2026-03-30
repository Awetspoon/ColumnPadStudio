using System.Text;
using System.Text.RegularExpressions;
using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.Domain.Logic;

public static partial class LegacyLayoutMigrationLogic
{
    public const int CurrentLayoutVersion = 14;

    public static string NormalizeLegacyText(string? text)
    {
        return PasteTransformLogic.NormalizeLineBreaks(text ?? string.Empty);
    }

    public static string ExpandEscapedNewlinesWhenSingleLine(string? text)
    {
        var source = text ?? string.Empty;
        if (ContainsRealNewline(source))
        {
            return source;
        }

        return source.Replace("\\r\\n", "\n", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\n", StringComparison.Ordinal);
    }

    public static string MigrateLegacyInlineText(string? text, double? storedWidth, double fontSize)
    {
        var source = text ?? string.Empty;
        if (ContainsRealNewline(source) || !LooksLikeLongSingleLine(source))
        {
            return source;
        }

        var arrowSegmentCount = source.Split("->", StringSplitOptions.None).Length - 1;
        if (arrowSegmentCount >= 3)
        {
            var segments = source.Split("->", StringSplitOptions.None)
                .Select(segment => segment.Trim())
                .Where(segment => segment.Length > 0)
                .ToArray();
            return segments.Length > 1 ? string.Join("\n", segments) : source;
        }

        var width = ColumnWidthLogic.ClampStoredWidth(storedWidth) ?? 360;
        var boundedFontSize = WorkspaceRules.ClampFontSize(fontSize <= 0 ? 13 : fontSize);
        var charactersPerLine = Math.Clamp((int)Math.Floor((width - 32) / Math.Max(6.5, boundedFontSize * 0.62)), 16, 240);
        return HardWrap(source, charactersPerLine);
    }

    public static bool TryMigrateLegacyMarkers(ColumnDocument column)
    {
        var lines = NormalizeLegacyText(column.Text).Split('\n');
        var nonBlankEntries = new List<(int LineIndex, LegacyLine Line)>();
        for (var index = 0; index < lines.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(lines[index]))
            {
                continue;
            }

            nonBlankEntries.Add((index, ParseLegacyLine(lines[index])));
        }

        if (nonBlankEntries.Count == 0)
        {
            column.Text = string.Join("\n", lines);
            return false;
        }

        if (nonBlankEntries.All(entry => entry.Line.Kind is LegacyLineKind.CheckedChecklist or LegacyLineKind.UncheckedChecklist))
        {
            column.MarkerMode = MarkerMode.Checklist;
            column.CheckedLines = nonBlankEntries
                .Where(entry => entry.Line.Kind == LegacyLineKind.CheckedChecklist)
                .Select(entry => entry.LineIndex)
                .ToHashSet();

            foreach (var entry in nonBlankEntries)
            {
                lines[entry.LineIndex] = entry.Line.Content;
            }

            column.Text = string.Join("\n", lines);
            return true;
        }

        if (nonBlankEntries.All(entry => entry.Line.Kind == LegacyLineKind.Bullet))
        {
            column.MarkerMode = MarkerMode.Bullets;
            column.CheckedLines.Clear();
            foreach (var entry in nonBlankEntries)
            {
                lines[entry.LineIndex] = entry.Line.Content;
            }

            column.Text = string.Join("\n", lines);
            return true;
        }

        column.Text = string.Join("\n", lines);
        return false;
    }

    private static bool ContainsRealNewline(string text)
    {
        return text.Contains('\n') || text.Contains('\r');
    }

    private static bool LooksLikeLongSingleLine(string text)
    {
        return text.Trim().Length >= 120;
    }

    private static string HardWrap(string text, int charactersPerLine)
    {
        if (text.Length <= charactersPerLine)
        {
            return text;
        }

        var remaining = text.Trim();
        var wrappedLines = new List<string>();
        while (remaining.Length > charactersPerLine)
        {
            var splitIndex = remaining.LastIndexOf(' ', Math.Min(charactersPerLine, remaining.Length - 1));
            if (splitIndex < charactersPerLine / 2)
            {
                splitIndex = charactersPerLine;
            }

            wrappedLines.Add(remaining[..splitIndex].Trim());
            remaining = remaining[splitIndex..].TrimStart();
        }

        if (remaining.Length > 0)
        {
            wrappedLines.Add(remaining);
        }

        return string.Join("\n", wrappedLines.Where(line => line.Length > 0));
    }

    private static LegacyLine ParseLegacyLine(string line)
    {
        var indentationLength = 0;
        while (indentationLength < line.Length && char.IsWhiteSpace(line[indentationLength]) && line[indentationLength] != '\n')
        {
            indentationLength++;
        }

        var indentation = line[..indentationLength];
        var content = line[indentationLength..];

        if (content.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
        {
            return new LegacyLine(LegacyLineKind.CheckedChecklist, indentation + content[6..]);
        }

        if (content.StartsWith("- [ ] ", StringComparison.Ordinal))
        {
            return new LegacyLine(LegacyLineKind.UncheckedChecklist, indentation + content[6..]);
        }

        if (content.StartsWith("\u2611 ", StringComparison.Ordinal))
        {
            return new LegacyLine(LegacyLineKind.CheckedChecklist, indentation + content[2..]);
        }

        if (content.StartsWith("\u2610 ", StringComparison.Ordinal))
        {
            return new LegacyLine(LegacyLineKind.UncheckedChecklist, indentation + content[2..]);
        }

        if (content.StartsWith("\u2022 ", StringComparison.Ordinal))
        {
            return new LegacyLine(LegacyLineKind.Bullet, indentation + content[2..]);
        }

        if (content.StartsWith("- ", StringComparison.Ordinal) && !OrderedListRegex().IsMatch(content))
        {
            return new LegacyLine(LegacyLineKind.Bullet, indentation + content[2..]);
        }

        return new LegacyLine(LegacyLineKind.Plain, line);
    }

    [GeneratedRegex(@"^\d+([.)])\s+")]
    private static partial Regex OrderedListRegex();

    private readonly record struct LegacyLine(LegacyLineKind Kind, string Content);

    private enum LegacyLineKind
    {
        Plain,
        Bullet,
        UncheckedChecklist,
        CheckedChecklist
    }
}
