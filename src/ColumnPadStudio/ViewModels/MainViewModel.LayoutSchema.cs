namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    private sealed record LayoutFile(
        int Version,
        bool ShowLineNumbers,
        bool WordWrap,
        string EditorFontFamily,
        string EditorFontStyle,
        double EditorFontSize,
        string ThemePreset,
        bool SpellCheckEnabled,
        string EditorLanguageTag,
        bool LinedPaperEnabled,
        string? ActiveId,
        int? ActiveIndex,
        List<LayoutColumn> Columns);

    private sealed record LayoutColumn(
        string Title,
        string Text,
        int? WidthPx,
        bool IsWidthLocked,
        string PastePreset,
        string LineMarkerMode,
        List<int> CheckedChecklistLineIndexes,
        string FontFamily,
        double FontSize,
        string FontStyle,
        string FontWeight,
        bool UseDefaultFont);

    private sealed record DirtyWorkspaceState(
        bool ShowLineNumbers,
        bool WordWrap,
        string EditorFontFamily,
        string EditorFontStyle,
        double EditorFontSize,
        string ThemePreset,
        bool SpellCheckEnabled,
        string EditorLanguageTag,
        bool LinedPaperEnabled,
        List<DirtyColumnState> Columns);

    private sealed record DirtyColumnState(
        string Title,
        string Text,
        int? WidthPx,
        bool IsWidthLocked,
        string PastePreset,
        string LineMarkerMode,
        List<int> CheckedChecklistLineIndexes,
        string FontFamily,
        double FontSize,
        string FontStyle,
        string FontWeight,
        bool UseDefaultFont);
}
