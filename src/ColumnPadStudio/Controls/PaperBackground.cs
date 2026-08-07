using ColumnPadStudio.Models;
using System.Windows;
using System.Windows.Media;

namespace ColumnPadStudio.Controls;

public sealed class PaperBackground : FrameworkElement
{
    public static readonly DependencyProperty BaseBackgroundProperty = DependencyProperty.Register(
        nameof(BaseBackground),
        typeof(Brush),
        typeof(PaperBackground),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PatternBrushProperty = DependencyProperty.Register(
        nameof(PatternBrush),
        typeof(Brush),
        typeof(PaperBackground),
        new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsPaperEnabledProperty = DependencyProperty.Register(
        nameof(IsPaperEnabled),
        typeof(bool),
        typeof(PaperBackground),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty PaperStyleProperty = DependencyProperty.Register(
        nameof(PaperStyle),
        typeof(PaperStyle),
        typeof(PaperBackground),
        new FrameworkPropertyMetadata(PaperStyle.Ruled, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight),
        typeof(double),
        typeof(PaperBackground),
        new FrameworkPropertyMetadata(23.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.Register(
        nameof(VerticalOffset),
        typeof(double),
        typeof(PaperBackground),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public Brush BaseBackground
    {
        get => (Brush)GetValue(BaseBackgroundProperty);
        set => SetValue(BaseBackgroundProperty, value);
    }

    public Brush PatternBrush
    {
        get => (Brush)GetValue(PatternBrushProperty);
        set => SetValue(PatternBrushProperty, value);
    }

    public bool IsPaperEnabled
    {
        get => (bool)GetValue(IsPaperEnabledProperty);
        set => SetValue(IsPaperEnabledProperty, value);
    }

    public PaperStyle PaperStyle
    {
        get => (PaperStyle)GetValue(PaperStyleProperty);
        set => SetValue(PaperStyleProperty, value);
    }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(BaseBackground, null, new Rect(RenderSize));

        if (!IsPaperEnabled || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var spacing = double.IsFinite(LineHeight) ? Math.Max(8.0, LineHeight) : 23.0;
        var style = Enum.IsDefined(PaperStyle) ? PaperStyle : PaperStyle.Ruled;
        var verticalOffset = double.IsFinite(VerticalOffset) ? Math.Max(0.0, VerticalOffset) : 0.0;
        var pen = new Pen(PatternBrush, style == PaperStyle.StrongRuled ? 2.0 : 1.0);

        if (style == PaperStyle.SoftRuled)
            drawingContext.PushOpacity(0.55);

        DrawRuledLines(drawingContext, pen, spacing, verticalOffset);

        if (style == PaperStyle.SoftRuled)
            drawingContext.Pop();
    }

    private void DrawRuledLines(DrawingContext drawingContext, Pen pen, double spacing, double verticalOffset)
    {
        var firstLineY = spacing - (verticalOffset % spacing) - 0.5;
        if (firstLineY < 0)
            firstLineY += spacing;

        for (var y = firstLineY; y < ActualHeight; y += spacing)
            drawingContext.DrawLine(pen, new Point(0, y), new Point(ActualWidth, y));
    }
}
