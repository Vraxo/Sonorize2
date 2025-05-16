using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models;
using Sonorize.ViewModels;
using System.Diagnostics;
using Avalonia.Styling;
using Avalonia.Media.Imaging;

namespace Sonorize.Views;

public class MainWindow : Window
{
    private ListBox _songListBox;
    private readonly ThemeColors _theme;
    private Panel _loopEditorPanel; // Reference to the loop editor panel

    public MainWindow(ThemeColors theme)
    {
        _theme = theme;
        Title = "Sonorize"; Width = 900; Height = 700; MinWidth = 600; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = _theme.B_BackgroundColor;

        // --- Menu Bar (Same as before) ---
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

        // --- Song List (Same as before, minor style adjustments for theme) ---
        _songListBox = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10),
            Styles = {
                new Style(x => x.OfType<ListBoxItem>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground), new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor) }},
                new Style(x => x.OfType<ListBoxItem>().Class(":pointerover")) { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) }},
                new Style(x => x.OfType<ListBoxItem>().Class(":selected")) { Setters = { new Setter(TemplatedControl.BackgroundProperty, BrushExtensions.Multiply(_theme.B_AccentColor, 0.7)), new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground) }},
                new Style(x => x.OfType<ListBoxItem>().Class(":selected").Class(":pointerover")) { Setters = { new Setter(TemplatedControl.BackgroundProperty, BrushExtensions.Multiply(_theme.B_AccentColor, 0.8)), new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground) }}
            }
        };
        _songListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Songs"));
        _songListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedSong", BindingMode.TwoWay));
        _songListBox.ItemTemplate = new FuncDataTemplate<Song>((song, scope) => {
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
        var scrollViewer = new ScrollViewer { Content = _songListBox, Padding = new Thickness(0, 0, 0, 5) };

        // --- Playback Slider ---
        var playbackSlider = new Slider { Minimum = 0, Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center, Background = _theme.B_SecondaryTextColor, Foreground = _theme.B_AccentColor };
        playbackSlider.Styles.Add(new Style(x => x.OfType<Slider>().Descendant().OfType<Thumb>()) { Setters = { new Setter(BackgroundProperty, _theme.B_AccentColor) } });
        playbackSlider.Bind(Slider.MaximumProperty, new Binding("PlaybackService.CurrentSongDurationSeconds"));
        playbackSlider.Bind(Slider.ValueProperty, new Binding("PlaybackService.CurrentPositionSeconds", BindingMode.TwoWay));

        // --- Play/Pause Button ---
        var playPauseButton = new Button { Content = "Play/Pause", Background = _theme.B_SlightlyLighterBackground, Foreground = _theme.B_TextColor, BorderBrush = _theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 5), MinWidth = 80 };
        playPauseButton.Click += (s, e) => { if (DataContext is MainWindowViewModel vm) { if (vm.PlaybackService.IsPlaying) vm.PlaybackService.Pause(); else if (vm.PlaybackService.CurrentSong != null) vm.PlaybackService.Resume(); else if (vm.Songs.Any()) vm.SelectedSong = vm.Songs.First(); /* Triggers play */ } };

        // --- Open Loop Editor Button ---
        var openLoopEditorButton = new Button { Content = "+ Loops", Background = _theme.B_SlightlyLighterBackground, Foreground = _theme.B_TextColor, BorderBrush = _theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), FontSize = 10, MinWidth = 60, Margin = new Thickness(5, 0, 0, 0) };
        openLoopEditorButton.Bind(Button.CommandProperty, new Binding("OpenLoopEditorCommand"));

        var playbackButtonsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        playbackButtonsPanel.Children.Add(playPauseButton);
        playbackButtonsPanel.Children.Add(openLoopEditorButton);

        var topPlaybackControls = new DockPanel { LastChildFill = true, Height = 35 };
        DockPanel.SetDock(playbackButtonsPanel, Dock.Left);
        topPlaybackControls.Children.Add(playbackButtonsPanel);
        topPlaybackControls.Children.Add(playbackSlider);

        // --- Active Loop Display Text ---
        var activeLoopDisplayText = new TextBlock { Foreground = _theme.B_SecondaryTextColor, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10, 2, 10, 0), MinHeight = 14 };
        activeLoopDisplayText.Bind(TextBlock.TextProperty, new Binding("ActiveLoopDisplayText"));

        var outerPlaybackControlsPanel = new StackPanel { Orientation = Orientation.Vertical, Background = _theme.B_BackgroundColor, Margin = new Thickness(5, 0, 5, 5) };
        outerPlaybackControlsPanel.Children.Add(topPlaybackControls);
        outerPlaybackControlsPanel.Children.Add(activeLoopDisplayText);

        // --- Status Bar (Same as before) ---
        var statusBar = new Border { Background = _theme.B_SlightlyLighterBackground, Padding = new Thickness(10, 4), Height = 26 };
        var statusBarText = new TextBlock { Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
        statusBarText.Bind(TextBlock.TextProperty, new Binding("StatusBarText"));
        statusBar.Child = statusBarText;

        // --- Loop Editor Panel ---
        _loopEditorPanel = CreateLoopEditorPanel();
        _loopEditorPanel.Bind(Visual.IsVisibleProperty, new Binding("IsLoopEditorVisible"));


        // --- Main Layout Grid (to allow overlay) ---
        var mainGrid = new Grid();
        var mainContentDockPanel = new DockPanel(); // Existing layout goes here
        DockPanel.SetDock(menu, Dock.Top);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        DockPanel.SetDock(outerPlaybackControlsPanel, Dock.Bottom);
        mainContentDockPanel.Children.Add(menu);
        mainContentDockPanel.Children.Add(statusBar);
        mainContentDockPanel.Children.Add(outerPlaybackControlsPanel);
        mainContentDockPanel.Children.Add(scrollViewer); // Fills remaining space

        mainGrid.Children.Add(mainContentDockPanel);
        mainGrid.Children.Add(_loopEditorPanel); // Add editor panel on top

        Content = mainGrid;
        Debug.WriteLine("[MainView] Constructor finished.");
    }

    private Panel CreateLoopEditorPanel()
    {
        Color loopEditorBackgroundColor = Colors.Transparent; // Fallback
        if (_theme.B_SlightlyLighterBackground is ISolidColorBrush solidBrush)
        {
            loopEditorBackgroundColor = solidBrush.Color;
        }

        var panel = new Panel
        {
            // Use the extracted color with opacity for the background
            Background = new SolidColorBrush(loopEditorBackgroundColor, 0.95),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ZIndex = 100 // Ensure it's on top
        };

        var editorContentBorder = new Border
        {
            Background = _theme.B_BackgroundColor,
            BorderBrush = _theme.B_AccentColor,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(15),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 400,
            MaxWidth = 500,
            MinHeight = 350,
            MaxHeight = 500
        };

        var editorStackPanel = new StackPanel { Spacing = 10 };

        // Header
        var editorHeader = new TextBlock { FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = _theme.B_TextColor, HorizontalAlignment = HorizontalAlignment.Center };
        editorHeader.Bind(TextBlock.TextProperty, new Binding("PlaybackService.CurrentSong.Title") { StringFormat = "Loop Editor: {0}" });


        // ListBox for existing loops
        var loopsListBox = new ListBox
        {
            MinHeight = 100,
            MaxHeight = 150,
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor,
            BorderThickness = new Thickness(1)
        };
        loopsListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("EditableLoopRegions"));
        loopsListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedEditableLoopRegion", BindingMode.TwoWay));
        loopsListBox.ItemTemplate = new FuncDataTemplate<LoopRegion>(
            (loop, scope) => new TextBlock { Text = loop.DisplayText, Foreground = _theme.B_TextColor, Padding = new Thickness(5) },
            supportsRecycling: true);

        var listManagementButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Right };
        var activateButton = new Button { Content = "Activate", Background = _theme.B_AccentColor, Foreground = _theme.B_AccentForeground, Padding = new Thickness(8, 4), FontSize = 10 };
        activateButton.Bind(Button.CommandProperty, new Binding("ActivateLoopRegionCommand"));
        var deleteButton = new Button { Content = "Delete", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, Padding = new Thickness(8, 4), FontSize = 10 };
        deleteButton.Bind(Button.CommandProperty, new Binding("DeleteLoopRegionCommand"));
        listManagementButtons.Children.Add(activateButton);
        listManagementButtons.Children.Add(deleteButton);


        // Create New Loop Section
        var createHeader = new TextBlock { Text = "Create New Loop:", FontSize = 14, FontWeight = FontWeight.Medium, Foreground = _theme.B_TextColor, Margin = new Thickness(0, 10, 0, 0) };
        var nameInput = new TextBox { Watermark = "Loop Name", Foreground = _theme.B_TextColor, Background = _theme.B_ControlBackgroundColor, BorderBrush = _theme.B_SecondaryTextColor };
        nameInput.Bind(TextBox.TextProperty, new Binding("NewLoopNameInput", BindingMode.TwoWay));

        var timeSettersGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") }; // Removed ColumnSpacing
        var startSetterPanel = new StackPanel { Spacing = 3, Margin = new Thickness(0, 0, 5, 0) }; // Added margin for spacing
        var setStartButton = new Button { Content = "Set Start", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, FontSize = 10, Padding = new Thickness(5) };
        setStartButton.Bind(Button.CommandProperty, new Binding("CaptureLoopStartCandidateCommand"));
        var startDisplay = new TextBlock { Foreground = _theme.B_SecondaryTextColor, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
        startDisplay.Bind(TextBlock.TextProperty, new Binding("NewLoopStartCandidateDisplay"));
        startSetterPanel.Children.Add(setStartButton); startSetterPanel.Children.Add(startDisplay);

        var endSetterPanel = new StackPanel { Spacing = 3, Margin = new Thickness(5, 0, 0, 0) }; // Added margin for spacing
        var setEndButton = new Button { Content = "Set End", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, FontSize = 10, Padding = new Thickness(5) };
        setEndButton.Bind(Button.CommandProperty, new Binding("CaptureLoopEndCandidateCommand"));
        var endDisplay = new TextBlock { Foreground = _theme.B_SecondaryTextColor, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center };
        endDisplay.Bind(TextBlock.TextProperty, new Binding("NewLoopEndCandidateDisplay"));
        endSetterPanel.Children.Add(setEndButton); endSetterPanel.Children.Add(endDisplay);

        Grid.SetColumn(startSetterPanel, 0); Grid.SetColumn(endSetterPanel, 1);
        timeSettersGrid.Children.Add(startSetterPanel); timeSettersGrid.Children.Add(endSetterPanel);

        var saveLoopButton = new Button { Content = "Save New Loop", Background = _theme.B_AccentColor, Foreground = _theme.B_AccentForeground, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 5, 0, 0) };
        saveLoopButton.Bind(Button.CommandProperty, new Binding("SaveNewLoopRegionCommand"));

        // Editor Actions
        var editorActionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
        var deactivateLoopButton = new Button { Content = "Deactivate Current Loop", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, Padding = new Thickness(10, 5) };
        deactivateLoopButton.Bind(Button.CommandProperty, new Binding("DeactivateActiveLoopCommand"));
        var closeEditorButton = new Button { Content = "Close Editor", Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor, Padding = new Thickness(10, 5) };
        closeEditorButton.Bind(Button.CommandProperty, new Binding("CloseLoopEditorCommand"));
        editorActionsPanel.Children.Add(deactivateLoopButton);
        editorActionsPanel.Children.Add(closeEditorButton);

        editorStackPanel.Children.Add(editorHeader);
        editorStackPanel.Children.Add(new TextBlock { Text = "Defined Loops:", FontSize = 12, Foreground = _theme.B_SecondaryTextColor });
        editorStackPanel.Children.Add(loopsListBox);
        editorStackPanel.Children.Add(listManagementButtons);
        editorStackPanel.Children.Add(new Separator { Margin = new Thickness(0, 5) });
        editorStackPanel.Children.Add(createHeader);
        editorStackPanel.Children.Add(nameInput);
        editorStackPanel.Children.Add(timeSettersGrid);
        editorStackPanel.Children.Add(saveLoopButton);
        editorStackPanel.Children.Add(editorActionsPanel);

        editorContentBorder.Child = editorStackPanel; // This is correct: Border has a Child property
        panel.Children.Add(editorContentBorder);     // Corrected: Panel uses Children collection
        return panel;
    }
}

// BrushExtensions (Same as before)
public static class BrushExtensions { public static IBrush Multiply(this IBrush brush, double factor) { if (brush is ISolidColorBrush solidBrush) { var c = solidBrush.Color; return new SolidColorBrush(Color.FromArgb(c.A, (byte)Math.Clamp(c.R * factor, 0, 255), (byte)Math.Clamp(c.G * factor, 0, 255), (byte)Math.Clamp(c.B * factor, 0, 255))); } return brush; } }