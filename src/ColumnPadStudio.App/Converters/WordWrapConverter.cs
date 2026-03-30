using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ColumnPadStudio.App.Converters;

public sealed class WordWrapConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? TextWrapping.Wrap : TextWrapping.NoWrap;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is TextWrapping wrapping && wrapping == TextWrapping.Wrap;
}
