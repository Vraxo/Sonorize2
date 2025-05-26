using System;
using Avalonia;

namespace Sonorize.Converters;

public class SliderFillWidthConverter : Avalonia.Data.Converters.IMultiValueConverter
{
    public object Convert(System.Collections.Generic.IList<object> values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (values.Count == 3 &&
            values[0] is double value &&
            values[1] is double max &&
            values[2] is Rect bounds &&
            max > 0)
        {
            return bounds.Width * (value / max);
        }

        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}