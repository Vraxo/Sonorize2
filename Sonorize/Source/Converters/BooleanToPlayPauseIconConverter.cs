// Path: Source/Converters/BooleanToPlayPauseIconConverter.cs
using System;
using Avalonia.Data.Converters;
using System.Globalization;

namespace Sonorize.Converters
{
    public class BooleanToPlayPauseIconConverter : IValueConverter
    {
        public static readonly BooleanToPlayPauseIconConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isPlaying)
            {
                // Play: U+25B6 (▶ BLACK RIGHT-POINTING TRIANGLE)
                // Pause: "||" (two pipe characters)
                return isPlaying ? "| |" : "\u25B6";
            }
            return "\u25B6"; // Default to Play icon
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}