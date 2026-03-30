using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.Domain.Models;

public sealed class EditorDefaults
{
    public string FontFamily { get; set; } = "Consolas";
    public string FontFaceStyle { get; set; } = "Regular";
    public double FontSize { get; set; } = 13;
    public EditorLineStyle LineStyle { get; set; } = EditorLineStyle.StandardRuled;
    public bool ShowLineNumbers { get; set; } = true;
    public bool WordWrap { get; set; } = true;
    public bool LinedPaper { get; set; }
    public bool SpellCheckEnabled { get; set; } = true;
    public string LanguageTag { get; set; } = "en-US";
    public ThemePreset ThemePreset { get; set; } = ThemePreset.Default;
}
