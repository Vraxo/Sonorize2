using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sonorize.Models;
using Sonorize.ViewModels;
using System.Diagnostics; // Added for Debug.WriteLine

namespace Sonorize.Views;

public class MainWindow : Window
{
    private ListBox _songListBox;

    public MainWindow()
    {
        Debug.WriteLine("[MainView] Constructor called.");
        Title = "Sonorize";
        Width = 900;
        Height = 700;
        MinWidth = 600;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;

        // Theme brushes
        var backgroundColor = SolidColorBrush.Parse("#FF1E1E1E");
        var slightlyLighterBackground = SolidColorBrush.Parse("#FF2D2D30");
        var textColor = SolidColorBrush.Parse("#FFF1F1F1");
        var secondaryTextColor = SolidColorBrush.Parse("#FFAAAAAA");
        var accentColor = SolidColorBrush.Parse("#FF007ACC");

        Background = backgroundColor;

        // --- Menu Bar ---
        var menu = new Menu
        {
            Background = slightlyLighterBackground,
            Foreground = textColor
        };

        var fileMenuItem = new MenuItem { Header = "_File" };

        var addDirectoryMenuItem = new MenuItem { Header = "_Add Music Directory..." };
        addDirectoryMenuItem.Bind(MenuItem.CommandProperty, new Binding("AddDirectoryAndRefreshCommand"));
        addDirectoryMenuItem.CommandParameter = this;

        var settingsMenuItem = new MenuItem { Header = "_Settings..." };
        settingsMenuItem.Bind(MenuItem.CommandProperty, new Binding("OpenSettingsCommand"));
        settingsMenuItem.CommandParameter = this;

        var exitMenuItem = new MenuItem { Header = "E_xit" };
        exitMenuItem.Bind(MenuItem.CommandProperty, new Binding("ExitCommand"));

        fileMenuItem.Items.Add(addDirectoryMenuItem);
        fileMenuItem.Items.Add(settingsMenuItem);
        fileMenuItem.Items.Add(new Separator());
        fileMenuItem.Items.Add(exitMenuItem);

        menu.Items.Add(fileMenuItem);

        // --- Song List ---
        _songListBox = new ListBox
        {
            Background = slightlyLighterBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10)
        };
        _songListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Songs"));
        _songListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedSong", BindingMode.TwoWay));

        _songListBox.ItemTemplate = new FuncDataTemplate<Song>(
            (song, scope) =>
            {
                // --- ADDED LOGGING ---
                Debug.WriteLine($"[ItemTemplate] Creating item for: {song.Title}. Thumbnail is {(song.Thumbnail == null ? "NULL" : "NOT NULL")}");
                if (song.Thumbnail != null)
                {
                    Debug.WriteLine($"[ItemTemplate]   Thumbnail PixelSize: {song.Thumbnail.PixelSize}, Dpi: {song.Thumbnail.Dpi}");
                }
                // --- END ADDED LOGGING ---

                var image = new Image
                {
                    Width = 32,
                    Height = 32,
                    Margin = new Thickness(5, 0, 5, 0),
                    Source = song.Thumbnail, // This is where the thumbnail is used
                    Stretch = Stretch.UniformToFill
                };
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

                var titleBlock = new TextBlock
                {
                    Text = song.Title,
                    FontSize = 14,
                    FontWeight = FontWeight.Normal,
                    Foreground = textColor,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 1)
                };
                var artistBlock = new TextBlock
                {
                    Text = song.Artist,
                    FontSize = 11,
                    Foreground = secondaryTextColor,
                    VerticalAlignment = VerticalAlignment.Center
                };
                var durationBlock = new TextBlock
                {
                    Text = song.DurationString,
                    FontSize = 11,
                    Foreground = secondaryTextColor,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var textStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                textStack.Children.Add(titleBlock);
                textStack.Children.Add(artistBlock);

                var itemGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(image, 0);
                Grid.SetColumn(textStack, 1);
                Grid.SetColumn(durationBlock, 2);
                itemGrid.Children.Add(image);
                itemGrid.Children.Add(textStack);
                itemGrid.Children.Add(durationBlock);

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

        var scrollViewer = new ScrollViewer
        {
            Content = _songListBox,
            Padding = new Thickness(0, 0, 0, 5)
        };

        // --- Playback Controls ---
        var playbackSlider = new Slider
        {
            Minimum = 0,
            Margin = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = secondaryTextColor,
            Foreground = accentColor
        };
        playbackSlider.Bind(Slider.MaximumProperty, new Binding("PlaybackService.CurrentSongDurationSeconds"));
        playbackSlider.Bind(Slider.ValueProperty, new Binding("PlaybackService.CurrentPositionSeconds", BindingMode.TwoWay));

        var playPauseButton = new Button
        {
            Content = "Play/Pause",
            Margin = new Thickness(5),
            Background = slightlyLighterBackground,
            Foreground = textColor,
            BorderBrush = accentColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(10, 5)
        };
        playPauseButton.Click += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.PlaybackService.IsPlaying)
                    vm.PlaybackService.Pause();
                else if (vm.PlaybackService.CurrentSong != null)
                    vm.PlaybackService.Resume();
                else if (vm.Songs.Any())
                    vm.PlaybackService.Play(vm.Songs.First());
            }
        };

        var playbackControlsPanel = new DockPanel
        {
            Background = backgroundColor,
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
            Background = slightlyLighterBackground,
            Padding = new Thickness(10, 4),
            Height = 26
        };
        var statusBarText = new TextBlock
        {
            Foreground = secondaryTextColor,
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