using System.Windows;
using System.Windows.Media;

namespace ColumnPadStudio.Services;

public static class ThemeResourceService
{
    public static void ApplyTheme(ResourceDictionary resources, string preset)
    {
        ArgumentNullException.ThrowIfNull(resources);

        if (string.Equals(preset, ThemePresetService.DarkPreset, StringComparison.Ordinal))
        {
            SetBrush(resources, "WindowBackgroundBrush", "#FF202020");
            SetBrush(resources, "MenuBackgroundBrush", "#FF2A2A2A");
            SetBrush(resources, "ToolbarBackgroundBrush", "#FF2A2F37");
            SetBrush(resources, "ControlForegroundBrush", "#FFF2F2F2");
            SetBrush(resources, "ControlBackgroundBrush", "#FF3A3A3A");
            SetBrush(resources, "ControlBorderBrush", "#FF6A6A6A");
            SetBrush(resources, "ControlHoverBackgroundBrush", "#FF454C56");
            SetBrush(resources, "ControlPressedBackgroundBrush", "#FF3A4452");
            SetBrush(resources, "ControlFocusBorderBrush", "#FF6FA1E0");
            SetDarkWorkflowNodeBrushes(resources);
            SetBrush(resources, "ControlPopupBackgroundBrush", "#FF2F2F2F");
            SetBrush(resources, "ControlPopupForegroundBrush", "#FFF2F2F2");
            SetBrush(resources, "ControlPopupHighlightBrush", "#FF3B6EA8");
            SetBrush(resources, "ControlPopupHighlightTextBrush", "#FFFFFFFF");
            SetBrush(resources, "ColumnHostBackgroundBrush", "#FF232323");
            SetBrush(resources, "ColumnHeaderBackgroundBrush", "#FF2D2D2D");
            SetBrush(resources, "ColumnSelectedHeaderBackgroundBrush", "#FF34404D");
            SetBrush(resources, "EditorBackgroundBrush", "#FF171717");
            SetBrush(resources, "EditorForegroundBrush", "#FFF2F2F2");
            SetBrush(resources, "EditorSelectionBrush", "#FF4A88CC");
            SetBrush(resources, "EditorSelectionTextBrush", "#FFFFFFFF");
            SetBrush(resources, "EditorInactiveSelectionBrush", "#FF385E8A");
            SetBrush(resources, "EditorInactiveSelectionTextBrush", "#FFFFFFFF");
            SetBrush(resources, "LinedPaperLineBrush", "#FF3D5E8A");
            SetBrush(resources, "LineNumberBackgroundBrush", "#FF222222");
            SetBrush(resources, "LineNumberForegroundBrush", "#FFB8B8B8");
            SetBrush(resources, "StatusBackgroundBrush", "#FF2A2A2A");
            SetBrush(resources, SystemColors.HighlightBrushKey, "#FF3B6EA8");
            SetBrush(resources, SystemColors.HighlightTextBrushKey, "#FFFFFFFF");
            SetBrush(resources, SystemColors.MenuHighlightBrushKey, "#FF3B6EA8");
            SetBrush(resources, SystemColors.HotTrackBrushKey, "#FF3B6EA8");
            SetBrush(resources, SystemColors.InactiveSelectionHighlightBrushKey, "#FF385E8A");
            SetBrush(resources, SystemColors.InactiveSelectionHighlightTextBrushKey, "#FFFFFFFF");
            SetBrush(resources, SystemColors.MenuBrushKey, "#FF2F2F2F");
            SetBrush(resources, SystemColors.MenuTextBrushKey, "#FFF2F2F2");
            SetBrush(resources, SystemColors.GrayTextBrushKey, "#FF9EA7B3");
            SetBrush(resources, SystemColors.ControlTextBrushKey, "#FFF2F2F2");
            SetBrush(resources, SystemColors.WindowBrushKey, "#FF2F2F2F");
            SetBrush(resources, SystemColors.WindowTextBrushKey, "#FFF2F2F2");
            SetBrush(resources, SystemColors.ControlBrushKey, "#FF3A3A3A");
            SetBrush(resources, SystemColors.InfoBrushKey, "#FF2F2F2F");
            SetBrush(resources, SystemColors.InfoTextBrushKey, "#FFF2F2F2");
            return;
        }

        if (string.Equals(preset, ThemePresetService.DefaultPreset, StringComparison.Ordinal))
        {
            SetBrush(resources, "WindowBackgroundBrush", "#FFEDEAE1");
            SetBrush(resources, "MenuBackgroundBrush", "#FFF2EFE6");
            SetBrush(resources, "ToolbarBackgroundBrush", "#FFE6E0D3");
            SetBrush(resources, "ControlForegroundBrush", "#FF1C1C1C");
            SetBrush(resources, "ControlBackgroundBrush", "#FFF8F3E8");
            SetBrush(resources, "ControlBorderBrush", "#FFC8BFAE");
            SetBrush(resources, "ControlHoverBackgroundBrush", "#FFFFFAEE");
            SetBrush(resources, "ControlPressedBackgroundBrush", "#FFE9DFC9");
            SetBrush(resources, "ControlFocusBorderBrush", "#FF2F5E94");
            SetDefaultWorkflowNodeBrushes(resources);
            SetBrush(resources, "ControlPopupBackgroundBrush", "#FFF8F3E8");
            SetBrush(resources, "ControlPopupForegroundBrush", "#FF1C1C1C");
            SetBrush(resources, "ControlPopupHighlightBrush", "#FFD8E5F4");
            SetBrush(resources, "ControlPopupHighlightTextBrush", "#FF1C1C1C");
            SetBrush(resources, "ColumnHostBackgroundBrush", "#FFF1EDE4");
            SetBrush(resources, "ColumnHeaderBackgroundBrush", "#FFD9D1C0");
            SetBrush(resources, "ColumnSelectedHeaderBackgroundBrush", "#FFE8DEC8");
            SetBrush(resources, "EditorBackgroundBrush", "#FFFFFCF4");
            SetBrush(resources, "EditorForegroundBrush", "#FF1C1C1C");
            SetBrush(resources, "EditorSelectionBrush", "#FFBECFE2");
            SetBrush(resources, "EditorSelectionTextBrush", "#FF1C1C1C");
            SetBrush(resources, "EditorInactiveSelectionBrush", "#FFD9E3EE");
            SetBrush(resources, "EditorInactiveSelectionTextBrush", "#FF1C1C1C");
            SetBrush(resources, "LinedPaperLineBrush", "#FF9DBFE8");
            SetBrush(resources, "LineNumberBackgroundBrush", "#FFEEE7D8");
            SetBrush(resources, "LineNumberForegroundBrush", "#FF7B7469");
            SetBrush(resources, "StatusBackgroundBrush", "#FFE8E2D5");
            SetBrush(resources, SystemColors.HighlightBrushKey, "#FFD8E5F4");
            SetBrush(resources, SystemColors.HighlightTextBrushKey, "#FF1C1C1C");
            SetBrush(resources, SystemColors.MenuHighlightBrushKey, "#FFD8E5F4");
            SetBrush(resources, SystemColors.HotTrackBrushKey, "#FF2B579A");
            SetBrush(resources, SystemColors.InactiveSelectionHighlightBrushKey, "#FFD9E3EE");
            SetBrush(resources, SystemColors.InactiveSelectionHighlightTextBrushKey, "#FF1C1C1C");
            SetBrush(resources, SystemColors.MenuBrushKey, "#FFF8F3E8");
            SetBrush(resources, SystemColors.MenuTextBrushKey, "#FF1C1C1C");
            SetBrush(resources, SystemColors.GrayTextBrushKey, "#FF7B7469");
            SetBrush(resources, SystemColors.ControlTextBrushKey, "#FF1C1C1C");
            SetBrush(resources, SystemColors.WindowBrushKey, "#FFF8F3E8");
            SetBrush(resources, SystemColors.WindowTextBrushKey, "#FF1C1C1C");
            SetBrush(resources, SystemColors.ControlBrushKey, "#FFF8F3E8");
            SetBrush(resources, SystemColors.InfoBrushKey, "#FFF8F3E8");
            SetBrush(resources, SystemColors.InfoTextBrushKey, "#FF1C1C1C");
            return;
        }

        SetBrush(resources, "WindowBackgroundBrush", "#FFEFEFEF");
        SetBrush(resources, "MenuBackgroundBrush", "#FFF5F5F5");
        SetBrush(resources, "ToolbarBackgroundBrush", "#FFE8EEF6");
        SetBrush(resources, "ControlForegroundBrush", "#FF111111");
        SetBrush(resources, "ControlBackgroundBrush", "#FFF4F4F4");
        SetBrush(resources, "ControlBorderBrush", "#FFB8B8B8");
        SetBrush(resources, "ControlHoverBackgroundBrush", "#FFFFFFFF");
        SetBrush(resources, "ControlPressedBackgroundBrush", "#FFDCE7F7");
        SetBrush(resources, "ControlFocusBorderBrush", "#FF2B579A");
        SetLightWorkflowNodeBrushes(resources);
        SetBrush(resources, "ControlPopupBackgroundBrush", "#FFF4F4F4");
        SetBrush(resources, "ControlPopupForegroundBrush", "#FF111111");
        SetBrush(resources, "ControlPopupHighlightBrush", "#FFDCE7F7");
        SetBrush(resources, "ControlPopupHighlightTextBrush", "#FF111111");
        SetBrush(resources, "ColumnHostBackgroundBrush", "#FFF2F2F2");
        SetBrush(resources, "ColumnHeaderBackgroundBrush", "#FFE4E4E4");
        SetBrush(resources, "ColumnSelectedHeaderBackgroundBrush", "#FFE8EEF6");
        SetBrush(resources, "EditorBackgroundBrush", "#FFFFFFFF");
        SetBrush(resources, "EditorForegroundBrush", "#FF111111");
        SetBrush(resources, "EditorSelectionBrush", "#FFB7D0F2");
        SetBrush(resources, "EditorSelectionTextBrush", "#FF111111");
        SetBrush(resources, "EditorInactiveSelectionBrush", "#FFD5E3F4");
        SetBrush(resources, "EditorInactiveSelectionTextBrush", "#FF111111");
        SetBrush(resources, "LinedPaperLineBrush", "#FFB5CFF2");
        SetBrush(resources, "LineNumberBackgroundBrush", "#FFF7F7F7");
        SetBrush(resources, "LineNumberForegroundBrush", "#FF7A7A7A");
        SetBrush(resources, "StatusBackgroundBrush", "#FFF3F3F3");
        SetBrush(resources, SystemColors.HighlightBrushKey, "#FFDCE7F7");
        SetBrush(resources, SystemColors.HighlightTextBrushKey, "#FF111111");
        SetBrush(resources, SystemColors.MenuHighlightBrushKey, "#FFDCE7F7");
        SetBrush(resources, SystemColors.HotTrackBrushKey, "#FF2B579A");
        SetBrush(resources, SystemColors.InactiveSelectionHighlightBrushKey, "#FFD5E3F4");
        SetBrush(resources, SystemColors.InactiveSelectionHighlightTextBrushKey, "#FF111111");
        SetBrush(resources, SystemColors.MenuBrushKey, "#FFF4F4F4");
        SetBrush(resources, SystemColors.MenuTextBrushKey, "#FF111111");
        SetBrush(resources, SystemColors.GrayTextBrushKey, "#FF7A7A7A");
        SetBrush(resources, SystemColors.ControlTextBrushKey, "#FF111111");
        SetBrush(resources, SystemColors.WindowBrushKey, "#FFF4F4F4");
        SetBrush(resources, SystemColors.WindowTextBrushKey, "#FF111111");
        SetBrush(resources, SystemColors.ControlBrushKey, "#FFF4F4F4");
        SetBrush(resources, SystemColors.InfoBrushKey, "#FFF4F4F4");
        SetBrush(resources, SystemColors.InfoTextBrushKey, "#FF111111");
    }

