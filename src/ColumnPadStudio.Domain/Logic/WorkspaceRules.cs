using System.Globalization;

namespace ColumnPadStudio.Domain.Logic;

public static class WorkspaceRules
{
    public const int MinColumns = 1;
    public const int MaxColumns = 9999;
    public const double MinColumnWidth = 120;
    public const double PreferredFlexibleColumnWidth = 360;
    public const double MaxColumnWidth = 5000;
    public const double MinFontSize = 8;
    public const double MaxFontSize = 40;

    public static int ClampColumnCount(int value) => Math.Clamp(value, MinColumns, MaxColumns);
    public static double ClampColumnWidth(double value) => Math.Clamp(value, MinColumnWidth, MaxColumnWidth);
    public static double ClampFontSize(double value) => Math.Clamp(value, MinFontSize, MaxFontSize);

    public static bool TryParseFontSize(string? value, out double parsed)
    {
        parsed = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed) ||
               double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed);
    }

    public static string FormatFontSize(double value)
    {
        var clamped = ClampFontSize(value);
        return Math.Abs(clamped - Math.Round(clamped)) < 0.001
            ? Math.Round(clamped).ToString(CultureInfo.CurrentCulture)
            : clamped.ToString("0.##", CultureInfo.CurrentCulture);
    }
}
