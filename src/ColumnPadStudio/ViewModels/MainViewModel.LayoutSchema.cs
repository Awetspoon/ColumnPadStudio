namespace ColumnPadStudio.ViewModels;

public sealed partial class MainViewModel
{
    internal sealed record LayoutFile(
        string FileType,
        int Version,
        bool ShowLineNumbers,
        int GutterWidthPx,
        bool WordWrap,
        string EditorFontFamily,
        string EditorFontStyle,
        double EditorFontSize,
        string ThemePreset,
        bool SpellCheckEnabled,
        string EditorLanguageTag,
        bool LinedPaperEnabled,
        string PaperStyle,
        string? ActiveId,
        int? ActiveIndex,
        IReadOnlyList<LayoutColumn> Columns);

    internal sealed record LayoutColumn(
        string Title,
        string Text,
        int? WidthPx,
        bool IsWidthLocked,
        string PastePreset,
        string LineMarkerMode,
        IReadOnlyList<int> CheckedChecklistLineIndexes,
        IReadOnlyList<LayoutImage> Images,
        string FontFamily,
        double FontSize,
        string FontStyle,
        string FontWeight,
        bool UseDefaultFont,
        string EditorTextColor);

    internal sealed record LayoutImage(
        string FilePath,
        string OriginalFileName,
        double Width,
        int PixelWidth,
        int PixelHeight,
        double Left,
        double Top,
        string Layer,
        byte[]? Content);

}
