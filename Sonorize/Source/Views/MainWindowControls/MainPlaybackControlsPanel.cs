using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
// Removed: using Avalonia.Themes.Fluent; 
using Sonorize.Converters;
using Sonorize.Models; // For ThemeColors
using Sonorize.Views; // For BrushExtensions

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
            Height = 24,
            Background = theme.B_SecondaryTextColor,
            Foreground = theme.B_AccentColor
        };
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor) } });
        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var mainPlayPauseButton = new Button
        {
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_AccentColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(20),
            Padding = new Thickness(0),
            Width = 40,
            Height = 40,
            FontSize = 20,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        var playPauseIconBinding = new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseIconConverter.Instance };
        mainPlayPauseButton.Bind(Button.ContentProperty, playPauseIconBinding);
        mainPlayPauseButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        // Style for the normal, enabled button state (not hovered, not pressed)
        mainPlayPauseButton.Styles.Add(new Style(s => s.Is<Button>()
            .Class(":enabled")
            .Not(x => x.Class(":pointerover"))
            .Not(x => x.Class(":pressed")))
        {
            Setters = { new Setter(Button.BackgroundProperty, theme.B_SlightlyLighterBackground) }
        });

        // Style for :pointerover state (applies when enabled)
        mainPlayPauseButton.Styles.Add(new Style(s => s.Is<Button>().Class(":pointerover"))
        {
            Setters = { new Setter(Button.BackgroundProperty, theme.B_ControlBackgroundColor) }
        });

        // Style for :pressed state (applies when enabled)
        mainPlayPauseButton.Styles.Add(new Style(s => s.Is<Button>().Class(":pressed"))
        {
            Setters = { new Setter(Button.BackgroundProperty, theme.B_SlightlyLighterBackground.Multiply(0.8)) }
        });

        mainPlayPauseButton.Resources["AccentFillColorDefaultBrush"] = theme.B_SlightlyLighterBackground;


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
            MinHeight = 30,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        toggleAdvPanelButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var timeDisplayTextBlock = new TextBlock
        {
            Foreground = theme.B_TextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 75,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        timeDisplayTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentTimeTotalTimeDisplay"));

        var seekerAreaPanel = new Panel
        {
            Height = 50,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent
        };
        // Changed order: Add button first, then slider so slider renders on top
        seekerAreaPanel.Children.Add(mainPlayPauseButton);
        seekerAreaPanel.Children.Add(mainPlaybackSlider);


        var topControlsGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(10, 0),
            Height = 50,
            VerticalAlignment = VerticalAlignment.Center
        };

        Grid.SetColumn(toggleAdvPanelButton, 0);
        Grid.SetColumn(seekerAreaPanel, 1);
        Grid.SetColumn(timeDisplayTextBlock, 2);

        topControlsGrid.Children.Add(toggleAdvPanelButton);
        topControlsGrid.Children.Add(seekerAreaPanel);
        topControlsGrid.Children.Add(timeDisplayTextBlock);

        var outerPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Background = theme.B_BackgroundColor,
            Margin = new Thickness(0, 5, 0, 5)
        };
        outerPanel.Children.Add(topControlsGrid);
        return outerPanel;
    }
}