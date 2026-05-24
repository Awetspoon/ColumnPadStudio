using System.IO;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.ViewModels;

namespace ColumnPadStudio.Services;

public sealed record FileDialogDefinition(string FileName, string Filter, string DefaultExt, bool AddExtension = true);

public enum OpenFileLoadKind
{
    TextDocument,
    MarkdownDocument,
    TextExport,
    MarkdownExport,
    WorkspaceSession,
    LayoutJson
}

public static class FileWorkflowService
{
    public const string SupportedOpenFileFilter = "Supported Files (*.columnpad.json;*.txt;*.md;*.json)|*.columnpad.json;*.txt;*.md;*.json|Layout Files (*.columnpad.json;*.json)|*.columnpad.json;*.json|Text Documents (*.txt)|*.txt|Markdown Documents (*.md)|*.md|All files (*.*)|*.*";

    public static OpenFileLoadKind ClassifyOpenFile(string? extension, string? content)
    {
        var normalizedExtension = (extension ?? string.Empty).ToLowerInvariant();

        return normalizedExtension switch
        {
            ".txt" => WorkspaceImportRules.LooksLikeTextExport(content)
                ? OpenFileLoadKind.TextExport
                : OpenFileLoadKind.TextDocument,
            ".md" => WorkspaceImportRules.LooksLikeMarkdownExport(content)
                ? OpenFileLoadKind.MarkdownExport
                : OpenFileLoadKind.MarkdownDocument,
            _ => WorkspaceSessionFileService.IsWorkspaceSessionJson(content)
                ? OpenFileLoadKind.WorkspaceSession
                : OpenFileLoadKind.LayoutJson
        };
    }

    public static FileDialogDefinition BuildWorkspaceSessionSaveDialog(string? preferredPath)
    {
        var fileName = string.IsNullOrWhiteSpace(preferredPath)
            ? "layout.columnpad.json"
            : Path.GetFileName(preferredPath);

        return new FileDialogDefinition(
            FileName: fileName,
            Filter: "ColumnPad Layout (*.columnpad.json)|*.columnpad.json|JSON (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt: ".columnpad.json",
            AddExtension: true);
    }

    public static FileDialogDefinition BuildSaveDialog(
        SaveFileKind kind,
        string? currentFilePath,
        bool requiresSaveAsBeforeOverwrite)
    {
        return kind switch
        {
            SaveFileKind.TextDocument => new FileDialogDefinition(
                FileName: BuildSuggestedSaveFileName(currentFilePath, requiresSaveAsBeforeOverwrite, "document.txt"),
                Filter: "Text (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt: ".txt",
                AddExtension: true),

            SaveFileKind.MarkdownDocument => new FileDialogDefinition(
                FileName: BuildSuggestedSaveFileName(currentFilePath, requiresSaveAsBeforeOverwrite, "document.md"),
                Filter: "Markdown (*.md)|*.md|All files (*.*)|*.*",
                DefaultExt: ".md",
                AddExtension: true),

            SaveFileKind.TextExport => new FileDialogDefinition(
                FileName: BuildSuggestedSaveFileName(currentFilePath, requiresSaveAsBeforeOverwrite, "ColumnPad_export.txt"),
                Filter: "Text (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt: ".txt",
                AddExtension: true),

            SaveFileKind.MarkdownExport => new FileDialogDefinition(
                FileName: BuildSuggestedSaveFileName(currentFilePath, requiresSaveAsBeforeOverwrite, "ColumnPad_export.md"),
                Filter: "Markdown (*.md)|*.md|All files (*.*)|*.*",
                DefaultExt: ".md",
                AddExtension: true),

            _ => new FileDialogDefinition(
                FileName: BuildSuggestedSaveFileName(currentFilePath, requiresSaveAsBeforeOverwrite, "layout.columnpad.json"),
                Filter: "ColumnPad Layout (*.columnpad.json)|*.columnpad.json|JSON (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt: ".columnpad.json",
                AddExtension: true)
        };
    }

    private static string BuildSuggestedSaveFileName(
        string? currentFilePath,
        bool requiresSaveAsBeforeOverwrite,
        string fallbackName)
    {
        var currentFileName = string.IsNullOrWhiteSpace(currentFilePath)
            ? null
            : Path.GetFileName(currentFilePath);

        if (string.IsNullOrWhiteSpace(currentFileName))
            return fallbackName;

        if (!requiresSaveAsBeforeOverwrite)
            return currentFileName;

        return AppendCopySuffix(currentFileName);
    }

    private static string AppendCopySuffix(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        var baseName = string.IsNullOrWhiteSpace(extension)
            ? fileName
            : Path.GetFileNameWithoutExtension(fileName);

        if (string.IsNullOrWhiteSpace(baseName))
            return fileName;

        return string.IsNullOrWhiteSpace(extension)
            ? $"{baseName}-copy"
            : $"{baseName}-copy{extension}";
    }
}
