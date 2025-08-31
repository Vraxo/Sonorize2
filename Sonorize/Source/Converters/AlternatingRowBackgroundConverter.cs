using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Sonorize.Models; // Required for Song model

namespace Sonorize.Converters;

public class AlternatingRowBackgroundConverter : IMultiValueConverter
{
    // These are set when the converter is instantiated in the style
    public IBrush? DefaultBrush { get; set; }
    public IBrush? AlternateBrush { get; set; }

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        // This converter now receives the Song object and the boolean setting.
        if (values.Count < 2 ||
            values[0] is not Song song ||
            values[1] is not bool enableAlternating)
        {
            return DefaultBrush;
        }

        if (!enableAlternating)
        {
            return DefaultBrush;
        }

        // Use the pre-calculated index from the view model for high performance.
        return (song.IndexInView % 2 == 1) ? AlternateBrush : DefaultBrush;
    }
}