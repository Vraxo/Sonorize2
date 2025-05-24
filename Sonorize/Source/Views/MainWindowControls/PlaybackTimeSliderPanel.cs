using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Models; // For ThemeColors

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
            Background = theme.B_SecondaryTextColor,
            Foreground = theme.B_AccentColor,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>())
        {
            Setters =
            {
                new Setter(Thumb.WidthProperty, 0.0),
                new Setter(Thumb.HeightProperty, 0.0),
                new Setter(Thumb.OpacityProperty, 0.0) // Hide the thumb visually
            }
        });
        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var timeSliderGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center,
            Height = 30,
            MinWidth = 500,
            HorizontalAlignment = HorizontalAlignment.Stretch
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