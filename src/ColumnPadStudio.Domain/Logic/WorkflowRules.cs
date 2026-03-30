namespace ColumnPadStudio.Domain.Logic;

public static class WorkflowRules
{
    public const double DefaultNodeWidth = 168;
    public const double MinNodeWidth = 100;
    public const double MaxNodeWidth = 360;

    public static double ClampNodeWidth(double value)
        => Math.Clamp(value, MinNodeWidth, MaxNodeWidth);
}
