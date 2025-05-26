using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Sonorize.Converters
{
    public class BooleanToPlayPauseIconConverter : IValueConverter
    {
        public static readonly BooleanToPlayPauseIconConverter Instance = new();

        private const string PlayIcon = "▶"; // U+25B6
        private const string PauseIcon = "||"; // U+2016 (Double Vertical Line)

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isPlaying)
            {
                return isPlaying ? PauseIcon : PlayIcon;
            }
            return PlayIcon; // Default to Play icon if value is not a bool
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}