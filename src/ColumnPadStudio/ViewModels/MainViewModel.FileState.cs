using System.IO;
using System.Text.Json;

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
            SaveFileKind.TextDocument or SaveFileKind.MarkdownDocument => BuildSingleDocumentText(),
            SaveFileKind.TextExport => BuildExportText(),
            SaveFileKind.MarkdownExport => BuildExportMarkdown(),
            _ => JsonSerializer.Serialize(new DirtyWorkspaceState(
                ShowLineNumbers,
                WordWrap,
                EditorFontFamily,
                EditorFontStyleName,
                EditorFontSize,
                ThemePreset,
                SpellCheckEnabled,
                EditorLanguageTag,
                LinedPaperEnabled,
                Columns.Select(c => new DirtyColumnState(
                    c.Title,
                    c.Text ?? string.Empty,
                    c.WidthPx,
                    c.IsWidthLocked,
                    c.PastePreset.ToString(),
                    c.LineMarkerMode.ToString(),
                    c.GetCheckedChecklistLineIndexes().ToList(),
                    c.Images.Select(image => new LayoutImage(
                        image.FilePath,
                        image.OriginalFileName,
                        image.Width,
                        image.PixelWidth,
                        image.PixelHeight,
                        image.Left,
                        image.Top,
                        image.Layer.ToString())).ToList(),
                    c.EditorFontFamily,
                    c.EditorFontSize,
                    c.EditorFontStyle.ToString(),
                    c.EditorFontWeight.ToString(),
                    c.UseDefaultFont)).ToList()))
        };
    }

    private bool IsRawDocumentKind => CurrentFileKind is SaveFileKind.TextDocument or SaveFileKind.MarkdownDocument;

    public void PrepareForRichContent()
    {
        if (IsRawDocumentKind)
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
