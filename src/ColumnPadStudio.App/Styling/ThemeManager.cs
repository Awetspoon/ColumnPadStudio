using System.Windows.Media;
using ColumnPadStudio.Domain.Enums;

namespace ColumnPadStudio.App.Styling;

public static class ThemeManager
{
    public static void ApplyTheme(ThemePreset preset)
    {
        var app = System.Windows.Application.Current;
        if (app is null)
        {
            return;
        }

        var palette = preset switch
        {
            ThemePreset.Light => new ThemePalette(
                "#FFFFFF", "#FFFFFF", "#F4F7FA", "#FFFFFF", "#EEF4FA", "#FFFFFF", "#EAF2FF", "#1668DC", "#FFFFFF", "#CCD6E0", "#F4F7FA", "#1668DC", "#111111", "#566270", "#8A949F", "#FFFFFF", "#E4EAF0", "#111111", "#F7F9FC",
                "#4F8BD6", "#111111", "#566270", "#E8F2FF", "#5D90DA", "#F2F5F8", "#99A7B6", "#F4EDFF", "#A487DE", "#EAF5EC", "#6FAE7D", "#FFF6E4", "#D3A54A"),
            ThemePreset.Dark => new ThemePalette(
                "#050709", "#0A0D10", "#11161B", "#0E1318", "#172029", "#0D1116", "#18222D", "#3D8CFF", "#FFFFFF", "#283240", "#101820", "#57A0FF", "#FFFFFF", "#BBC3CC", "#727D87", "#060B10", "#1A2430", "#FFFFFF", "#090E13",
                "#6BA8E8", "#FFFFFF", "#D0D7DE", "#12243A", "#5EA3FF", "#161C24", "#506074", "#1B1730", "#8068BF", "#142119", "#4D8B5A", "#261E10", "#B48A32"),
            _ => new ThemePalette(
                "#162132", "#22384E", "#2E4760", "#2D455D", "#395873", "#253C52", "#3A5A74", "#DA9663", "#1E2834", "#9D9C99", "#273B51", "#DA9663", "#F6F1E7", "#D9CCBA", "#9A948B", "#142435", "#35506A", "#F6F1E7", "#102030",
                "#89A7C5", "#F6F1E7", "#D9CCBA", "#273B51", "#9D9C99", "#314A63", "#8BA2B6", "#4B3A49", "#C2A4B7", "#294034", "#88AE94", "#4A3723", "#DA9663")
        };

        SetBrush(app, "AppBackgroundBrush", palette.AppBackground);
        SetBrush(app, "SurfaceBrush", palette.Surface);
        SetBrush(app, "SurfaceAltBrush", palette.SurfaceAlt);
        SetBrush(app, "ControlSurfaceBrush", palette.ControlSurface);
        SetBrush(app, "ControlSurfaceHoverBrush", palette.ControlSurfaceHover);
        SetBrush(app, "PopupSurfaceBrush", palette.PopupSurface);
        SetBrush(app, "PopupHoverBrush", palette.PopupHover);
        SetBrush(app, "PopupSelectedBrush", palette.PopupSelected);
        SetBrush(app, "PopupSelectedForegroundBrush", palette.PopupSelectedForeground);
        SetBrush(app, "BorderBrushStrong", palette.BorderStrong);
        SetBrush(app, "HeaderBrush", palette.Header);
        SetBrush(app, "AccentBrush", palette.Accent);
        SetBrush(app, "ForegroundBrush", palette.Foreground);
        SetBrush(app, "ForegroundMutedBrush", palette.ForegroundMuted);
        SetBrush(app, "DisabledForegroundBrush", palette.DisabledForeground);
        SetBrush(app, "EditorPaperBrush", palette.EditorPaper);
        SetBrush(app, "EditorLineBrush", palette.EditorLine);
        SetBrush(app, "EditorTextBrush", palette.EditorText);
        SetBrush(app, "GutterBrush", palette.Gutter);
        SetBrush(app, "WorkflowLinkBrush", palette.WorkflowLink);
        SetBrush(app, "WorkflowNodeForegroundBrush", palette.WorkflowNodeForeground);
        SetBrush(app, "WorkflowNodeMutedForegroundBrush", palette.WorkflowNodeMutedForeground);
        SetBrush(app, "WorkflowStartFillBrush", palette.WorkflowStartFill);
        SetBrush(app, "WorkflowStartStrokeBrush", palette.WorkflowStartStroke);
        SetBrush(app, "WorkflowStepFillBrush", palette.WorkflowStepFill);
        SetBrush(app, "WorkflowStepStrokeBrush", palette.WorkflowStepStroke);
        SetBrush(app, "WorkflowDecisionFillBrush", palette.WorkflowDecisionFill);
        SetBrush(app, "WorkflowDecisionStrokeBrush", palette.WorkflowDecisionStroke);
        SetBrush(app, "WorkflowEndFillBrush", palette.WorkflowEndFill);
        SetBrush(app, "WorkflowEndStrokeBrush", palette.WorkflowEndStroke);
        SetBrush(app, "WorkflowNoteFillBrush", palette.WorkflowNoteFill);
        SetBrush(app, "WorkflowNoteStrokeBrush", palette.WorkflowNoteStroke);
        SetBrush(app, System.Windows.SystemColors.WindowBrushKey, palette.ControlSurface);
        SetBrush(app, System.Windows.SystemColors.WindowTextBrushKey, palette.Foreground);
        SetBrush(app, System.Windows.SystemColors.ControlBrushKey, palette.Surface);
        SetBrush(app, System.Windows.SystemColors.ControlTextBrushKey, palette.Foreground);
        SetBrush(app, System.Windows.SystemColors.MenuBrushKey, palette.PopupSurface);
        SetBrush(app, System.Windows.SystemColors.MenuTextBrushKey, palette.Foreground);
        SetBrush(app, System.Windows.SystemColors.HighlightBrushKey, palette.PopupSelected);
        SetBrush(app, System.Windows.SystemColors.HighlightTextBrushKey, palette.PopupSelectedForeground);
        SetBrush(app, System.Windows.SystemColors.InactiveSelectionHighlightBrushKey, palette.PopupHover);
        SetBrush(app, System.Windows.SystemColors.InactiveSelectionHighlightTextBrushKey, palette.Foreground);
        SetBrush(app, System.Windows.SystemColors.GrayTextBrushKey, palette.DisabledForeground);
        SetBrush(app, System.Windows.SystemColors.HotTrackBrushKey, palette.Accent);
    }

    private static void SetBrush(System.Windows.Application app, object key, string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex)!;
        app.Resources[key] = new SolidColorBrush(color);
    }

    private readonly record struct ThemePalette(
        string AppBackground,
        string Surface,
        string SurfaceAlt,
        string ControlSurface,
        string ControlSurfaceHover,
        string PopupSurface,
        string PopupHover,
        string PopupSelected,
        string PopupSelectedForeground,
        string BorderStrong,
        string Header,
        string Accent,
        string Foreground,
        string ForegroundMuted,
        string DisabledForeground,
        string EditorPaper,
        string EditorLine,
        string EditorText,
        string Gutter,
        string WorkflowLink,
        string WorkflowNodeForeground,
        string WorkflowNodeMutedForeground,
        string WorkflowStartFill,
        string WorkflowStartStroke,
        string WorkflowStepFill,
        string WorkflowStepStroke,
        string WorkflowDecisionFill,
        string WorkflowDecisionStroke,
        string WorkflowEndFill,
        string WorkflowEndStroke,
        string WorkflowNoteFill,
        string WorkflowNoteStroke);
}
