using System.IO;
using System.Text.Json;
using ColumnPadStudio.Models;

namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    private void SetCurrentFileReference(string? path, SaveFileKind kind, bool requiresSaveAs = false)
    {
        CurrentFilePath = string.IsNullOrWhiteSpace(path) ? null : path;
        CurrentFileKind = kind;
        _requiresSaveAsBeforeOverwrite = CurrentFilePath is not null && requiresSaveAs;
        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(CurrentFileKind));
        OnPropertyChanged(nameof(CurrentFileDisplayName));
        OnPropertyChanged(nameof(CanSaveCurrentFileDirectly));
        OnPropertyChanged(nameof(RequiresSaveAsBeforeOverwrite));
    }

    public void SetExternalFileReference(string? path, SaveFileKind kind, bool requiresSaveAs, bool markClean)
    {
        SetCurrentFileReference(path, kind, requiresSaveAs);
        if (markClean)
            MarkClean();
    }

    private void MarkClean()
    {
        _cleanStateSignature = CaptureDirtyState();
        _forceDirty = false;
    }

    private void ForceDirty()
    {
        _forceDirty = true;
    }

    private string CaptureDirtyState()
    {
        return CurrentFileKind switch
        {
            SaveFileKind.TextDocument => BuildSingleDocumentText(),
            SaveFileKind.TextExport => BuildExportText(),
            SaveFileKind.JsonExport => BuildExportJson(),
            _ => JsonSerializer.Serialize(CreateLayoutSnapshot(includeActiveSelection: false, includeImageContent: false))
        };
    }

    private bool IsRawDocumentKind => CurrentFileKind == SaveFileKind.TextDocument;
    private bool IsLossyDocumentKind => IsRawDocumentKind || CurrentFileKind is SaveFileKind.TextExport or SaveFileKind.JsonExport;

    public void PrepareForRichContent()
    {
        if (IsLossyDocumentKind)
            SetCurrentFileReference(null, SaveFileKind.Layout);

        ForceDirty();
    }

    private void PromoteRawDocumentToLayoutIfNeeded(int targetColumnCount)
    {
        if (!IsRawDocumentKind || targetColumnCount <= 1)
            return;

        SetCurrentFileReference(null, SaveFileKind.Layout);
    }

    private static string BuildDocumentTitle(string? sourceLabel)
    {
        var baseName = Path.GetFileNameWithoutExtension(sourceLabel);
        return string.IsNullOrWhiteSpace(baseName) ? "Document" : baseName;
    }
}
