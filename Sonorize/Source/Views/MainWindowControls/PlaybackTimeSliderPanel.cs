using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb, Track
using Avalonia.Controls.Templates;  // For FuncControlTemplate
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Models; // For ThemeColors
using System;

namespace Sonorize.Views.MainWindowControls;

public static class PlaybackTimeSliderPanel
{
    public static Grid Create(ThemeColors theme)
    {
        // TextBlock for Current Time
        var currentTimeTextBlock = new TextBlock
        {
            Foreground = theme.B_SecondaryTextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            MinWidth = 40,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        currentTimeTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentTimeDisplay"));
        currentTimeTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        // TextBlock for Total Time
        var totalTimeTextBlock = new TextBlock
        {
            Foreground = theme.B_SecondaryTextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0),
            MinWidth = 40,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        totalTimeTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.TotalTimeDisplay"));
        totalTimeTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        var mainPlaybackSlider = new Slider
        {
            Name = "MainPlaybackSliderInstance",
            Minimum = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = theme.B_SecondaryTextColor, // Inactive part of the track
            Foreground = theme.B_AccentColor,        // Active part of the track
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 32,                             // Overall height of the slider control
            MinHeight = 32,                          // Ensure it requests this minimum height
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8),      // Rounded ends for the overall slider control
            RenderTransform = new TranslateTransform(0, -3) // Shift slider up by 3 pixels
        };

        // Override the theme resource for track height specifically for this slider
        mainPlaybackSlider.Resources["SliderTrackThemeHeight"] = 6.0;

        // Slider Template - thumb invisible but interactive
        mainPlaybackSlider.Template = new FuncControlTemplate<Slider>((slider, _) =>
        {
            var backgroundPart = new Border
            {
                Background = slider.Background,
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center,
                Height = 8
            };

            var filledPart = new Border
            {
                Background = slider.Foreground,
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center,
                Height = 8
            };

            var thumb = new Thumb
            {
                Width = 4,
                Height = 6,
                Opacity = 0,
                IsHitTestVisible = true
            };

            var track = new Track
            {
                Name = "PART_Track",
                Orientation = Orientation.Horizontal,
                Thumb = thumb
            };

            track.Bind(Track.MinimumProperty, slider[!Slider.MinimumProperty]);
            track.Bind(Track.MaximumProperty, slider[!Slider.MaximumProperty]);
            track.Bind(Track.ValueProperty, slider[!Slider.ValueProperty]);

            var container = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
                Height = 6,
                Children =
                {
                    backgroundPart,
                    new Grid
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        Children = { filledPart }
                    },
                    track
                }
            };

            // Bind filledPart.Width to proportional slider value
            filledPart.Bind(Border.WidthProperty, new MultiBinding
            {
                Converter = new SliderFillWidthConverter(),
                Bindings =
                {
                    slider[!Slider.ValueProperty],
                    slider[!Slider.MaximumProperty],
                    slider[!Slider.BoundsProperty]
                }
            });

            return container;
        });

        // HERE IS THE CRUCIAL CHANGE:
        // Handle PointerPressed on the slider itself (the whole clickable area)
        mainPlaybackSlider.PointerPressed += (sender, e) =>
        {
            var s = (Slider)sender;
            var pos = e.GetPosition(s);
            var bounds = s.Bounds;

            if (bounds.Width > 0)
            {
                // Calculate ratio (clamped 0 to 1)
                double ratio = Math.Clamp(pos.X / bounds.Width, 0, 1);

                // Calculate new slider value based on ratio
                double newValue = s.Minimum + ratio * (s.Maximum - s.Minimum);

                s.Value = newValue;
                e.Handled = true;
            }
        };

        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var timeSliderGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center,
            Height = 32,
            MinWidth = 500,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = false
        };

        Grid.SetColumn(currentTimeTextBlock, 0);
        Grid.SetColumn(mainPlaybackSlider, 1);
        Grid.SetColumn(totalTimeTextBlock, 2);

        timeSliderGrid.Children.Add(currentTimeTextBlock);
        timeSliderGrid.Children.Add(mainPlaybackSlider);
        timeSliderGrid.Children.Add(totalTimeTextBlock);

        return timeSliderGrid;
    }
}

// Converter for binding filled track width
public class SliderFillWidthConverter : Avalonia.Data.Converters.IMultiValueConverter
{
    public object Convert(System.Collections.Generic.IList<object> values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
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

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
