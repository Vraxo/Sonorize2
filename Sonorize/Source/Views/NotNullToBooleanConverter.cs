using Avalonia.Data.Converters;
using System;

namespace Sonorize.Views;

public class NotNullToBooleanConverter : IValueConverter
{
    public static readonly NotNullToBooleanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

// Removing the old BrushExtensions class as it was not used correctly in XAML and the new converter serves the purpose.