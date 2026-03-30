using System.Text.Json;
using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.Application.Services;

public static partial class WorkspaceFileClassifier
{
    public static WorkspaceFileKind Classify(string path, string content)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".txt" => IsTextExport(content) ? WorkspaceFileKind.TextExport : WorkspaceFileKind.RawText,
            ".md" => IsMarkdownExport(content) ? WorkspaceFileKind.MarkdownExport : WorkspaceFileKind.RawMarkdown,
            ".json" => IsSessionJson(content) ? WorkspaceFileKind.Session : WorkspaceFileKind.Layout,
            ".columnpad" => WorkspaceFileKind.Layout,
            _ => WorkspaceFileKind.Layout
        };
    }

    public static bool IsTextExport(string content)
        => TextMetricsRegex().IsMatch(content);

    public static bool IsMarkdownExport(string content)
    {
        var trimmed = content.TrimStart();
        if (!trimmed.StartsWith("## ", StringComparison.Ordinal))
        {
            return false;
        }

        return MarkdownHeadingRegex().IsMatch(content);
    }

    public static bool IsSessionJson(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "workspaces", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.Array)
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^=====\s+.+?\s+=====$", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex TextMetricsRegex();

    [System.Text.RegularExpressions.GeneratedRegex(@"^##\s+.+$", System.Text.RegularExpressions.RegexOptions.Multiline)]
    private static partial System.Text.RegularExpressions.Regex MarkdownHeadingRegex();
}
