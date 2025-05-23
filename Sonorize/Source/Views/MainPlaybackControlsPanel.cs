using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Converters;
using Sonorize.Models; // For ThemeColors

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
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor) } });
        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var mainPlayPauseButton = new Button { Content = "Play", Background = theme.B_SlightlyLighterBackground, Foreground = theme.B_TextColor, BorderBrush = theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 5), MinWidth = 70 };
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        var playPauseContentBinding = new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseTextConverter.Instance };
        mainPlayPauseButton.Bind(Button.ContentProperty, playPauseContentBinding);

        var toggleAdvPanelButton = new Button { Content = "+", Background = theme.B_SlightlyLighterBackground, Foreground = theme.B_TextColor, BorderBrush = theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), MinWidth = 30, FontWeight = FontWeight.Bold };
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        toggleAdvPanelButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var controlsButtonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        };
        controlsButtonPanel.Children.Add(mainPlayPauseButton);
        controlsButtonPanel.Children.Add(toggleAdvPanelButton);

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

        var topMainPlaybackControls = new DockPanel
        {
            LastChildFill = true,
            Height = 35,
            Margin = new Thickness(10, 0)
        };
        DockPanel.SetDock(controlsButtonPanel, Dock.Left);
        DockPanel.SetDock(timeDisplayTextBlock, Dock.Right);

        topMainPlaybackControls.Children.Add(controlsButtonPanel);
        topMainPlaybackControls.Children.Add(timeDisplayTextBlock);
        topMainPlaybackControls.Children.Add(mainPlaybackSlider);

        var activeLoopDisplayText = new TextBlock { Foreground = theme.B_SecondaryTextColor, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10, 0, 10, 2), MinHeight = 14 };
        activeLoopDisplayText.Bind(TextBlock.TextProperty, new Binding("LoopEditor.ActiveLoopDisplayText"));

        var outerPanel = new StackPanel { Orientation = Orientation.Vertical, Background = theme.B_BackgroundColor, Margin = new Thickness(0, 5, 0, 5) };
        outerPanel.Children.Add(topMainPlaybackControls);
        outerPanel.Children.Add(activeLoopDisplayText);
        return outerPanel;
    }
}