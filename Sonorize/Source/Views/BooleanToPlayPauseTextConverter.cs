using Avalonia.Data.Converters;
using System;

namespace Sonorize.Views;

// --- Converters (moved outside the MainWindow partial class) ---
// Make converters public so they can be referenced in XAML resources using just the namespace prefix

public class BooleanToPlayPauseTextConverter : IValueConverter
{
    public static readonly BooleanToPlayPauseTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isPlaying) return isPlaying ? "Pause" : "Play";
        return "Play";
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

// Removing the old BrushExtensions class as it was not used correctly in XAML and the new converter serves the purpose.