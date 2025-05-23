// Path: Source/Views/MainView.cs
using System;
using Avalonia.Controls;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sonorize.Converters;

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