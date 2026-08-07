using System.IO;
using System.Text;
using System.Text.Json;

namespace ColumnPadStudio.Domain.Workspaces;

public readonly record struct ImportedColumn(string Title, string Text);

public static class WorkspaceImportRules
{
    public const string TextExportMarker = "ColumnPad Export";
    public const string TextExportFormatLine = "Format: Text";
    public const string TextExportVersionLine = "Version: 2";
    public const string JsonExportFileType = "ColumnPadTextExport";
    public const int CurrentJsonExportVersion = 1;
    public const string WorkspaceSessionFileType = "ColumnPadWorkspaceSession";
    public const int CurrentWorkspaceSessionVersion = 2;

    private const string TextExportHeaderPrefix = "===== ";
    private const string TextExportHeaderSuffix = " =====";

    public static bool IsWorkspaceSessionJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return false;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            var root = document.RootElement;
            if (!root.TryGetProperty("Version", out var versionNode) ||
                versionNode.ValueKind != JsonValueKind.Number ||
                !versionNode.TryGetInt32(out var version) ||
                version < 1 ||
                version > CurrentWorkspaceSessionVersion)
            {
                return false;
            }

            var hasFileType = root.TryGetProperty("FileType", out var fileTypeNode);
            if (hasFileType &&
                (fileTypeNode.ValueKind != JsonValueKind.String ||
                 !string.Equals(fileTypeNode.GetString(), WorkspaceSessionFileType, StringComparison.Ordinal)))
            {
                return false;
            }

            if (version >= CurrentWorkspaceSessionVersion && !hasFileType)
                return false;

            return root.TryGetProperty("Workspaces", out var workspaces) &&
                   workspaces.ValueKind == JsonValueKind.Array &&
                   workspaces.GetArrayLength() > 0;
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

    public static bool IsJsonExport(string? json)
    {
        try
        {
            _ = ParseJsonExportColumns(json);
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static List<ImportedColumn> ParseTextExportColumns(string? text)
    {
        var normalized = NormalizeLineEndings(text);
        var exportBody = StripTextExportPreamble(normalized.Split('\n'));
        var lines = exportBody.Lines;
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
            if (exportBody.UsesEscaping && TryUnescapeBodyLine(line, out var unescapedLine))
            {
                currentTitle ??= "Column 1";
                skipInitialBlank = false;
                body.Append(unescapedLine).Append('\n');
                continue;
            }

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

    public static List<ImportedColumn> ParseJsonExportColumns(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("The JSON export is empty.");

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("FileType", out var fileTypeNode) ||
                fileTypeNode.ValueKind != JsonValueKind.String ||
                !string.Equals(fileTypeNode.GetString(), JsonExportFileType, StringComparison.Ordinal) ||
                !root.TryGetProperty("Version", out var versionNode) ||
                versionNode.ValueKind != JsonValueKind.Number ||
                !versionNode.TryGetInt32(out var version) ||
                version < 1 ||
                version > CurrentJsonExportVersion ||
                !root.TryGetProperty("Columns", out var columnsNode) ||
                columnsNode.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("This is not a supported ColumnPad text export.");
            }

            var columns = new List<ImportedColumn>(columnsNode.GetArrayLength());
            foreach (var columnNode in columnsNode.EnumerateArray())
            {
                if (columnNode.ValueKind != JsonValueKind.Object ||
                    !columnNode.TryGetProperty("Title", out var titleNode) ||
                    titleNode.ValueKind != JsonValueKind.String ||
                    !columnNode.TryGetProperty("Text", out var textNode) ||
                    textNode.ValueKind != JsonValueKind.String)
                {
                    throw new InvalidDataException("A ColumnPad text export contains an invalid column.");
                }

                columns.Add(new ImportedColumn(titleNode.GetString() ?? string.Empty, textNode.GetString() ?? string.Empty));
            }

            return columns;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("The JSON export could not be read.", ex);
        }
    }

    public static string EscapeTextExportBody(string? text)
    {
        return EscapeExportBody(text, line => line.StartsWith('\\') ||
                                                  TryParseTextExportHeader(line, out _));
    }

    private static string NormalizeLineEndings(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');
    }

    private static ExportBody StripTextExportPreamble(string[] lines)
    {
        if (lines.Length == 0 || !string.Equals(lines[0].Trim(), TextExportMarker, StringComparison.Ordinal))
            return new ExportBody(lines, UsesEscaping: false);

        var index = 1;
        if (index < lines.Length && string.Equals(lines[index].Trim(), TextExportFormatLine, StringComparison.Ordinal))
            index++;

        var usesEscaping = false;
        if (index < lines.Length && string.Equals(lines[index].Trim(), TextExportVersionLine, StringComparison.Ordinal))
        {
            usesEscaping = true;
            index++;
        }
        else if (index < lines.Length && lines[index].TrimStart().StartsWith("Version:", StringComparison.Ordinal))
        {
            throw new InvalidDataException("This text export was created by a newer version of ColumnPad.");
        }

        while (index < lines.Length && lines[index].Length == 0)
            index++;

        return new ExportBody(lines[index..], usesEscaping);
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

    private static string EscapeExportBody(string? text, Func<string, bool> shouldEscape)
    {
        var lines = NormalizeLineEndings(text).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            if (shouldEscape(lines[index]))
                lines[index] = "\\" + lines[index];
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryUnescapeBodyLine(string line, out string unescaped)
    {
        if (line.StartsWith('\\'))
        {
            unescaped = line[1..];
            return true;
        }

        unescaped = line;
        return false;
    }

    private readonly record struct ExportBody(string[] Lines, bool UsesEscaping);
}
