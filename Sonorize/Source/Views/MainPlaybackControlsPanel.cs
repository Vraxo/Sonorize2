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
        // Previous Button
        var previousButton = new Button
        {
            Content = "<",
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(0),
            FontSize = 16,
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
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        mainPlayPauseButton.Bind(Button.ContentProperty, new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseIconConverter.Instance });

        var nextButton = new Button
        {
            Content = ">",
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(0),
            FontSize = 16,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        nextButton.Bind(Button.CommandProperty, new Binding("Playback.NextTrackCommand"));
        nextButton.Bind(Button.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var playbackButtonControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        playbackButtonControlsPanel.Children.Add(previousButton);
        playbackButtonControlsPanel.Children.Add(mainPlayPauseButton);
        playbackButtonControlsPanel.Children.Add(nextButton);

        var toggleAdvPanelButton = new Button
        {
            Content = "+",
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_AccentColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 4),
            MinWidth = 30,
            FontWeight = FontWeight.Bold
        };
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

        var mainPlaybackSlider = new Slider
        {
            Name = "MainPlaybackSliderInstance",
            Minimum = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = theme.B_SecondaryTextColor,
            Foreground = theme.B_AccentColor,
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 0.5 * 800 // 50% of an assumed max width, replace with a binding or measurement logic if dynamic
        };
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

        // Container to center and limit slider width
        var sliderContainer = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Width = 400 // 50% of 800px, or adjust as needed
        };
        sliderContainer.Children.Add(mainPlaybackSlider);

        var sliderDockPanel = new DockPanel
        {
            LastChildFill = true,
            Height = 30
        };
        DockPanel.SetDock(leftControlsPanel, Dock.Left);
        DockPanel.SetDock(timeDisplayTextBlock, Dock.Right);

        sliderDockPanel.Children.Add(leftControlsPanel);
        sliderDockPanel.Children.Add(timeDisplayTextBlock);
        sliderDockPanel.Children.Add(sliderContainer);

        var topMainPlaybackControls = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(10, 5, 10, 0),
            Spacing = 8
        };
        topMainPlaybackControls.Children.Add(playbackButtonControlsPanel);
        topMainPlaybackControls.Children.Add(sliderDockPanel);

        var outerPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Background = theme.B_BackgroundColor,
            Margin = new Thickness(0, 5, 0, 5)
        };
        outerPanel.Children.Add(topMainPlaybackControls);

        return outerPanel;
    }
}
