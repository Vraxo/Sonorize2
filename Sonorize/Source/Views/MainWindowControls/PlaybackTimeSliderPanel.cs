using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Models;

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
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 32,
            MinHeight = 32,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8),
            RenderTransform = new TranslateTransform(0, -3)
        };

        mainPlaybackSlider.Resources["SliderTrackThemeHeight"] = 6.0;

        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>())
        {
            Setters =
            {
                new Setter(Thumb.WidthProperty, 16.0),
                new Setter(Thumb.HeightProperty, 16.0),
                new Setter(Thumb.BackgroundProperty, theme.B_AccentColor),
                new Setter(Thumb.CornerRadiusProperty, new CornerRadius(8)),
                new Setter(Thumb.BorderThicknessProperty, new Thickness(1)),
                new Setter(Thumb.BorderBrushProperty, theme.B_SecondaryTextColor),
                new Setter(Thumb.TemplateProperty, new FuncControlTemplate<Thumb>((templatedParent, templatedNamescope) =>
                {
                    return new Border
                    {
                        [!Border.WidthProperty] = templatedParent[!Thumb.WidthProperty],
                        [!Border.HeightProperty] = templatedParent[!Thumb.HeightProperty],
                        [!Border.BackgroundProperty] = templatedParent[!Thumb.BackgroundProperty],
                        [!Border.BorderBrushProperty] = templatedParent[!Thumb.BorderBrushProperty],
                        [!Border.BorderThicknessProperty] = templatedParent[!Thumb.BorderThicknessProperty],
                        [!Border.CornerRadiusProperty] = templatedParent[!Thumb.CornerRadiusProperty],
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                }))
            }
        });

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