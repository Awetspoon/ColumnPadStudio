using System.Text;
using System.Text.Json;

namespace ColumnPadStudio.Domain.Workspaces;

public readonly record struct ImportedColumn(string Title, string Text);

public static class WorkspaceImportRules
{
    public const string TextExportMarker = "ColumnPad Export";
    public const string TextExportFormatLine = "Format: Text";
    public const string MarkdownExportMarker = "<!-- ColumnPad Export: Markdown -->";

    private const string TextExportHeaderPrefix = "===== ";
    private const string TextExportHeaderSuffix = " =====";
    private const string MarkdownHeaderPrefix = "## ";

    public static bool IsWorkspaceSessionJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            return document.RootElement.TryGetProperty("Workspaces", out var workspaces) &&
                   workspaces.ValueKind == JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static bool LooksLikeTextExport(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var lines = NormalizeLineEndings(content).Split('\n');
        return lines.Take(4).Any(line => string.Equals(line.Trim(), TextExportMarker, StringComparison.Ordinal));
    }

    public static bool LooksLikeMarkdownExport(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var lines = NormalizeLineEndings(content).Split('\n');
        return lines.Take(4).Any(line => string.Equals(line.Trim(), MarkdownExportMarker, StringComparison.Ordinal));
    }

    public static List<ImportedColumn> ParseTextExportColumns(string? text)
    {
        var normalized = NormalizeLineEndings(text);
        var lines = StripTextExportPreamble(normalized.Split('\n'));
        var bodyFallback = string.Join('\n', lines);
        var parsed = new List<ImportedColumn>();

        string? currentTitle = null;
        var body = new StringBuilder();
        var skipInitialBlank = false;

        void Flush()
        {
            if (currentTitle is null)
                return;

            parsed.Add(new ImportedColumn(currentTitle, body.ToString().TrimEnd('\n')));
            body.Clear();
        }

        foreach (var line in lines)
        {
            if (TryParseTextExportHeader(line, out var title))
            {
                Flush();
                currentTitle = string.IsNullOrWhiteSpace(title) ? $"Column {parsed.Count + 1}" : title.Trim();
                skipInitialBlank = true;
                continue;
            }

            currentTitle ??= "Column 1";

            if (skipInitialBlank && line.Length == 0)
            {
                skipInitialBlank = false;
                continue;
            }

            skipInitialBlank = false;
            body.Append(line).Append('\n');
        }

        Flush();

        if (parsed.Count == 0)
            parsed.Add(new ImportedColumn("Column 1", bodyFallback.TrimEnd('\n')));

        return parsed;
    }

    public static List<ImportedColumn> ParseMarkdownExportColumns(string? markdown)
    {
        var normalized = NormalizeLineEndings(markdown);
        var lines = StripMarkdownExportPreamble(normalized.Split('\n'));
        var bodyFallback = string.Join('\n', lines);
        var parsed = new List<ImportedColumn>();

        string? currentTitle = null;
        var body = new StringBuilder();
        var skipInitialBlank = false;

        void Flush()
        {
            if (currentTitle is null)
                return;

            parsed.Add(new ImportedColumn(currentTitle, body.ToString().TrimEnd('\n')));
            body.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith(MarkdownHeaderPrefix, StringComparison.Ordinal))
            {
                Flush();
                var heading = line[MarkdownHeaderPrefix.Length..];
                currentTitle = string.IsNullOrWhiteSpace(heading) ? $"Column {parsed.Count + 1}" : heading.Trim();
                skipInitialBlank = true;
                continue;
            }

            currentTitle ??= "Column 1";

            if (skipInitialBlank && line.Length == 0)
            {
                skipInitialBlank = false;
                continue;
            }

            skipInitialBlank = false;
            body.Append(line).Append('\n');
        }

        Flush();

        if (parsed.Count == 0)
            parsed.Add(new ImportedColumn("Column 1", bodyFallback.TrimEnd('\n')));

        return parsed;
    }

    private static string NormalizeLineEndings(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static string[] StripTextExportPreamble(string[] lines)
    {
        if (lines.Length == 0 || !string.Equals(lines[0].Trim(), TextExportMarker, StringComparison.Ordinal))
            return lines;

        var index = 1;
        if (index < lines.Length && string.Equals(lines[index].Trim(), TextExportFormatLine, StringComparison.Ordinal))
            index++;

        while (index < lines.Length && lines[index].Length == 0)
            index++;

        return lines[index..];
    }

    private static string[] StripMarkdownExportPreamble(string[] lines)
    {
        if (lines.Length == 0 || !string.Equals(lines[0].Trim(), MarkdownExportMarker, StringComparison.Ordinal))
            return lines;

        var index = 1;
        while (index < lines.Length && lines[index].Length == 0)
            index++;

        return lines[index..];
    }

    private static bool TryParseTextExportHeader(string line, out string title)
    {
        if (line.StartsWith(TextExportHeaderPrefix, StringComparison.Ordinal) &&
            line.EndsWith(TextExportHeaderSuffix, StringComparison.Ordinal) &&
            line.Length >= TextExportHeaderPrefix.Length + TextExportHeaderSuffix.Length + 1)
        {
            title = line[TextExportHeaderPrefix.Length..^TextExportHeaderSuffix.Length];
            return true;
        }

        title = string.Empty;
        return false;
    }
}
