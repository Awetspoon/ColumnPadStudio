using ColumnPadStudio.Domain.Enums;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.App.Styling;

internal static class WorkflowNodePalette
{
    private static readonly WorkflowNodeColor[] AvailableColorsInternal =
    [
        WorkflowNodeColor.Automatic,
        WorkflowNodeColor.Blue,
        WorkflowNodeColor.Green,
        WorkflowNodeColor.Orange,
        WorkflowNodeColor.Purple,
        WorkflowNodeColor.Red,
        WorkflowNodeColor.Slate
    ];

    public static IReadOnlyList<WorkflowNodeColor> AvailableColors => AvailableColorsInternal;

    public static (string FillKey, string StrokeKey) GetBrushKeys(WorkflowNode node)
    {
        return node.Color switch
        {
            WorkflowNodeColor.Blue => ("WorkflowBlueFillBrush", "WorkflowBlueStrokeBrush"),
            WorkflowNodeColor.Green => ("WorkflowGreenFillBrush", "WorkflowGreenStrokeBrush"),
            WorkflowNodeColor.Orange => ("WorkflowOrangeFillBrush", "WorkflowOrangeStrokeBrush"),
            WorkflowNodeColor.Purple => ("WorkflowPurpleFillBrush", "WorkflowPurpleStrokeBrush"),
            WorkflowNodeColor.Red => ("WorkflowRedFillBrush", "WorkflowRedStrokeBrush"),
            WorkflowNodeColor.Slate => ("WorkflowSlateFillBrush", "WorkflowSlateStrokeBrush"),
            _ => node.Kind switch
            {
                WorkflowNodeKind.Start => ("WorkflowStartFillBrush", "WorkflowStartStrokeBrush"),
                WorkflowNodeKind.Step => ("WorkflowStepFillBrush", "WorkflowStepStrokeBrush"),
                WorkflowNodeKind.Decision => ("WorkflowDecisionFillBrush", "WorkflowDecisionStrokeBrush"),
                WorkflowNodeKind.End => ("WorkflowEndFillBrush", "WorkflowEndStrokeBrush"),
                WorkflowNodeKind.Note => ("WorkflowNoteFillBrush", "WorkflowNoteStrokeBrush"),
                _ => ("WorkflowStepFillBrush", "WorkflowStepStrokeBrush")
            }
        };
    }

    public static string GetDisplayName(WorkflowNodeColor color)
    {
        return color switch
        {
            WorkflowNodeColor.Automatic => "Automatic",
            WorkflowNodeColor.Blue => "Blue",
            WorkflowNodeColor.Green => "Green",
            WorkflowNodeColor.Orange => "Orange",
            WorkflowNodeColor.Purple => "Purple",
            WorkflowNodeColor.Red => "Red",
            WorkflowNodeColor.Slate => "Slate",
            _ => color.ToString()
        };
    }
}
