using System.IO;
using ColumnPadStudio.Domain.Workspaces;
using ColumnPadStudio.Models;

namespace ColumnPadStudio.Services;

public sealed record FileDialogDefinition(string FileName, string Filter, string DefaultExt, bool AddExtension = true);

public enum OpenFileLoadKind
{
    TextDocument,
    TextExport,
    JsonExport,
    WorkspaceSession,
    WorkflowJson,
    LayoutJson,
    Unsupported
}

public static class FileWorkflowService
{
    public const string SupportedOpenFileFilter = "Supported Files (*.columnpad.json;*.json;*.txt)|*.columnpad.json;*.json;*.txt|ColumnPad and JSON Files (*.columnpad.json;*.json)|*.columnpad.json;*.json|Text Documents (*.txt)|*.txt|All files (*.*)|*.*";

    public static OpenFileLoadKind ClassifyOpenFile(string? extension, string? content)
    {
        var normalizedExtension = (extension ?? string.Empty).ToLowerInvariant();

        if (string.Equals(normalizedExtension, ".txt", StringComparison.Ordinal))
        {
            return WorkspaceImportRules.LooksLikeTextExport(content)
                ? OpenFileLoadKind.TextExport
                : OpenFileLoadKind.TextDocument;
        }

        return normalizedExtension.EndsWith(".json", StringComparison.Ordinal)
            ? ClassifyJsonFile(content)
            : OpenFileLoadKind.Unsupported;
    }

    private static OpenFileLoadKind ClassifyJsonFile(string? content)
    {
        if (WorkspaceSessionFileService.IsWorkspaceSessionJson(content))
            return OpenFileLoadKind.WorkspaceSession;

        if (WorkspaceImportRules.IsJsonExport(content))
            return OpenFileLoadKind.JsonExport;

        return WorkflowService.IsWorkflowDefinitionJson(content)
            ? OpenFileLoadKind.WorkflowJson
            : OpenFileLoadKind.LayoutJson;
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

            SaveFileKind.TextExport => new FileDialogDefinition(
                FileName: BuildSuggestedSaveFileName(currentFilePath, requiresSaveAsBeforeOverwrite, "ColumnPad_export.txt"),
                Filter: "Text (*.txt)|*.txt|All files (*.*)|*.*",
                DefaultExt: ".txt",
                AddExtension: true),

            SaveFileKind.JsonExport => new FileDialogDefinition(
                FileName: BuildSuggestedSaveFileName(currentFilePath, requiresSaveAsBeforeOverwrite, "ColumnPad_export.json"),
                Filter: "ColumnPad Text Export (*.json)|*.json|All files (*.*)|*.*",
                DefaultExt: ".json",
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
