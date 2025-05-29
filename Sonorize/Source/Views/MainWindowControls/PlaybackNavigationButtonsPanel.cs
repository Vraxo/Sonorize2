using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels; // Required for RepeatMode enum

namespace Sonorize.Views.MainWindowControls;

public static class PlaybackNavigationButtonsPanel
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
        previousButton.Bind(Button.CommandProperty, new Binding("Library.PreviousTrackCommand"));

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
        // Bind to Playback.Controls for command and IsPlaying state
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.Controls.PlayPauseResumeCommand"));
        mainPlayPauseButton.Bind(Button.ContentProperty, new Binding("Playback.Controls.IsPlaying") { Converter = BooleanToPlayPauseIconConverter.Instance });

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
        nextButton.Bind(Button.CommandProperty, new Binding("Library.NextTrackCommand"));

        var shuffleButton = new ToggleButton
        {
            Foreground = theme.B_SecondaryTextColor,
            Background = Brushes.Transparent,
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            ContentTemplate = null,
            Width = 32,
            Height = 32
        };
        shuffleButton.Content = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 18,
            FontFamily = new FontFamily("Segoe UI Symbol, Arial"),
            [!TextBlock.TextProperty] = new Binding("Playback.ModeControls.ShuffleEnabled") { Converter = BooleanToShuffleIconConverter.Instance }
        };
        shuffleButton.Bind(ToggleButton.IsCheckedProperty, new Binding("Playback.ModeControls.ShuffleEnabled", BindingMode.TwoWay));
        shuffleButton[!ToggleButton.ForegroundProperty] = new Binding("IsChecked")
        {
            Source = shuffleButton,
            Converter = new FuncValueConverter<bool, IBrush>(isChecked => isChecked ? theme.B_AccentColor : theme.B_SecondaryTextColor)
        };
        shuffleButton[!ToggleButton.BorderBrushProperty] = new Binding("IsChecked")
        {
            Source = shuffleButton,
            Converter = new FuncValueConverter<bool, IBrush>(isChecked => isChecked ? theme.B_AccentColor : theme.B_ControlBackgroundColor)
        };
        // HasCurrentSong is still on PlaybackViewModel or proxied via Playback.Controls
        shuffleButton.Bind(Control.IsEnabledProperty, new Binding("Playback.Controls.HasCurrentSong"));

        var repeatModeButton = new ToggleButton
        {
            Foreground = theme.B_SecondaryTextColor,
            Background = Brushes.Transparent,
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            ContentTemplate = null,
            Width = 32,
            Height = 32
        };
        repeatModeButton.Content = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 18,
            FontFamily = new FontFamily("Segoe UI Symbol, Arial"),
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            [!TextBlock.TextProperty] = new Binding("Playback.ModeControls.RepeatMode")
            {
                Converter = new FuncValueConverter<RepeatMode, string>(mode => mode switch
                {
                    RepeatMode.None => "―",
                    RepeatMode.PlayOnce => "₁",
                    RepeatMode.RepeatOne => "🔂",
                    RepeatMode.RepeatAll => "🔁",
                    _ => "?"
                })
            },
            [!TextBlock.RenderTransformProperty] = new Binding("Playback.ModeControls.RepeatMode")
            {
                Converter = new FuncValueConverter<RepeatMode, ITransform?>(mode =>
                {
                    return mode switch
                    {
                        RepeatMode.PlayOnce => new TranslateTransform(0, -3),
                        RepeatMode.None => new TranslateTransform(0, -1),
                        _ => null
                    };
                })
            }
        };
        repeatModeButton[!ToggleButton.ForegroundProperty] = new Binding("Playback.ModeControls.RepeatMode")
        {
            Converter = new FuncValueConverter<RepeatMode, IBrush>(mode => mode != RepeatMode.None ? theme.B_AccentColor : theme.B_SecondaryTextColor)
        };
        repeatModeButton[!ToggleButton.BorderBrushProperty] = new Binding("Playback.ModeControls.RepeatMode")
        {
            Converter = new FuncValueConverter<RepeatMode, IBrush>(mode => mode != RepeatMode.None ? theme.B_AccentColor : theme.B_ControlBackgroundColor)
        };
        repeatModeButton.Bind(ToggleButton.IsCheckedProperty, new Binding("Playback.ModeControls.IsRepeatActive"));
        repeatModeButton.Bind(Button.CommandProperty, new Binding("Playback.ModeControls.CycleRepeatModeCommand"));
        repeatModeButton.Bind(Control.IsEnabledProperty, new Binding("Playback.Controls.HasCurrentSong"));

        var combinedPlaybackButtonControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0)
        };

        combinedPlaybackButtonControlsPanel.Children.Add(shuffleButton);
        combinedPlaybackButtonControlsPanel.Children.Add(previousButton);
        combinedPlaybackButtonControlsPanel.Children.Add(mainPlayPauseButton);
        combinedPlaybackButtonControlsPanel.Children.Add(nextButton);
        combinedPlaybackButtonControlsPanel.Children.Add(repeatModeButton);

        return combinedPlaybackButtonControlsPanel;
    }
}