using System;
using Avalonia.Data.Converters;
using System.Globalization;

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

    public class InverseBooleanConverter : IValueConverter
    {
        public object? Convert(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return value; // Or throw, depending on desired strictness
        }

        public object? ConvertBack(object? value, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
            {
                return !b;
            }
            return value; // Or throw
        }
    }

    public class AndBooleanConverter : IMultiValueConverter
    {
        public object? Convert(System.Collections.Generic.IList<object?> values, System.Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            bool result = true;
            foreach (var value in values)
            {
                if (value is bool b)
                {
                    result &= b;
                }
                else
                {
                    // If any value is not a boolean, result is false
                    return false; // Or true, depending on how non-booleans should be handled
                }
            }
            return result;
        }

        public object[] ConvertBack(object? value, System.Type[] targetTypes, object? parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotSupportedException();
        }
    }