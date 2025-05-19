using Avalonia.Data.Converters;
using Avalonia.Media; // Required for Color
using System;
using System.Globalization;

namespace Sonorize.Views;

// --- Dedicated converter for applying Alpha to a SolidColorBrush ---
// This replaces the incorrect usage of BrushExtensions.Multiply as a converter in the initial XAML attempt.
public class AccentBrushWithAlphaConverter : IValueConverter
{
    public static readonly AccentBrushWithAlphaConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ISolidColorBrush solidBrush && parameter is double alphaFactor)
        {
            // Use the color from the brush and apply the alpha factor to create a new brush
            var color = solidBrush.Color;
            byte newAlpha = (byte)(Math.Clamp(alphaFactor, 0.0, 1.0) * 255);
            return new SolidColorBrush(Color.FromArgb(newAlpha, color.R, color.G, color.B));
        }
        // Fallback if input is not a SolidColorBrush or parameter is not a double
        return new SolidColorBrush(Colors.Transparent); // Or a default semi-transparent color
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

// Removing the old BrushExtensions class as it was not used correctly in XAML and the new converter serves the purpose.