    private static void SetDarkWorkflowNodeBrushes(ResourceDictionary resources)
    {
        SetBrush(resources, "WorkflowNodeAutoBackgroundBrush", "#FF243850");
        SetBrush(resources, "WorkflowNodeAutoBorderBrush", "#FF7EA4CA");
        SetBrush(resources, "WorkflowNodeBlueBackgroundBrush", "#FF1F3C5B");
        SetBrush(resources, "WorkflowNodeBlueBorderBrush", "#FF72A7DC");
        SetBrush(resources, "WorkflowNodeGreenBackgroundBrush", "#FF213F2E");
        SetBrush(resources, "WorkflowNodeGreenBorderBrush", "#FF7AB385");
        SetBrush(resources, "WorkflowNodeAmberBackgroundBrush", "#FF4B3820");
        SetBrush(resources, "WorkflowNodeAmberBorderBrush", "#FFD29A52");
        SetBrush(resources, "WorkflowNodeRoseBackgroundBrush", "#FF4B2830");
        SetBrush(resources, "WorkflowNodeRoseBorderBrush", "#FFD3838F");
        SetBrush(resources, "WorkflowNodeSlateBackgroundBrush", "#FF303742");
        SetBrush(resources, "WorkflowNodeSlateBorderBrush", "#FF93A0AF");
    }

