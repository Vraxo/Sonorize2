using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml.Templates;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;

namespace Sonorize.Views.MainWindowControls;

public static class MainPlaybackControlsPanel
{
    public static Grid Create(ThemeColors theme)
    {
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
            FontSize = 18,
            FontFamily = "Segoe UI Symbol, Arial",
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
            [!TextBlock.TextProperty] = new Binding("Playback.ShuffleEnabled") { Converter = BooleanToShuffleIconConverter.Instance }
        };
        shuffleButton.Bind(ToggleButton.IsCheckedProperty, new Binding("Playback.ShuffleEnabled", BindingMode.TwoWay));
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
        shuffleButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

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
            FontSize = 18,
            FontFamily = "Segoe UI Symbol, Arial",
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
            [!TextBlock.TextProperty] = new Binding("Playback.RepeatMode")
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
            [!TextBlock.RenderTransformProperty] = new Binding("Playback.RepeatMode")
            {
                Converter = new FuncValueConverter<RepeatMode, ITransform?>(mode => mode switch
                {
                    RepeatMode.PlayOnce => new TranslateTransform(0, -3),
                    RepeatMode.None => new TranslateTransform(0, -1),
                    _ => null
                })
            }
        };
        repeatModeButton[!ToggleButton.ForegroundProperty] = new Binding("Playback.RepeatMode")
        {
            Converter = new FuncValueConverter<RepeatMode, IBrush>(mode => mode != RepeatMode.None ? theme.B_AccentColor : theme.B_SecondaryTextColor)
        };
        repeatModeButton[!ToggleButton.BorderBrushProperty] = new Binding("Playback.RepeatMode")
        {
            Converter = new FuncValueConverter<RepeatMode, IBrush>(mode => mode != RepeatMode.None ? theme.B_AccentColor : theme.B_ControlBackgroundColor)
        };
        repeatModeButton.Bind(ToggleButton.IsCheckedProperty, new Binding("Playback.IsRepeatActive"));
        repeatModeButton.Bind(Button.CommandProperty, new Binding("Playback.CycleRepeatModeCommand"));
        repeatModeButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

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

        var toggleAdvPanelButton = new Button
        {
            Content = "+",
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 4),
            MinWidth = 30,
            FontWeight = FontWeight.Bold,
            Width = 32,
            Height = 32
        };
        toggleAdvPanelButton[!Button.BorderBrushProperty] = new Binding("IsAdvancedPanelVisible")
        {
            Converter = new FuncValueConverter<bool, IBrush>(isVisible => isVisible ? theme.B_AccentColor : theme.B_ControlBackgroundColor)
        };
        toggleAdvPanelButton[!Button.ForegroundProperty] = new Binding("IsAdvancedPanelVisible")
        {
            Converter = new FuncValueConverter<bool, IBrush>(isVisible => isVisible ? theme.B_AccentColor : theme.B_TextColor)
        };
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        toggleAdvPanelButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var rightControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 10, 0)
        };
        rightControlsPanel.Children.Add(toggleAdvPanelButton);

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
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center,
            Background = Brushes.Transparent,
            Foreground = theme.B_AccentColor,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            [!Slider.MaximumProperty] = new Binding("Playback.CurrentSongDurationSeconds"),
            [!Slider.ValueProperty] = new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay),
            [!Control.IsHitTestVisibleProperty] = new Binding("Playback.HasCurrentSong")
        };

        mainPlaybackSlider.Styles.Add(new Style(x => x.OfType<Slider>()))
{
            Setters =
    {
                new Setter(Slider.TemplateProperty, new FuncControlTemplate((control, scope) =>
                {
                    var slider = (Slider)control;

                    var track = new Track
                    {
                        Name = "PART_Track",
                        Minimum = slider.Minimum,
                        Maximum = slider.Maximum,
                        Value = slider.Value,
                        Orientation = Orientation.Horizontal,
                        Thumb = new Thumb
                        {
                            Width = 12,
                            Height = 12,
                            Background = slider.Foreground,
                            BorderBrush = slider.Foreground,
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(6)
                        },
                        DecreaseRepeatButton = new RepeatButton
                        {
                            Background = theme.B_AccentColor,
                            Height = 8,
                            VerticalAlignment = VerticalAlignment.Center,
                            IsEnabled = false
                        },
                        IncreaseRepeatButton = new RepeatButton
                        {
                            Background = theme.B_ControlBackgroundColor,
                            Height = 8,
                            VerticalAlignment = VerticalAlignment.Center,
                            IsEnabled = false
                        }
                    };

                    track.Bind(Track.MinimumProperty, new Binding("Minimum") { Source = slider });
                    track.Bind(Track.MaximumProperty, new Binding("Maximum") { Source = slider });
                    track.Bind(Track.ValueProperty, new Binding("Value") { Source = slider, Mode = BindingMode.TwoWay });

                    return new Grid
                    {
                        Height = 24,
                        Children = { track }
                    };
                }))
    }
        };

        var timeSliderGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center,
            Height = 36,
            MinWidth = 500,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        Grid.SetColumn(currentTimeTextBlock, 0);
        Grid.SetColumn(mainPlaybackSlider, 1);
        Grid.SetColumn(totalTimeTextBlock, 2);
        timeSliderGrid.Children.Add(currentTimeTextBlock);
        timeSliderGrid.Children.Add(mainPlaybackSlider);
        timeSliderGrid.Children.Add(totalTimeTextBlock);

        var centerPlaybackControlsStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 5, 0, 0),
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        centerPlaybackControlsStack.Children.Add(combinedPlaybackButtonControlsPanel);
        centerPlaybackControlsStack.Children.Add(timeSliderGrid);

        var songInfoPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(10, 0, 0, 0),
            Spacing = 8
        };
        songInfoPanel.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        var thumbnailImage = new Image
        {
            Width = 64,
            Height = 64,
            Stretch = Stretch.UniformToFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        RenderOptions.SetBitmapInterpolationMode(thumbnailImage, BitmapInterpolationMode.HighQuality);
        thumbnailImage.Bind(Image.SourceProperty, new Binding("Playback.CurrentSong.Thumbnail"));

        var textStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 1
        };

        var titleTextBlock = new TextBlock
        {
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.B_TextColor,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 200
        };
        titleTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Title"));

        var artistTextBlock = new TextBlock
        {
            FontSize = 11,
            Foreground = theme.B_SecondaryTextColor,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 200
        };
        artistTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Artist"));

        textStack.Children.Add(titleTextBlock);
        textStack.Children.Add(artistTextBlock);
        songInfoPanel.Children.Add(thumbnailImage);
        songInfoPanel.Children.Add(textStack);

        var outerGrid = new Grid
        {
            Background = theme.B_BackgroundColor,
            Margin = new Thickness(0, 5, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = new RowDefinitions("Auto"),
            ColumnDefinitions = new ColumnDefinitions("*")
        };
        Grid.SetColumn(songInfoPanel, 0);
        Grid.SetColumn(centerPlaybackControlsStack, 0);
        Grid.SetColumn(rightControlsPanel, 0);
        outerGrid.Children.Add(songInfoPanel);
        outerGrid.Children.Add(centerPlaybackControlsStack);
        outerGrid.Children.Add(rightControlsPanel);

        return outerGrid;
    }
}
