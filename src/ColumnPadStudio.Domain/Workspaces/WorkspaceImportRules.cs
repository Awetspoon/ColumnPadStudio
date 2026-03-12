using System.Text;
using System.Text.Json;

namespace ColumnPadStudio.Domain.Workspaces;

public readonly record struct ImportedColumn(string Title, string Text);

public static class WorkspaceImportRules
{
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

        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        return lines.Any(line => TryParseTextExportHeader(line, out _));
    }

    public static bool LooksLikeMarkdownExport(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (!normalized.TrimStart().StartsWith(MarkdownHeaderPrefix, StringComparison.Ordinal))
            return false;

        var lines = normalized.Split('\n');
        return lines.Any(line => line.StartsWith(MarkdownHeaderPrefix, StringComparison.Ordinal) && line.Length > MarkdownHeaderPrefix.Length);
    }

    public static List<ImportedColumn> ParseTextExportColumns(string? text)
    {
        var normalized = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
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
            parsed.Add(new ImportedColumn("Column 1", normalized.TrimEnd('\n')));

        return parsed;
    }

    public static List<ImportedColumn> ParseMarkdownExportColumns(string? markdown)
    {
        var normalized = (markdown ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        var lines = normalized.Split('\n');
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
            parsed.Add(new ImportedColumn("Column 1", normalized.TrimEnd('\n')));

        return parsed;
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
