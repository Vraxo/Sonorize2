using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sonorize.Converters;

public class EnumToBooleanConverter : IValueConverter
{
    public static readonly EnumToBooleanConverter Instance = new();

    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool result = value is not null && parameter is not null && value.Equals(parameter);
        return Invert ? !result : result;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter is typically used one-way for visibility.
        // If two-way binding is needed (e.g., for radio buttons), parameter would be the enum value to return.
        throw new NotSupportedException();
    }
}