using System.Text;
using ColumnPadStudio.Domain.Lists;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.Models;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    public string BuildExportText()
    {
        var builder = new StringBuilder();
        builder.AppendLine(WorkspaceImportRules.TextExportMarker);
        builder.AppendLine(WorkspaceImportRules.TextExportFormatLine);
        builder.AppendLine();

        for (var i = 0; i < Columns.Count; i++)
        {
            var column = Columns[i];
            var title = BuildExportTitle(column.Title, i);
            AppendExportSection(builder, $"===== {title} =====", column.Text, i < Columns.Count - 1);
        }

        return builder.ToString();
    }

    public string BuildExportMarkdown()
    {
        var builder = new StringBuilder();
        builder.AppendLine(WorkspaceImportRules.MarkdownExportMarker);
        builder.AppendLine();

        for (var i = 0; i < Columns.Count; i++)
        {
            var column = Columns[i];
            var title = BuildExportTitle(column.Title, i);
            AppendExportSection(builder, $"## {title}", column.Text, i < Columns.Count - 1);
        }

        return builder.ToString();
    }

    public string BuildSingleDocumentText()
        => Columns.FirstOrDefault()?.Text ?? string.Empty;

    public void LoadTextDocument(
        string text,
        string? sourceLabel = null,
        string? sourcePath = null,
        SaveFileKind kind = SaveFileKind.TextDocument)
    {
        Columns.Clear();

        var document = MakeColumn(BuildDocumentTitle(sourceLabel));
        document.Text = text ?? string.Empty;
        document.WidthPx = null;
        document.IsWidthLocked = false;
        document.PastePreset = PasteListPreset.None;
        document.EditorFontFamily = EditorFontFamily;
        document.EditorFontSize = EditorFontSize;
        document.EditorFontStyle = _editorFontStyle;
        document.EditorFontWeight = _editorFontWeight;
        document.UseDefaultFont = true;
        Columns.Add(document);

        ActiveColumnId = document.Id;
        SetCurrentFileReference(sourcePath, kind, requiresSaveAs: !string.IsNullOrWhiteSpace(sourcePath));
        OnPropertyChanged(nameof(ColumnCount));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        StatusText = sourceLabel is null ? "Document opened." : $"Opened: {sourceLabel}";
        MarkClean();
    }

    public void LoadFromExportText(string text, string? sourceLabel = null, string? sourcePath = null)
    {
        var parsed = WorkspaceImportRules.ParseTextExportColumns(text);
        ApplyImportedColumns(parsed, sourceLabel, sourcePath, SaveFileKind.TextExport, "Text imported.");
    }

    public void LoadFromExportMarkdown(string markdown, string? sourceLabel = null, string? sourcePath = null)
    {
        var parsed = WorkspaceImportRules.ParseMarkdownExportColumns(markdown);
        ApplyImportedColumns(parsed, sourceLabel, sourcePath, SaveFileKind.MarkdownExport, "Markdown imported.");
    }

    private static void AppendExportSection(StringBuilder builder, string header, string? body, bool appendSectionBreak)
    {
        builder.AppendLine(header);

        var normalizedBody = NormalizeExportText(body);
        if (normalizedBody.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine(normalizedBody);
        }

        if (appendSectionBreak)
            builder.AppendLine();
    }

    private static string BuildExportTitle(string? title, int index)
    {
        var normalized = NormalizeExportText(title).Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? $"Column {index + 1}" : normalized;
    }

    private static string NormalizeExportText(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .TrimEnd('\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }

    private void ApplyImportedColumns(
        IReadOnlyList<ImportedColumn> parsed,
        string? sourceLabel,
        string? sourcePath,
        SaveFileKind kind,
        string fallbackStatus)
    {
        var imported = parsed.Count > 0
            ? parsed
            : [new ImportedColumn("Column 1", string.Empty)];

        SetColumnCount(imported.Count);

        for (var i = 0; i < imported.Count; i++)
        {
            var (title, text) = imported[i];
            var column = Columns[i];

            column.Title = string.IsNullOrWhiteSpace(title) ? $"Column {i + 1}" : title.Trim();
            column.Text = text ?? string.Empty;
            column.WidthPx = null;
            column.IsWidthLocked = false;
            column.PastePreset = PasteListPreset.None;
            column.LineMarkerMode = LineMarkerMode.Numbers;
            column.SetCheckedChecklistLineIndexes(null);
            column.ClearImages();
            column.EditorFontFamily = EditorFontFamily;
            column.EditorFontSize = EditorFontSize;
            column.EditorFontStyle = _editorFontStyle;
            column.EditorFontWeight = _editorFontWeight;
            column.UseDefaultFont = true;
        }

        ActiveColumnId = Columns.First().Id;
        SetCurrentFileReference(sourcePath, kind, requiresSaveAs: !string.IsNullOrWhiteSpace(sourcePath));
        RequestRebuildColumns?.Invoke(this, EventArgs.Empty);
        RefreshStatus();
        StatusText = sourceLabel is null ? fallbackStatus : $"Opened: {sourceLabel}";
        MarkClean();
    }
}
