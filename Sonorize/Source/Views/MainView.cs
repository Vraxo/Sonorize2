using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models;
using Sonorize.ViewModels;
using Avalonia.Data;
using Sonorize.Controls; // For WaveformDisplayControl
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives; // For TemplatedControl
using Avalonia.Media.Imaging;
using Avalonia.Data.Converters;
using Avalonia.Styling;
using Sonorize.Services;

namespace Sonorize.Views;
public class MainWindow : Window
{
    private readonly ThemeColors _theme;

    public MainWindow(ThemeColors theme)
    {
        _theme = theme;
        Title = "Sonorize"; Width = 950; Height = 750; MinWidth = 700; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = _theme.B_BackgroundColor;

        var mainGrid = new Grid
        {
            RowDefinitions = new RowDefinitions
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };

        var menu = CreateMenu();
        Grid.SetRow(menu, 0);
        mainGrid.Children.Add(menu);

        var songListScrollViewer = CreateSongListScrollViewer();
        Grid.SetRow(songListScrollViewer, 1);
        mainGrid.Children.Add(songListScrollViewer);

        var advancedPlaybackPanel = CreateAdvancedPlaybackPanel();
        advancedPlaybackPanel.Bind(Visual.IsVisibleProperty, new Binding("IsAdvancedPanelVisible"));
        Grid.SetRow(advancedPlaybackPanel, 2);
        mainGrid.Children.Add(advancedPlaybackPanel);

        var mainPlaybackControls = CreateMainPlaybackControls();
        Grid.SetRow(mainPlaybackControls, 3);
        mainGrid.Children.Add(mainPlaybackControls);

        var statusBar = CreateStatusBar();
        Grid.SetRow(statusBar, 4);
        mainGrid.Children.Add(statusBar);

        Content = mainGrid;
    }

    private Menu CreateMenu()
    {
        var menu = new Menu { Background = _theme.B_SlightlyLighterBackground, Foreground = _theme.B_TextColor };
        var fileMenuItem = new MenuItem { Header = "_File", Foreground = _theme.B_TextColor };
        var addDirectoryMenuItem = new MenuItem { Header = "_Add Music Directory...", Foreground = _theme.B_TextColor };
        addDirectoryMenuItem.Bind(MenuItem.CommandProperty, new Binding("AddDirectoryAndRefreshCommand"));
        addDirectoryMenuItem.CommandParameter = this;
        var settingsMenuItem = new MenuItem { Header = "_Settings...", Foreground = _theme.B_TextColor };
        settingsMenuItem.Bind(MenuItem.CommandProperty, new Binding("OpenSettingsCommand"));
        settingsMenuItem.CommandParameter = this;
        var exitMenuItem = new MenuItem { Header = "E_xit", Foreground = _theme.B_TextColor };
        exitMenuItem.Bind(MenuItem.CommandProperty, new Binding("ExitCommand"));
        fileMenuItem.Items.Add(addDirectoryMenuItem); fileMenuItem.Items.Add(settingsMenuItem);
        fileMenuItem.Items.Add(new Separator()); fileMenuItem.Items.Add(exitMenuItem);
        menu.Items.Add(fileMenuItem);
        return menu;
    }