    private static void SetDefaultWorkflowNodeBrushes(ResourceDictionary resources)
    {
        SetBrush(resources, "WorkflowNodeAutoBackgroundBrush", "#FFF2E8D8");
        SetBrush(resources, "WorkflowNodeAutoBorderBrush", "#FFC59A69");
        SetBrush(resources, "WorkflowNodeBlueBackgroundBrush", "#FFE2ECF7");
        SetBrush(resources, "WorkflowNodeBlueBorderBrush", "#FF5D7FA8");
        SetBrush(resources, "WorkflowNodeGreenBackgroundBrush", "#FFE7F2DD");
        SetBrush(resources, "WorkflowNodeGreenBorderBrush", "#FF678E5B");
        SetBrush(resources, "WorkflowNodeAmberBackgroundBrush", "#FFFFF0D4");
        SetBrush(resources, "WorkflowNodeAmberBorderBrush", "#FFB78331");
        SetBrush(resources, "WorkflowNodeRoseBackgroundBrush", "#FFF8E2DE");
        SetBrush(resources, "WorkflowNodeRoseBorderBrush", "#FFB76A60");
        SetBrush(resources, "WorkflowNodeSlateBackgroundBrush", "#FFE9E1D4");
        SetBrush(resources, "WorkflowNodeSlateBorderBrush", "#FF877D71");
    }

