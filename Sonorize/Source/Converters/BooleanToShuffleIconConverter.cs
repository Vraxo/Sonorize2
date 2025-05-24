using System;
using Avalonia.Data.Converters;
using System.Globalization;
using System.Diagnostics; // Added for Debug

namespace Sonorize.Converters
{
    public class BooleanToShuffleIconConverter : IValueConverter
    {
        public static readonly BooleanToShuffleIconConverter Instance = new();

        private const string ShuffleActiveIcon = "🔀"; // U+1F500 Twisted Arrows Right and Left
        private const string ShuffleInactiveIcon = "↔"; // U+2194 Left Right Arrow

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // Add debug logging here
            Debug.WriteLine($"[Converter] BooleanToShuffleIconConverter Convert called. Value: {value}, TargetType: {targetType}");
            if (value is bool isShuffleEnabled)
            {
                Debug.WriteLine($"[Converter] isShuffleEnabled: {isShuffleEnabled}. Returning: {(isShuffleEnabled ? ShuffleActiveIcon : ShuffleInactiveIcon)}");
                return isShuffleEnabled ? ShuffleActiveIcon : ShuffleInactiveIcon;
            }
            Debug.WriteLine($"[Converter] Value is not bool ({value?.GetType().Name ?? "null"}). Returning: {ShuffleInactiveIcon}");
            return ShuffleInactiveIcon; // Default
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}