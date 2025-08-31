using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sonorize.Converters;

public class OrBooleanConverter : IMultiValueConverter
{
    public static readonly OrBooleanConverter Instance = new();

    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        return values.Any(v => v is bool b && b);
    }
}