using ColumnPadStudio.Domain.Workspaces;

namespace ColumnPadStudio.Models;

public sealed record AppPreferences(
    string ThemePreset = "Default Mode",
    bool SnapAllColumnsEnabled = true,
    int ColumnSpacingPx = 4,
    bool FitColumnsToWindow = false,
    int DefaultColumnWidthPx = (int)WorkspaceConstraints.DefaultColumnWidth)
{
    public const int MinimumColumnSpacingPx = 0;
    public const int MaximumColumnSpacingPx = 200;
    public const int DefaultColumnSpacingPx = 4;
    public const int StandardColumnWidthPx = (int)WorkspaceConstraints.DefaultColumnWidth;

    public static int NormalizeColumnSpacing(int value)
    {
        return Math.Clamp(value, MinimumColumnSpacingPx, MaximumColumnSpacingPx);
    }

    public static int NormalizeDefaultColumnWidth(int value)
    {
        return WorkspaceConstraints.ClampColumnWidth(value);
    }
}