    private static void SetLightWorkflowNodeBrushes(ResourceDictionary resources)
    {
        SetBrush(resources, "WorkflowNodeAutoBackgroundBrush", "#FFEAF3FF");
        SetBrush(resources, "WorkflowNodeAutoBorderBrush", "#FF7D8FA3");
        SetBrush(resources, "WorkflowNodeBlueBackgroundBrush", "#FFE7F0FF");
        SetBrush(resources, "WorkflowNodeBlueBorderBrush", "#FF5B7EAD");
        SetBrush(resources, "WorkflowNodeGreenBackgroundBrush", "#FFE7F8E7");
        SetBrush(resources, "WorkflowNodeGreenBorderBrush", "#FF4F8A4F");
        SetBrush(resources, "WorkflowNodeAmberBackgroundBrush", "#FFFFF6DB");
        SetBrush(resources, "WorkflowNodeAmberBorderBrush", "#FFB28A22");
        SetBrush(resources, "WorkflowNodeRoseBackgroundBrush", "#FFFDEAEA");
        SetBrush(resources, "WorkflowNodeRoseBorderBrush", "#FFB15A5A");
        SetBrush(resources, "WorkflowNodeSlateBackgroundBrush", "#FFF1F1F1");
        SetBrush(resources, "WorkflowNodeSlateBorderBrush", "#FF8A8A8A");
    }
    private static void SetBrush(ResourceDictionary resources, string key, string hex)
        => SetBrush(resources, (object)key, hex);

    private static void SetBrush(ResourceDictionary resources, object key, string hex)
    {
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
        if (brush.CanFreeze)
            brush.Freeze();

        resources[key] = brush;
    }
}
