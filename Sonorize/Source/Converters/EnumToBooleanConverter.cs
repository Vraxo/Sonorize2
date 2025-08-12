using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sonorize.Converters;

public class EnumToBooleanConverter : IValueConverter
{
    public static readonly EnumToBooleanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not null && parameter is not null && value.Equals(parameter);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter is typically used one-way for visibility.
        // If two-way binding is needed (e.g., for radio buttons), parameter would be the enum value to return.
        throw new NotSupportedException();
    }
}