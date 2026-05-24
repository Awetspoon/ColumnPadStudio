namespace ColumnPadStudio.Services;

public static class ThemePresetService
{
    public const string DefaultPreset = "Default Mode";
    public const string LightPreset = "Light Mode";
    public const string DarkPreset = "Dark Mode";

    public static IReadOnlyList<string> Presets { get; } =
    [
        DefaultPreset,
        LightPreset,
        DarkPreset
    ];

    public static string Normalize(string? value)
    {
        if (string.Equals(value, "Notepad Classic", StringComparison.OrdinalIgnoreCase))
            return LightPreset;

        if (string.Equals(value, "High Contrast", StringComparison.OrdinalIgnoreCase))
            return DarkPreset;

        if (string.Equals(value, "Compact", StringComparison.OrdinalIgnoreCase))
            return DefaultPreset;

        return value switch
        {
            LightPreset => LightPreset,
            DarkPreset => DarkPreset,
            DefaultPreset => DefaultPreset,
            _ => DefaultPreset
        };
    }
}