    private ScrollViewer CreateSongListScrollViewer()
    {
        var songListBox = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10),
            Name = "SongListBox"
        };

        songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>())
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground), new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor) } });
        songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover"))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) } });
        songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, BrushExtensions.Multiply(_theme.B_AccentColor, 0.7)), new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground) } });
        songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, BrushExtensions.Multiply(_theme.B_AccentColor, 0.8)), new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground) } });

        songListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Songs"));
        songListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedSong", BindingMode.TwoWay));
        songListBox.ItemTemplate = new FuncDataTemplate<Song>((song, scope) => {
            var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 5, 0), Source = song.Thumbnail, Stretch = Stretch.UniformToFill };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
            var titleBlock = new TextBlock { Text = song.Title, FontSize = 14, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
            var artistBlock = new TextBlock { Text = song.Artist, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
            var durationBlock = new TextBlock { Text = song.DurationString, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            if (_theme.B_AccentForeground is ISolidColorBrush accentFgBrush && accentFgBrush.Color.ToHsl().L > 0.5) { artistBlock.Foreground = _theme.B_SecondaryTextColor; durationBlock.Foreground = _theme.B_SecondaryTextColor; }
            var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Children = { titleBlock, artistBlock } };
            var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center, Children = { image, textStack, durationBlock } };
            Grid.SetColumn(image, 0); Grid.SetColumn(textStack, 1); Grid.SetColumn(durationBlock, 2);
            return new Border { Padding = new Thickness(10, 6, 10, 6), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
        }, supportsRecycling: true);
        return new ScrollViewer { Content = songListBox, Padding = new Thickness(0, 0, 0, 5) };
    }

    private Border CreateAdvancedPlaybackPanel()
    {
        var panelRoot = new Border
        {
            Background = _theme.B_SlightlyLighterBackground,
            Padding = new Thickness(10),
            BorderBrush = _theme.B_AccentColor,
            BorderThickness = new Thickness(0, 1, 0, 1),
            MinHeight = 200,
            ClipToBounds = true
        };

        var mainStack = new StackPanel { Spacing = 10 };

        var speedPitchGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 5) };
        var speedLabel = new TextBlock { Text = "Speed:", VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_TextColor };
        var speedSlider = new Slider
        {
            Minimum = 0.5,
            Maximum = 2.0,
            SmallChange = 0.05,
            LargeChange = 0.25,
            TickFrequency = 0.25,
            Foreground = _theme.B_AccentColor,
            Background = _theme.B_SecondaryTextColor
        };
        speedSlider.Bind(Slider.ValueProperty, new Binding("PlaybackSpeed", BindingMode.TwoWay));
        var speedDisplay = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0), Foreground = _theme.B_TextColor, MinWidth = 35 };
        speedDisplay.Bind(TextBlock.TextProperty, new Binding("PlaybackSpeedDisplay"));

        var pitchLabel = new TextBlock { Text = "Pitch:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(15, 0, 0, 0), Foreground = _theme.B_TextColor };
        var pitchSlider = new Slider { Minimum = -5, Maximum = 5, SmallChange = 0.5, LargeChange = 1, TickFrequency = 1, Foreground = _theme.B_AccentColor, Background = _theme.B_SecondaryTextColor };
        pitchSlider.Bind(Slider.ValueProperty, new Binding("PlaybackPitch", BindingMode.TwoWay));
        var pitchDisplay = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0), Foreground = _theme.B_TextColor, MinWidth = 45 };
        pitchDisplay.Bind(TextBlock.TextProperty, new Binding("PlaybackPitchDisplay"));

        Grid.SetColumn(speedLabel, 0); Grid.SetColumn(speedSlider, 1); Grid.SetColumn(speedDisplay, 2);
        Grid.SetColumn(pitchLabel, 3); Grid.SetColumn(pitchSlider, 4); Grid.SetColumn(pitchDisplay, 5);
        speedPitchGrid.Children.Add(speedLabel); speedPitchGrid.Children.Add(speedSlider); speedPitchGrid.Children.Add(speedDisplay);
        speedPitchGrid.Children.Add(pitchLabel); speedPitchGrid.Children.Add(pitchSlider); speedPitchGrid.Children.Add(pitchDisplay);
        mainStack.Children.Add(speedPitchGrid);

        Color accentColorForLoopRegion = (_theme.B_AccentColor as ISolidColorBrush)?.Color ?? Colors.Orange;
        var waveformDisplay = new WaveformDisplayControl
        {
            Height = 80,
            MinHeight = 60,
            Background = _theme.B_ControlBackgroundColor,
            WaveformBrush = _theme.B_AccentColor,
            PositionMarkerBrush = Brushes.OrangeRed,
            LoopRegionBrush = new SolidColorBrush(accentColorForLoopRegion, 0.3)
        };
        waveformDisplay.Bind(WaveformDisplayControl.WaveformPointsProperty, new Binding("WaveformRenderData"));
        waveformDisplay.Bind(WaveformDisplayControl.CurrentPositionProperty, new Binding("PlaybackService.CurrentPosition"));
        waveformDisplay.Bind(WaveformDisplayControl.DurationProperty, new Binding("PlaybackService.CurrentSongDuration"));
        waveformDisplay.Bind(WaveformDisplayControl.ActiveLoopProperty, new Binding("PlaybackService.CurrentSong.ActiveLoop"));
        waveformDisplay.SeekRequested += (s, time) => (DataContext as MainWindowViewModel)?.WaveformSeekCommand.Execute(time);

        var waveformLoadingIndicator = new ProgressBar
        {
            IsIndeterminate = true,
            Height = 5,
            Margin = new Thickness(0, -5, 0, 0),
            Foreground = _theme.B_AccentColor,
            Background = Brushes.Transparent
        };
        waveformLoadingIndicator.Bind(Visual.IsVisibleProperty, new Binding("IsWaveformLoading"));

        var waveformContainer = new Panel();
        waveformContainer.Children.Add(waveformDisplay);
        waveformContainer.Children.Add(waveformLoadingIndicator);
        mainStack.Children.Add(waveformContainer);

        var loopEditorGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 10, 0, 0) };
        var createLoopPanel = new StackPanel { Spacing = 5 };
        var loopNameInput = new TextBox { Watermark = "Loop Name", Foreground = _theme.B_TextColor, Background = _theme.B_ControlBackgroundColor };
        loopNameInput.Bind(TextBox.TextProperty, new Binding("NewLoopNameInput", BindingMode.TwoWay));

        var timeSettersGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,10,Auto,*"), VerticalAlignment = VerticalAlignment.Center }; // Matched XAML
        var setStartBtn = new Button { Content = "Set Start", FontSize = 10, Padding = new Thickness(3), Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor };
        setStartBtn.Bind(Button.CommandProperty, new Binding("CaptureLoopStartCandidateCommand"));
        var startDisp = new TextBlock { FontSize = 10, Margin = new Thickness(3, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
        startDisp.Bind(TextBlock.TextProperty, new Binding("NewLoopStartCandidateDisplay"));

        // Corrected Grid column attachment for setEndBtn and endDisp
        var setEndBtn = new Button { Content = "Set End", FontSize = 10, Padding = new Thickness(3), Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor };
        Grid.SetColumn(setEndBtn, 2); // Should be column 2 as per "Auto,*,[Space=10],Auto,*"
        setEndBtn.Bind(Button.CommandProperty, new Binding("CaptureLoopEndCandidateCommand"));

        var endDisp = new TextBlock { FontSize = 10, Margin = new Thickness(3, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
        Grid.SetColumn(endDisp, 3); // Should be column 3
        endDisp.Bind(TextBlock.TextProperty, new Binding("NewLoopEndCandidateDisplay"));

        Grid.SetColumn(setStartBtn, 0); Grid.SetColumn(startDisp, 1); // These were correct
        timeSettersGrid.Children.Add(setStartBtn); timeSettersGrid.Children.Add(startDisp);
        timeSettersGrid.Children.Add(setEndBtn); timeSettersGrid.Children.Add(endDisp);


        var saveLoopBtn = new Button { Content = "Save Loop", HorizontalAlignment = HorizontalAlignment.Stretch, Background = _theme.B_AccentColor, Foreground = _theme.B_AccentForeground };
        saveLoopBtn.Bind(Button.CommandProperty, new Binding("SaveNewLoopRegionCommand"));
        saveLoopBtn.Bind(Button.IsEnabledProperty, new Binding("CanSaveNewLoopRegion"));

        createLoopPanel.Children.Add(new TextBlock { Text = "Create/Edit Loop Region:", FontSize = 12, Foreground = _theme.B_TextColor });
        createLoopPanel.Children.Add(loopNameInput);
        createLoopPanel.Children.Add(timeSettersGrid);
        createLoopPanel.Children.Add(saveLoopBtn);
        Grid.SetColumn(createLoopPanel, 0);

        var manageLoopsPanel = new StackPanel { Spacing = 5, Margin = new Thickness(10, 0, 0, 0), MinWidth = 180 };
        var loopsListBox = new ListBox { MinHeight = 50, MaxHeight = 100, Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor };
        loopsListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("EditableLoopRegions"));
        loopsListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedEditableLoopRegion", BindingMode.TwoWay));
        loopsListBox.ItemTemplate = new FuncDataTemplate<LoopRegion>((loop, _) => new TextBlock { Text = loop.DisplayText, Padding = new Thickness(3), Foreground = _theme.B_TextColor });

        var loopListButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right };
        var activateBtn = new Button { Content = "Activate", FontSize = 10, Background = _theme.B_AccentColor, Foreground = _theme.B_AccentForeground };
        activateBtn.Bind(Button.CommandProperty, new Binding("ActivateLoopRegionCommand"));
        var deleteBtn = new Button { Content = "Delete", FontSize = 10, Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor };
        deleteBtn.Bind(Button.CommandProperty, new Binding("DeleteLoopRegionCommand"));
        loopListButtons.Children.Add(activateBtn); loopListButtons.Children.Add(deleteBtn);

        var deactivateBtn = new Button { Content = "Deactivate Current Loop", HorizontalAlignment = HorizontalAlignment.Stretch, FontSize = 10, Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, Margin = new Thickness(0, 5, 0, 0) };
        deactivateBtn.Bind(Button.CommandProperty, new Binding("DeactivateActiveLoopCommand"));

        manageLoopsPanel.Children.Add(new TextBlock { Text = "Defined Loops:", FontSize = 12, Foreground = _theme.B_TextColor });
        manageLoopsPanel.Children.Add(loopsListBox);
        manageLoopsPanel.Children.Add(loopListButtons);
        manageLoopsPanel.Children.Add(deactivateBtn);
        Grid.SetColumn(manageLoopsPanel, 1);

        loopEditorGrid.Children.Add(createLoopPanel); loopEditorGrid.Children.Add(manageLoopsPanel);
        mainStack.Children.Add(loopEditorGrid);
        panelRoot.Child = mainStack;
        return panelRoot;
    }

    private StackPanel CreateMainPlaybackControls()
    {
        var mainPlaybackSlider = new Slider
        {
            Name = "MainPlaybackSliderInstance",
            Minimum = 0,
            Margin = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Background = _theme.B_SecondaryTextColor,
            Foreground = _theme.B_AccentColor
        };

        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>())
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor) }
        });

        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("PlaybackService.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("PlaybackService.CurrentPositionSeconds", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(IsEnabledProperty, new Binding("PlaybackService.HasCurrentSong"));


        var mainPlayPauseButton = new Button { Content = "Play", Background = _theme.B_SlightlyLighterBackground, Foreground = _theme.B_TextColor, BorderBrush = _theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 5), MinWidth = 70 };
        mainPlayPauseButton.Click += (s, e) => { if (DataContext is MainWindowViewModel vm) { if (vm.PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Playing) vm.PlaybackService.Pause(); else vm.PlaybackService.Resume(); } }; // Check CurrentPlaybackStatus for resume/pause decision
        mainPlayPauseButton.Bind(Button.ContentProperty, new Binding("PlaybackService.CurrentPlaybackStatus") { Converter = new PlaybackStatusToPlayPauseTextConverter() }); // Use CurrentPlaybackStatus
        mainPlayPauseButton.Bind(IsEnabledProperty, new Binding("PlaybackService.HasCurrentSong"));


        var toggleAdvPanelButton = new Button { Content = "+", Background = _theme.B_SlightlyLighterBackground, Foreground = _theme.B_TextColor, BorderBrush = _theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), MinWidth = 30, FontWeight = FontWeight.Bold, Margin = new Thickness(5, 0, 0, 0) };
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        // Enable advanced panel button even if IsPlaying is false, as long as a song is selected
        toggleAdvPanelButton.Bind(IsEnabledProperty, new Binding("PlaybackService.HasCurrentSong"));


        var controlsButtonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 10, 0) };
        controlsButtonPanel.Children.Add(mainPlayPauseButton);
        controlsButtonPanel.Children.Add(toggleAdvPanelButton);

        var topMainPlaybackControls = new DockPanel { LastChildFill = true, Height = 35, Margin = new Thickness(5, 0, 5, 0) };
        DockPanel.SetDock(controlsButtonPanel, Dock.Left);
        topMainPlaybackControls.Children.Add(controlsButtonPanel);
        topMainPlaybackControls.Children.Add(mainPlaybackSlider);

        var activeLoopDisplayText = new TextBlock { Foreground = _theme.B_SecondaryTextColor, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10, 0, 10, 2), MinHeight = 14 };
        activeLoopDisplayText.Bind(TextBlock.TextProperty, new Binding("ActiveLoopDisplayText"));

        var outerPanel = new StackPanel { Orientation = Orientation.Vertical, Background = _theme.B_BackgroundColor, Margin = new Thickness(0, 5, 0, 5) };
        outerPanel.Children.Add(topMainPlaybackControls);
        outerPanel.Children.Add(activeLoopDisplayText);
        return outerPanel;
    }

    private Border CreateStatusBar()
    {
        var statusBar = new Border { Background = _theme.B_SlightlyLighterBackground, Padding = new Thickness(10, 4), Height = 26 };
        var statusBarText = new TextBlock { Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
        statusBarText.Bind(TextBlock.TextProperty, new Binding("StatusBarText"));
        statusBar.Child = statusBarText;
        return statusBar;
    }
}

// Changed to use PlaybackStateStatus for more accurate Play/Pause text
public class PlaybackStatusToPlayPauseTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is PlaybackStateStatus status)
        {
            return status == PlaybackStateStatus.Playing ? "Pause" : "Play";
        }
        return "Play"; // Default
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

public static class BrushExtensions { public static IBrush Multiply(this IBrush brush, double factor) { if (brush is ISolidColorBrush solidBrush) { var c = solidBrush.Color; return new SolidColorBrush(Color.FromArgb(c.A, (byte)Math.Clamp(c.R * factor, 0, 255), (byte)Math.Clamp(c.G * factor, 0, 255), (byte)Math.Clamp(c.B * factor, 0, 255))); } return brush; } }