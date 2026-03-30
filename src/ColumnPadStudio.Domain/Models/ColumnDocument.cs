using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.Domain.Models;

public sealed class ColumnDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Column";
    public string Text { get; set; } = string.Empty;
    public double? Width { get; set; }
    public bool IsWidthLocked { get; set; }
    public PastePreset PastePreset { get; set; }
    public bool UseDefaultFont { get; set; } = true;
    public string FontFamily { get; set; } = "Consolas";
    public double FontSize { get; set; } = 13;
    public string FontStyleName { get; set; } = "Normal";
    public string FontWeightName { get; set; } = "Normal";
    public MarkerMode MarkerMode { get; set; } = MarkerMode.Numbers;
    public HashSet<int> CheckedLines { get; set; } = new();
}
