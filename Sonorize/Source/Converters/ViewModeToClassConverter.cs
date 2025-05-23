using Avalonia.Data.Converters;
using System;
using System.Globalization;
using Sonorize.ViewModels;
using Avalonia; // Assuming LibraryViewMode is in ViewModels

namespace Sonorize.Converters;

public class ViewModeToClassConverter : IValueConverter
{
    public static readonly ViewModeToClassConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LibraryViewMode viewMode && targetType == typeof(string))
        {
            // Return the class name if in Grid view, otherwise return null to remove the class
            return viewMode == LibraryViewMode.Grid ? "grid-view" : null;
        }
        return null; // Or AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // ConvertBack is not needed for this converter
        return AvaloniaProperty.UnsetValue;
    }
}