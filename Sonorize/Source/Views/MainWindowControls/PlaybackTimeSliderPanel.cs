using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Sonorize;

public static class PlaybackTimeSliderPanel
{
    public static Control Create()
    {
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = 25,
            Height = 30,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(20),
            Background = Brushes.LightGray,
            Foreground = Brushes.SteelBlue
        };

        slider.Template = new FuncControlTemplate<Slider>((s, _) =>
        {
            var fill = new Border
            {
                Background = s.Foreground,
                Height = 4,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var background = new Border
            {
                Background = s.Background,
                Height = 4,
                VerticalAlignment = VerticalAlignment.Center
            };

            var thumb = new Thumb
            {
                Width = 14,
                Height = 14,
                CornerRadius = new CornerRadius(7),
                Background = Brushes.White,
                BorderBrush = Brushes.DarkGray,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };

            var track = new Track
            {
                Name = "PART_Track",
                Orientation = Orientation.Horizontal,
                Thumb = thumb,
                Minimum = s.Minimum,
                Maximum = s.Maximum,
                Value = s.Value
            };

            track.Bind(Track.MinimumProperty, s[!Slider.MinimumProperty]);
            track.Bind(Track.MaximumProperty, s[!Slider.MaximumProperty]);
            track.Bind(Track.ValueProperty, s[!Slider.ValueProperty]);

            // Bind fill width to value/max * total width
            fill.Bind(Border.WidthProperty, new MultiBinding
            {
                Converter = new SliderFillWidthConverter(),
                Bindings =
                {
                    s[!Slider.ValueProperty],
                    s[!Slider.MaximumProperty],
                    s[!Slider.BoundsProperty]
                }
            });

            return new Grid
            {
                Children =
                {
                    background,
                    fill,
                    track
                }
            };
        });

        return new StackPanel
        {
            Children =
            {
                new TextBlock
                {
                    Text = "Working Slider",
                    Margin = new Thickness(20),
                    FontWeight = FontWeight.Bold
                },
                slider
            }
        };
    }
}

public class SliderFillWidthConverter : IMultiValueConverter
{
    public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Count == 3 &&
            values[0] is double value &&
            values[1] is double max &&
            values[2] is Rect bounds &&
            max > 0)
        {
            return bounds.Width * (value / max);
        }

        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
