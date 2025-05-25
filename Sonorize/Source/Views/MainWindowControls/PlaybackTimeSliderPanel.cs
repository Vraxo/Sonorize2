using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb
using Avalonia.Controls.Templates;  // For FuncControlTemplate
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
            Background = theme.B_SecondaryTextColor, // Inactive part of the track
            Foreground = theme.B_AccentColor,     // Active part of the track
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 32,                          // Overall height of the slider control
            MinHeight = 32,                       // Ensure it requests this minimum height
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(8),   // Rounded ends for the overall slider control
            RenderTransform = new TranslateTransform(0, -3) // Shift slider up by 3 pixels
        };

        // Override the theme resource for track height specifically for this slider
        mainPlaybackSlider.Resources["SliderTrackThemeHeight"] = 6.0;

        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>())
        {
            Setters =
            {
                new Setter(Thumb.WidthProperty, 4.0),
                new Setter(Thumb.HeightProperty, 4.0),
                new Setter(Thumb.CornerRadiusProperty, new CornerRadius(0)), // Ensure Thumb's own CornerRadius is 0
                // Provide a ControlTemplate for the Thumb that respects its CornerRadius.
                new Setter(Thumb.TemplateProperty, new FuncControlTemplate<Thumb>((templatedParent, templatedNamescope) =>
                {
                    return new Border
                    {
                        // Bind to the Thumb's properties for Background, BorderBrush, BorderThickness.
                        [!Border.BackgroundProperty] = templatedParent[!Thumb.BackgroundProperty],
                        [!Border.BorderBrushProperty] = templatedParent[!Thumb.BorderBrushProperty],
                        [!Border.BorderThicknessProperty] = templatedParent[!Thumb.BorderThicknessProperty],
                        // Ensure this Border's CornerRadius is bound to the Thumb's CornerRadius.
                        [!Border.CornerRadiusProperty] = templatedParent[!Thumb.CornerRadiusProperty]
                    };
                })),
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
            Height = 32,
            MinWidth = 500,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = false // Allow child transforms to potentially render outside bounds if necessary (though ideally it stays within)
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