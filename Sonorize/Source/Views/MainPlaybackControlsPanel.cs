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

        // Previous Button
        var previousButton = new Button
        {
            Content = "<", // Previous Track Symbol
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor, // Slightly less prominent border
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(0),
            FontSize = 16,
            FontWeight = FontWeight.Normal,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        previousButton.Bind(Button.CommandProperty, new Binding("Playback.PreviousTrackCommand"));
        previousButton.Bind(Button.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var mainPlayPauseButton = new Button
        {
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_AccentColor,
            BorderThickness = new Thickness(1),
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(19),
            Padding = new Thickness(0),
            FontSize = 18,
            FontWeight = FontWeight.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        var playPauseIconBinding = new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseIconConverter.Instance };
        mainPlayPauseButton.Bind(Button.ContentProperty, playPauseIconBinding);

        // Next Button
        var nextButton = new Button
        {
            Content = ">", // Next Track Symbol
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor, // Slightly less prominent border
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(0),
            FontSize = 16,
            FontWeight = FontWeight.Normal,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        nextButton.Bind(Button.CommandProperty, new Binding("Playback.NextTrackCommand"));
        nextButton.Bind(Button.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        // Group for playback control buttons (Prev, Play/Pause, Next)
        var playbackButtonControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        playbackButtonControlsPanel.Children.Add(previousButton);
        playbackButtonControlsPanel.Children.Add(mainPlayPauseButton);
        playbackButtonControlsPanel.Children.Add(nextButton);


        var toggleAdvPanelButton = new Button { Content = "+", Background = theme.B_SlightlyLighterBackground, Foreground = theme.B_TextColor, BorderBrush = theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), MinWidth = 30, FontWeight = FontWeight.Bold };
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        toggleAdvPanelButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var leftControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
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

        var sliderDockPanel = new DockPanel
        {
            LastChildFill = true,
            Height = 30
        };
        DockPanel.SetDock(leftControlsPanel, Dock.Left);
        DockPanel.SetDock(timeDisplayTextBlock, Dock.Right);

        sliderDockPanel.Children.Add(leftControlsPanel);
        sliderDockPanel.Children.Add(timeDisplayTextBlock);
        sliderDockPanel.Children.Add(mainPlaybackSlider);

        var topMainPlaybackControls = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10, 5, 10, 0),
            Spacing = 8
        };

        topMainPlaybackControls.Children.Add(playbackButtonControlsPanel); // Add the group of buttons
        topMainPlaybackControls.Children.Add(sliderDockPanel);

        var outerPanel = new StackPanel { Orientation = Orientation.Vertical, Background = theme.B_BackgroundColor, Margin = new Thickness(0, 5, 0, 5) };
        outerPanel.Children.Add(topMainPlaybackControls);
        return outerPanel;
    }
}