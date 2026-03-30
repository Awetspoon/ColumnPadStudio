using System.Text.RegularExpressions;
using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.Domain.Logic;

public static partial class PasteTransformLogic
{
    public static string PrepareForEditor(string? clipboardText, PastePreset preset)
    {
        var normalized = NormalizeLineBreaks(clipboardText ?? string.Empty);
        var fixedSpacing = CollapseMalformedDoubleSpacing(normalized);
        var transformed = preset switch
        {
            PastePreset.Bullets => ApplyBulletPreset(fixedSpacing),
            PastePreset.Checklist => ApplyChecklistPreset(fixedSpacing),
            _ => fixedSpacing
        };

        return transformed.Replace("\n", Environment.NewLine);
    }

    public static string ApplyPresetToSelectedLines(string? text, int selectionStart, int selectionLength, PastePreset preset)
    {
        var source = text ?? string.Empty;
        if (preset == PastePreset.None)
        {
            return source;
        }

        var (startLineIndex, endLineIndex) = TextSelectionLogic.GetSelectedLineRange(source, selectionStart, selectionLength);
        var lineEnding = source.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = NormalizeLineBreaks(source).Split('\n');

        for (var lineIndex = startLineIndex; lineIndex <= endLineIndex && lineIndex < lines.Length; lineIndex++)
        {
            lines[lineIndex] = preset switch
            {
                PastePreset.Bullets => ApplyBulletPresetToLine(lines[lineIndex]),
                PastePreset.Checklist => ApplyChecklistPresetToLine(lines[lineIndex]),
                _ => lines[lineIndex]
            };
        }

        return string.Join(lineEnding, lines);
    }

    public static string NormalizeLineBreaks(string text)
    {
        return text.Replace("\r\r\n", "\n")
            .Replace("\u2028", "\n")
            .Replace("\u2029", "\n")
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');
    }

    public static string CollapseMalformedDoubleSpacing(string text)
    {
        var lines = text.Split('\n');
        if (lines.Length < 4)
        {
            return text;
        }

        var nonBlankLineCount = 0;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            if (index % 2 == 1)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return text;
                }

                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                return text;
            }

            nonBlankLineCount++;
        }

        if (nonBlankLineCount < 2)
        {
            return text;
        }

        return string.Join("\n", lines.Where((_, index) => index % 2 == 0));
    }

    private static string ApplyBulletPreset(string text)
    {
        return string.Join("\n", text.Split('\n').Select(ApplyBulletPresetToLine));
    }

    private static string ApplyChecklistPreset(string text)
    {
        return string.Join("\n", text.Split('\n').Select(ApplyChecklistPresetToLine));
    }

    private static string ApplyBulletPresetToLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var parsed = ParseLine(line);
        if (parsed.Kind == ParsedLineKind.OrderedList)
        {
            return line;
        }

        return $"{parsed.Indentation}- {parsed.Body}";
    }

    private static string ApplyChecklistPresetToLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        var parsed = ParseLine(line);
        if (parsed.Kind == ParsedLineKind.OrderedList)
        {
            return line;
        }

        var prefix = parsed.Kind == ParsedLineKind.CheckedChecklist ? "- [x] " : "- [ ] ";
        return $"{parsed.Indentation}{prefix}{parsed.Body}";
    }

    private static ParsedLine ParseLine(string line)
    {
        var indentationLength = 0;
        while (indentationLength < line.Length && char.IsWhiteSpace(line[indentationLength]) && line[indentationLength] != '\n')
        {
            indentationLength++;
        }

        var indentation = line[..indentationLength];
        var content = line[indentationLength..];

        if (OrderedListRegex().IsMatch(content))
        {
            return new ParsedLine(indentation, content, ParsedLineKind.OrderedList);
        }

        if (content.StartsWith("- [x] ", StringComparison.OrdinalIgnoreCase))
        {
            return new ParsedLine(indentation, content[6..], ParsedLineKind.CheckedChecklist);
        }

        if (content.StartsWith("- [ ] ", StringComparison.Ordinal))
        {
            return new ParsedLine(indentation, content[6..], ParsedLineKind.UncheckedChecklist);
        }

        if (content.StartsWith("\u2611 ", StringComparison.Ordinal))
        {
            return new ParsedLine(indentation, content[2..], ParsedLineKind.CheckedChecklist);
        }

        if (content.StartsWith("\u2610 ", StringComparison.Ordinal))
        {
            return new ParsedLine(indentation, content[2..], ParsedLineKind.UncheckedChecklist);
        }

        if (content.StartsWith("\u2022 ", StringComparison.Ordinal))
        {
            return new ParsedLine(indentation, content[2..], ParsedLineKind.Bullet);
        }

        if (content.StartsWith("- ", StringComparison.Ordinal))
        {
            return new ParsedLine(indentation, content[2..], ParsedLineKind.Bullet);
        }

        return new ParsedLine(indentation, content, ParsedLineKind.Plain);
    }

    [GeneratedRegex(@"^\d+([.)])\s+")]
    private static partial Regex OrderedListRegex();

    private readonly record struct ParsedLine(string Indentation, string Body, ParsedLineKind Kind);

    private enum ParsedLineKind
    {
        Plain,
        Bullet,
        UncheckedChecklist,
        CheckedChecklist,
        OrderedList
    }
}
