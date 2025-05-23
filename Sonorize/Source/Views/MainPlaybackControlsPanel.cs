using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.Views.MainWindowControls;

namespace Sonorize.Views.MainWindowControls;

public static class MainPlaybackControlsPanel
{
    public static StackPanel Create(ThemeColors theme)
    {
        var mainPlaybackSlider = new Slider
        {
            Name = "MainPlaybackSliderInstance",
            Minimum = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = theme.B_SecondaryTextColor,
            Foreground = theme.B_AccentColor
        };
        // Style to make the Thumb invisible
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>())
        {
            Setters =
            {
                new Setter(Thumb.WidthProperty, 0.0),
                new Setter(Thumb.HeightProperty, 0.0),
                new Setter(Thumb.OpacityProperty, 0.0)
            }
        });
        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var mainPlayPauseButton = new Button
        {
            // Content bound to icon converter
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_AccentColor,
            BorderThickness = new Thickness(1),
            Width = 38, // Circular button size
            Height = 38, // Circular button size
            CornerRadius = new CornerRadius(19), // Half of Width/Height for circular shape
            Padding = new Thickness(0), // Adjusted for icon centering
            FontSize = 18, // Adjusted for icon size, might need tweaking based on icon
            FontWeight = FontWeight.Normal, // Icons typically don't need bold
            HorizontalAlignment = HorizontalAlignment.Center, // Alignment of the button itself in its parent
            VerticalContentAlignment = VerticalAlignment.Center, // Ensure icon is centered vertically
            HorizontalContentAlignment = HorizontalAlignment.Center // Ensure icon is centered horizontally
        };
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        // Use the new BooleanToPlayPauseIconConverter
        var playPauseIconBinding = new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseIconConverter.Instance };
        mainPlayPauseButton.Bind(Button.ContentProperty, playPauseIconBinding);


        var toggleAdvPanelButton = new Button { Content = "+", Background = theme.B_SlightlyLighterBackground, Foreground = theme.B_TextColor, BorderBrush = theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), MinWidth = 30, FontWeight = FontWeight.Bold };
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        toggleAdvPanelButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        // This panel will now only contain the toggleAdvPanelButton, docked to the left of the slider
        var leftControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal, // Though it only has one child now
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        };
        leftControlsPanel.Children.Add(toggleAdvPanelButton);

        var timeDisplayTextBlock = new TextBlock
        {
            Foreground = theme.B_TextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 75
        };
        timeDisplayTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentTimeTotalTimeDisplay"));
        timeDisplayTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        // Panel for the slider and its side elements (toggle button, time display)
        var sliderDockPanel = new DockPanel
        {
            LastChildFill = true,
            Height = 30 // Height for this specific row
        };
        DockPanel.SetDock(leftControlsPanel, Dock.Left);
        DockPanel.SetDock(timeDisplayTextBlock, Dock.Right);

        sliderDockPanel.Children.Add(leftControlsPanel);
        sliderDockPanel.Children.Add(timeDisplayTextBlock);
        sliderDockPanel.Children.Add(mainPlaybackSlider); // Fills the center

        // Main container for the playback controls section, now a StackPanel for vertical arrangement
        var topMainPlaybackControls = new StackPanel
        {
            Orientation = Orientation.Vertical,
            // Removed fixed Height = 65, allowing panel to size to content
            Margin = new Thickness(10, 5, 10, 0), // Adjusted top margin slightly
            Spacing = 8 // Spacing between play button and slider panel, increased slightly
        };

        topMainPlaybackControls.Children.Add(mainPlayPauseButton); // Play button on top, centered
        topMainPlaybackControls.Children.Add(sliderDockPanel);   // Slider and its side controls below

        var outerPanel = new StackPanel { Orientation = Orientation.Vertical, Background = theme.B_BackgroundColor, Margin = new Thickness(0, 5, 0, 5) };
        outerPanel.Children.Add(topMainPlaybackControls);
        return outerPanel;
    }
}