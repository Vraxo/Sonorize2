using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Sonorize.Converters;

public class AlternatingRowBackgroundConverter : IMultiValueConverter
{
    // These are set when the converter is instantiated in the style
    public IBrush? DefaultBrush { get; set; }
    public IBrush? AlternateBrush { get; set; }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter now receives the item, its parent list, and the boolean setting.
        if (values.Count < 3 ||
            values[0] is not { } item ||
            values[1] is not IList list ||
            values[2] is not bool enableAlternating)
        {
            return DefaultBrush;
        }

        if (!enableAlternating)
        {
            return DefaultBrush;
        }

        int index = list.IndexOf(item);

        return (index != -1 && index % 2 == 1) ? AlternateBrush : DefaultBrush;
    }
}