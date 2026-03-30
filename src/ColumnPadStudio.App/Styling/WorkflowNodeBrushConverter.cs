using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ColumnPadStudio.Domain.Models;

namespace ColumnPadStudio.App.Styling;

public sealed class WorkflowNodeBrushConverter : IValueConverter
{
    public bool UseStrokeBrush { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not WorkflowNode node)
        {
            return Brushes.Transparent;
        }

        var palette = WorkflowNodePalette.GetBrushKeys(node);
        var key = UseStrokeBrush ? palette.StrokeKey : palette.FillKey;
        return System.Windows.Application.Current.Resources[key] as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
