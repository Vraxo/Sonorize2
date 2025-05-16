using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For TemplatedControl and Thumb
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sonorize.Models;
using Sonorize.ViewModels;
using System.Diagnostics;
using Avalonia.Styling; // For Style, Selector

namespace Sonorize.Views;

public class MainWindow : Window
{
    private ListBox _songListBox;
    private readonly ThemeColors _theme;

    public MainWindow(ThemeColors theme)
    {
        _theme = theme;

        Debug.WriteLine($"[MainView] Constructor called with theme: BG {_theme.BackgroundColor}, Accent {_theme.AccentColor}");
        Title = "Sonorize";
        Width = 900;
        Height = 700;
        MinWidth = 600;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        Background = _theme.B_BackgroundColor;

        // --- Menu Bar ---
        var menu = new Menu
        {
            Background = _theme.B_SlightlyLighterBackground,
            Foreground = _theme.B_TextColor
        };
        var fileMenuItem = new MenuItem { Header = "_File", Foreground = _theme.B_TextColor };
        var addDirectoryMenuItem = new MenuItem { Header = "_Add Music Directory...", Foreground = _theme.B_TextColor };
        addDirectoryMenuItem.Bind(MenuItem.CommandProperty, new Binding("AddDirectoryAndRefreshCommand"));
        addDirectoryMenuItem.CommandParameter = this;
        var settingsMenuItem = new MenuItem { Header = "_Settings...", Foreground = _theme.B_TextColor };
        settingsMenuItem.Bind(MenuItem.CommandProperty, new Binding("OpenSettingsCommand"));
        settingsMenuItem.CommandParameter = this;
        var exitMenuItem = new MenuItem { Header = "E_xit", Foreground = _theme.B_TextColor };
        exitMenuItem.Bind(MenuItem.CommandProperty, new Binding("ExitCommand"));
        fileMenuItem.Items.Add(addDirectoryMenuItem);
        fileMenuItem.Items.Add(settingsMenuItem);
        fileMenuItem.Items.Add(new Separator());
        fileMenuItem.Items.Add(exitMenuItem);
        menu.Items.Add(fileMenuItem);

        // --- Song List ---
        _songListBox = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10),
            Styles =
            {
                new Style(x => x.OfType<ListBoxItem>())
                {
                    Setters =
                    {
                        new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground),
                        new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor)
                    }
                },
                new Style(x => x.OfType<ListBoxItem>().Class(":pointerover"))
                {
                    Setters =
                    {
                        new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor),
                    }
                },
                new Style(x => x.OfType<ListBoxItem>().Class(":selected"))
                {
                    Setters =
                    {
                        new Setter(TemplatedControl.BackgroundProperty, BrushExtensions.Multiply(_theme.B_AccentColor, 0.7)),
                        new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
                    }
                },
                 new Style(x => x.OfType<ListBoxItem>().Class(":selected").Class(":pointerover"))
                {
                     Setters =
                    {
                        new Setter(TemplatedControl.BackgroundProperty, BrushExtensions.Multiply(_theme.B_AccentColor, 0.8)),
                        new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
                    }
                }
            }
        };
        _songListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Songs"));
        _songListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedSong", BindingMode.TwoWay));

        _songListBox.ItemTemplate = new FuncDataTemplate<Song>(
            (song, scope) =>
            {
                var image = new Image
                {
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(5, 0, 5, 0),
                    Source = song.Thumbnail,
                    Stretch = Stretch.UniformToFill
                };
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

                var titleBlock = new TextBlock
                {
                    Text = song.Title,
                    FontSize = 14,
                    FontWeight = FontWeight.Normal,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 1)
                };
                var artistBlock = new TextBlock
                {
                    Text = song.Artist,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var durationBlock = new TextBlock // Declaration of durationBlock
                {
                    Text = song.DurationString,
                    FontSize = 11,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Heuristic for artist/duration foreground when selected item has a light foreground text
                if (_theme.B_AccentForeground is ISolidColorBrush accentFgBrush && accentFgBrush.Color.ToHsl().L > 0.5)
                {
                    // This logic will apply the secondary text color if the AccentForeground is light.
                    // The ListBoxItem :selected style sets a general Foreground. If that is light (e.g. white on dark accent),
                    // then making artist/duration dimmer can improve readability.
                    // Note: This applies directly and won't change if the ListBoxItem's *selected* style for Foreground changes.
                    // For more dynamic behavior tied to the ListBoxItem's actual selected foreground, styles targeting
                    // child TextBlocks would be needed, which is more complex.
                    artistBlock.Foreground = _theme.B_SecondaryTextColor;
                    durationBlock.Foreground = _theme.B_SecondaryTextColor;
                }


                var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                textStack.Children.Add(titleBlock);
                textStack.Children.Add(artistBlock);

                var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(image, 0); Grid.SetColumn(textStack, 1); Grid.SetColumn(durationBlock, 2);
                itemGrid.Children.Add(image); itemGrid.Children.Add(textStack); itemGrid.Children.Add(durationBlock);

                return new Border
                {
                    Padding = new Thickness(10, 6, 10, 6),
                    MinHeight = 44,
                    Background = Brushes.Transparent,
                    Child = itemGrid
                };
            },
            supportsRecycling: true
        );

        var scrollViewer = new ScrollViewer { Content = _songListBox, Padding = new Thickness(0, 0, 0, 5) };

        // --- Playback Controls ---
        var playbackSlider = new Slider
        {
            Minimum = 0,
            Margin = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = _theme.B_SecondaryTextColor,
            Foreground = _theme.B_AccentColor
        };
        var sliderThumbStyle = new Style(x => x.OfType<Slider>().Descendant().OfType<Thumb>());
        sliderThumbStyle.Setters.Add(new Setter(BackgroundProperty, _theme.B_AccentColor));
        playbackSlider.Styles.Add(sliderThumbStyle);

        playbackSlider.Bind(Slider.MaximumProperty, new Binding("PlaybackService.CurrentSongDurationSeconds"));
        playbackSlider.Bind(Slider.ValueProperty, new Binding("PlaybackService.CurrentPositionSeconds", BindingMode.TwoWay));

        var playPauseButton = new Button
        {
            Content = "Play/Pause",
            Margin = new Thickness(5),
            Background = _theme.B_SlightlyLighterBackground,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_AccentColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10, 5)
        };
        playPauseButton.Click += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.PlaybackService.IsPlaying) vm.PlaybackService.Pause();
                else if (vm.PlaybackService.CurrentSong != null) vm.PlaybackService.Resume();
                else if (vm.Songs.Any()) vm.PlaybackService.Play(vm.Songs.First());
            }
        };

        var playbackControlsPanel = new DockPanel
        {
            Background = _theme.B_BackgroundColor,
            Margin = new Thickness(5, 0, 5, 5),
            Height = 35,
            LastChildFill = true
        };
        DockPanel.SetDock(playPauseButton, Dock.Left);
        playbackControlsPanel.Children.Add(playPauseButton);
        playbackControlsPanel.Children.Add(playbackSlider);

        // --- Status Bar ---
        var statusBar = new Border
        {
            Background = _theme.B_SlightlyLighterBackground,
            Padding = new Thickness(10, 4),
            Height = 26
        };
        var statusBarText = new TextBlock
        {
            Foreground = _theme.B_SecondaryTextColor,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11
        };
        statusBarText.Bind(TextBlock.TextProperty, new Binding("StatusBarText"));
        statusBar.Child = statusBarText;

        // --- Main Layout ---
        var mainDockPanel = new DockPanel();
        DockPanel.SetDock(menu, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        DockPanel.SetDock(playbackControlsPanel, Dock.Bottom);
        mainDockPanel.Children.Add(menu);
        mainDockPanel.Children.Add(statusBar);
        mainDockPanel.Children.Add(playbackControlsPanel);
        mainDockPanel.Children.Add(scrollViewer);

        Content = mainDockPanel;
        Debug.WriteLine("[MainView] Constructor finished.");
    }
}

// Helper extension class for IBrush operations
public static class BrushExtensions
{
    public static IBrush Multiply(this IBrush brush, double factor)
    {
        if (brush is ISolidColorBrush solidBrush)
        {
            var c = solidBrush.Color;
            return new SolidColorBrush(Color.FromArgb(
                c.A,
                (byte)Math.Clamp(c.R * factor, 0, 255),
                (byte)Math.Clamp(c.G * factor, 0, 255),
                (byte)Math.Clamp(c.B * factor, 0, 255)
            ));
        }
        return brush;
    }
}