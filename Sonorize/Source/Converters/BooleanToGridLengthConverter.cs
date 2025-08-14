using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace Sonorize.Converters
{
    public class BooleanToGridLengthConverter : IValueConverter
    {
        public static readonly BooleanToGridLengthConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isVisible && isVisible)
            {
                if (parameter is GridLength length)
                {
                    return length;
                }
                if (parameter is string lengthString)
                {
                    return GridLength.Parse(lengthString);
                }
            }
            // If not visible, or parameter is wrong, collapse the column
            return new GridLength(0);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
