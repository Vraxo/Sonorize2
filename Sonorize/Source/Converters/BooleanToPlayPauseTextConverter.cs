// Path: Source/Views/MainView.cs
using System;
using Avalonia.Data.Converters;

namespace Sonorize.Converters;

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
