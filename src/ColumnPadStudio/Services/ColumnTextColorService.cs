using System.Globalization;
using System.Windows.Media;

namespace ColumnPadStudio.Services;

public static class ColumnTextColorService
{
    public const string ThemeDefault = "Theme";
    public const string Red = "Red";
    public const string Orange = "Orange";
    public const string Green = "Green";
    public const string Teal = "Teal";
    public const string Blue = "Blue";
    public const string Purple = "Purple";
    public const string Grey = "Grey";

    public static IReadOnlyList<string> Presets { get; } =
    [
        ThemeDefault,
        Red,
        Orange,
        Green,
        Teal,
        Blue,
        Purple,
        Grey
    ];

    public static string Normalize(string? value)
    {
        var candidate = value?.Trim();
        foreach (var preset in Presets)
        {
            if (string.Equals(candidate, preset, StringComparison.OrdinalIgnoreCase))
                return preset;
        }

        return TryNormalizeCustomHex(candidate, out var customHex)
            ? customHex
            : ThemeDefault;
    }

    public static bool IsCustom(string? value)
    {
        return TryNormalizeCustomHex(value, out _);
    }

    public static bool TryNormalizeCustomHex(string? value, out string normalized)
    {
        normalized = string.Empty;
        var candidate = value?.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        if (candidate.StartsWith('#'))
            candidate = candidate[1..];

        if (candidate.Length != 6
            || !int.TryParse(candidate, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        normalized = $"#{candidate.ToUpperInvariant()}";
        return true;
    }

    public static SolidColorBrush? CreateCustomBrush(string? value)
    {
        if (!TryNormalizeCustomHex(value, out var normalized))
            return null;

        var rgb = int.Parse(normalized.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var brush = new SolidColorBrush(Color.FromRgb(
            (byte)((rgb >> 16) & 0xFF),
            (byte)((rgb >> 8) & 0xFF),
            (byte)(rgb & 0xFF)));
        if (brush.CanFreeze)
            brush.Freeze();

        return brush;
    }
}
