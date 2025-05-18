using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models;
using Sonorize.ViewModels;
using Avalonia.Data;
using Sonorize.Controls;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives; // For Thumb, ToggleButton
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

        var searchBarPanel = CreateSearchBarPanel();
        Grid.SetRow(searchBarPanel, 1);
        mainGrid.Children.Add(searchBarPanel);

        var tabControl = CreateMainTabView();
        Grid.SetRow(tabControl, 2);
        mainGrid.Children.Add(tabControl);

        var advancedPlaybackPanel = CreateAdvancedPlaybackPanel();
        advancedPlaybackPanel.Bind(Visual.IsVisibleProperty, new Binding("IsAdvancedPanelVisible"));
        Grid.SetRow(advancedPlaybackPanel, 3);
        mainGrid.Children.Add(advancedPlaybackPanel);

        var mainPlaybackControls = CreateMainPlaybackControls();
        Grid.SetRow(mainPlaybackControls, 4);
        mainGrid.Children.Add(mainPlaybackControls);

        var statusBar = CreateStatusBar();
        Grid.SetRow(statusBar, 5);
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

    private Panel CreateSearchBarPanel()
    {
        var searchBox = new TextBox
        {
            Watermark = "Search songs by title, artist, or album...",
            Margin = new Thickness(10, 5, 10, 5),
            Padding = new Thickness(10, 7),
            Background = _theme.B_SlightlyLighterBackground,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            FontSize = 14
        };
        searchBox.Bind(TextBox.TextProperty, new Binding("SearchQuery", BindingMode.TwoWay));
        searchBox.Styles.Add(new Style(s => s.Is<TextBox>().Class(":focus"))
        {
            Setters = { new Setter(TextBox.BorderBrushProperty, _theme.B_AccentColor) }
        });

        var panel = new Panel
        {
            Children = { searchBox },
            Margin = new Thickness(0, 5, 0, 0)
        };
        return panel;
    }

    private TabControl CreateMainTabView()
    {
        var tabControl = new TabControl
        {
            Background = _theme.B_BackgroundColor,
            Margin = new Thickness(10, 5, 10, 5),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };
        tabControl.Bind(TabControl.SelectedIndexProperty, new Binding("ActiveTabIndex", BindingMode.TwoWay));


        var tabItemStyle = new Style(s => s.Is<TabItem>());
        tabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, _theme.B_BackgroundColor));
        tabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, _theme.B_SecondaryTextColor));
        tabItemStyle.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(12, 7)));
        tabItemStyle.Setters.Add(new Setter(TabItem.FontSizeProperty, 13.0));
        tabItemStyle.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeight.SemiBold));
        tabItemStyle.Setters.Add(new Setter(TabItem.BorderThicknessProperty, new Thickness(0)));
        tabItemStyle.Setters.Add(new Setter(TabItem.BorderBrushProperty, Brushes.Transparent));


        var selectedTabItemStyle = new Style(s => s.Is<TabItem>().Class(":selected"));
        selectedTabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, _theme.B_BackgroundColor));
        selectedTabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, _theme.B_TextColor));

        var pointerOverTabItemStyle = new Style(s => s.Is<TabItem>().Class(":pointerover").Not(x => x.Class(":selected")));
        pointerOverTabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, _theme.B_SlightlyLighterBackground));
        pointerOverTabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, _theme.B_TextColor));

        tabControl.Styles.Add(tabItemStyle);
        tabControl.Styles.Add(selectedTabItemStyle);
        tabControl.Styles.Add(pointerOverTabItemStyle);

        var libraryTab = new TabItem
        {
            Header = "LIBRARY",
            Content = CreateSongListScrollViewer()
        };

        var artistsTab = new TabItem
        {
            Header = "ARTISTS",
            Content = CreateArtistsListScrollViewer()
        };

        var albumsTab = new TabItem
        {
            Header = "ALBUMS",
            Content = CreateAlbumsListScrollViewer()
        };

        tabControl.Items.Add(libraryTab);
        tabControl.Items.Add(artistsTab);
        tabControl.Items.Add(albumsTab);

        return tabControl;
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
        {
            Setters = {
            new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground),
            new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor)
        }
        });
        songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) } });
        songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
        {
            Setters = {
            new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
            new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
        }
        });
        songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
        {
            Setters = {
            new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
            new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
        }
        });

        songListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("FilteredSongs"));
        songListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedSong", BindingMode.TwoWay));

        songListBox.ItemTemplate = new FuncDataTemplate<Song>((song, nameScope) => {
            var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 5, 0), Source = song.Thumbnail, Stretch = Stretch.UniformToFill };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
            var titleBlock = new TextBlock { Text = song.Title, FontSize = 14, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
            var artistBlock = new TextBlock { Text = song.Artist, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            var durationBlock = new TextBlock { Text = song.DurationString, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Children = { titleBlock, artistBlock } };
            var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center, Children = { image, textStack, durationBlock } };
            Grid.SetColumn(image, 0); Grid.SetColumn(textStack, 1); Grid.SetColumn(durationBlock, 2);
            return new Border { Padding = new Thickness(10, 6, 10, 6), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
        }, supportsRecycling: true);

        return new ScrollViewer { Content = songListBox, Padding = new Thickness(0, 0, 0, 5) };
    }

    private ScrollViewer CreateArtistsListScrollViewer()
    {
        var artistsListBox = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10)
        };

        artistsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>())
        {
            Setters = {
            new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground),
            new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor)
        }
        });
        artistsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) } });
        artistsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
        {
            Setters = {
            new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
            new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
        }
        });
        artistsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
        {
            Setters = {
            new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
            new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
        }
        });

        artistsListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Artists"));
        artistsListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedArtist", BindingMode.TwoWay));


        artistsListBox.ItemTemplate = new FuncDataTemplate<ArtistViewModel>((artistVM, nameScope) =>
        {
            var image = new Image
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(5, 0, 10, 0),
                Source = artistVM.Thumbnail,
                Stretch = Stretch.UniformToFill
            };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

            var artistNameBlock = new TextBlock
            {
                Text = artistVM.Name,
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center
            };

            var itemGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            itemGrid.Children.Add(image);
            itemGrid.Children.Add(artistNameBlock);
            Grid.SetColumn(image, 0);
            Grid.SetColumn(artistNameBlock, 1);

            return new Border
            {
                Padding = new Thickness(10, 8),
                MinHeight = 44,
                Background = Brushes.Transparent,
                Child = itemGrid
            };
        }, supportsRecycling: true);

        return new ScrollViewer { Content = artistsListBox, Padding = new Thickness(0, 0, 0, 5) };
    }

    private ScrollViewer CreateAlbumsListScrollViewer()
    {
        var albumsListBox = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10)
        };

        albumsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>())
        {
            Setters = {
            new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground),
            new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor)
        }
        });
        albumsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) } });
        albumsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
        {
            Setters = {
            new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
            new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
        }
        });
        albumsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
        {
            Setters = {
            new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
            new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
        }
        });

        albumsListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Albums"));
        albumsListBox.Bind(ListBox.SelectedItemProperty, new Binding("SelectedAlbum", BindingMode.TwoWay));

        albumsListBox.ItemTemplate = new FuncDataTemplate<AlbumViewModel>((albumVM, nameScope) =>
        {
            var image = new Image
            {
                Width = 32,
                Height = 32,
                Margin = new Thickness(5, 0, 10, 0),
                Source = albumVM.Thumbnail,
                Stretch = Stretch.UniformToFill
            };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

            var albumTitleBlock = new TextBlock
            {
                Text = albumVM.Title,
                FontSize = 14,
                FontWeight = FontWeight.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };
            var albumArtistBlock = new TextBlock
            {
                Text = albumVM.Artist,
                FontSize = 11,
                Foreground = _theme.B_SecondaryTextColor,
                VerticalAlignment = VerticalAlignment.Center
            };
            var textStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { albumTitleBlock, albumArtistBlock }
            };


            var itemGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            itemGrid.Children.Add(image);
            itemGrid.Children.Add(textStack);
            Grid.SetColumn(image, 0);
            Grid.SetColumn(textStack, 1);

            return new Border
            {
                Padding = new Thickness(10, 6),
                MinHeight = 44,
                Background = Brushes.Transparent,
                Child = itemGrid
            };
        }, supportsRecycling: true);

        return new ScrollViewer { Content = albumsListBox, Padding = new Thickness(0, 0, 0, 5) };
    }


    private Border CreateAdvancedPlaybackPanel()
    {
        var panelRoot = new Border
        {
            Background = _theme.B_SlightlyLighterBackground,
            Padding = new Thickness(10),
            BorderBrush = _theme.B_AccentColor,
            BorderThickness = new Thickness(0, 1, 0, 1),
            MinHeight = 180, // Adjusted to accommodate new checkbox
            ClipToBounds = true
        };
        var mainStack = new StackPanel { Spacing = 10 };

        var speedPitchGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,15,Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 5) };
        var speedLabel = new TextBlock { Text = "Tempo:", VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_TextColor, Margin = new Thickness(0, 0, 5, 0) };
        var speedSlider = new Slider { Minimum = 0.5, Maximum = 2.0, SmallChange = 0.05, LargeChange = 0.25, TickFrequency = 0.25, Foreground = _theme.B_AccentColor, Background = _theme.B_SecondaryTextColor };
        speedSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor) } });
        speedSlider.Bind(Slider.ValueProperty, new Binding("PlaybackSpeed", BindingMode.TwoWay));
        var speedDisplay = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0), Foreground = _theme.B_TextColor, MinWidth = 35, HorizontalAlignment = HorizontalAlignment.Right };
        speedDisplay.Bind(TextBlock.TextProperty, new Binding("PlaybackSpeedDisplay"));

        var pitchLabel = new TextBlock { Text = "Pitch:", VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_TextColor, Margin = new Thickness(0, 0, 5, 0) };
        var pitchSlider = new Slider { Minimum = -4, Maximum = 4, SmallChange = 0.1, LargeChange = 0.5, TickFrequency = 0.5, Foreground = _theme.B_AccentColor, Background = _theme.B_SecondaryTextColor };
        pitchSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor) } });
        pitchSlider.Bind(Slider.ValueProperty, new Binding("PlaybackPitch", BindingMode.TwoWay));
        var pitchDisplay = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0), Foreground = _theme.B_TextColor, MinWidth = 45, HorizontalAlignment = HorizontalAlignment.Right };
        pitchDisplay.Bind(TextBlock.TextProperty, new Binding("PlaybackPitchDisplay"));

        Grid.SetColumn(speedLabel, 0); Grid.SetColumn(speedSlider, 1); Grid.SetColumn(speedDisplay, 2);
        Grid.SetColumn(pitchLabel, 4); Grid.SetColumn(pitchSlider, 5); Grid.SetColumn(pitchDisplay, 6);
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
        waveformDisplay.Bind(WaveformDisplayControl.ActiveLoopProperty, new Binding("PlaybackService.CurrentSong.SavedLoop"));
        waveformDisplay.SeekRequested += (s, time) => { if (DataContext is MainWindowViewModel vm) vm.WaveformSeekCommand.Execute(time); };

        var waveformLoadingIndicator = new ProgressBar { IsIndeterminate = true, Height = 5, Margin = new Thickness(0, -5, 0, 0), Foreground = _theme.B_AccentColor, Background = Brushes.Transparent };
        waveformLoadingIndicator.Bind(Visual.IsVisibleProperty, new Binding("IsWaveformLoading"));
        var waveformContainer = new Panel();
        waveformContainer.Children.Add(waveformDisplay); waveformContainer.Children.Add(waveformLoadingIndicator);
        mainStack.Children.Add(waveformContainer);

        var loopControlsOuterPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 5,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var loopDefinitionLabel = new TextBlock
        {
            Text = "Define Loop:",
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Foreground = _theme.B_TextColor
        };

        var loopActionsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var setStartBtn = new Button { Content = "A", FontSize = 12, Padding = new Thickness(10, 5), MinWidth = 40, Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor };
        setStartBtn.Bind(Button.CommandProperty, new Binding("CaptureLoopStartCandidateCommand"));
        var startDisp = new TextBlock { FontSize = 11, Margin = new Thickness(3, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, MinWidth = 60 };
        startDisp.Bind(TextBlock.TextProperty, new Binding("NewLoopStartCandidateDisplay"));

        var setEndBtn = new Button { Content = "B", FontSize = 12, Padding = new Thickness(10, 5), MinWidth = 40, Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor };
        setEndBtn.Bind(Button.CommandProperty, new Binding("CaptureLoopEndCandidateCommand"));
        var endDisp = new TextBlock { FontSize = 11, Margin = new Thickness(3, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, MinWidth = 60 };
        endDisp.Bind(TextBlock.TextProperty, new Binding("NewLoopEndCandidateDisplay"));

        var saveLoopBtn = new Button { Content = "Save Loop", FontSize = 11, Padding = new Thickness(10, 5), Background = _theme.B_AccentColor, Foreground = _theme.B_AccentForeground };
        saveLoopBtn.Bind(Button.CommandProperty, new Binding("SaveLoopCommand"));
        saveLoopBtn.Bind(Button.IsEnabledProperty, new Binding("CanSaveLoopRegion"));

        var clearLoopBtn = new Button { Content = "Clear Loop", FontSize = 11, Padding = new Thickness(10, 5), Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor };
        clearLoopBtn.Bind(Button.CommandProperty, new Binding("ClearLoopCommand"));
        var clearLoopBinding = new Binding("PlaybackService.CurrentSong.SavedLoop")
        {
            Converter = NotNullToBooleanConverter.Instance
        };
        clearLoopBtn.Bind(Button.IsEnabledProperty, clearLoopBinding);


        loopActionsPanel.Children.Add(setStartBtn);
        loopActionsPanel.Children.Add(startDisp);
        loopActionsPanel.Children.Add(setEndBtn);
        loopActionsPanel.Children.Add(endDisp);
        loopActionsPanel.Children.Add(saveLoopBtn);
        loopActionsPanel.Children.Add(clearLoopBtn);

        // Panel for the Loop Active CheckBox
        var loopActiveTogglePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0), // Added some top margin
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var loopActiveCheckBox = new CheckBox
        {
            Content = "Activate Loop",
            Foreground = _theme.B_TextColor,
            VerticalAlignment = VerticalAlignment.Center
        };
        loopActiveCheckBox.Bind(ToggleButton.IsCheckedProperty, new Binding("IsCurrentLoopActiveUiBinding", BindingMode.TwoWay));

        var loopActiveCheckBoxIsEnabledBinding = new Binding("PlaybackService.CurrentSong.SavedLoop")
        {
            Converter = NotNullToBooleanConverter.Instance
        };
        loopActiveCheckBox.Bind(IsEnabledProperty, loopActiveCheckBoxIsEnabledBinding);

        loopActiveTogglePanel.Children.Add(loopActiveCheckBox);

        loopControlsOuterPanel.Children.Add(loopDefinitionLabel);
        loopControlsOuterPanel.Children.Add(loopActionsPanel);
        loopControlsOuterPanel.Children.Add(loopActiveTogglePanel); // Add the new CheckBox panel here

        mainStack.Children.Add(loopControlsOuterPanel);
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
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor) } });
        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("PlaybackService.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("PlaybackService.CurrentPositionSeconds", BindingMode.OneWay)); // Changed to OneWay
        mainPlaybackSlider.Bind(IsEnabledProperty, new Binding("PlaybackService.HasCurrentSong"));

        mainPlaybackSlider.PointerReleased += (sender, args) =>
        {
            if (sender is Slider slider && DataContext is MainWindowViewModel vm) // vm declared once here
            {
                if (vm.PlaybackService.CurrentSong != null)
                {
                    if (vm.MainSliderSeekCommand.CanExecute(slider.Value))
                    {
                        vm.MainSliderSeekCommand.Execute(slider.Value);
                        Debug.WriteLine($"[MainView] Slider PointerReleased: Value {slider.Value}, Command executed.");
                    }
                    else
                    {
                        Debug.WriteLine($"[MainView] Slider PointerReleased: Value {slider.Value}, Command CANNOT execute (Duration: {vm.PlaybackService.CurrentSongDuration.TotalSeconds}).");
                    }
                }
                else // CurrentSong is null
                {
                    Debug.WriteLine($"[MainView] Slider PointerReleased: No current song, seek ignored.");
                }
            }
        };

        var mainPlayPauseButton = new Button { Content = "Play", Background = _theme.B_SlightlyLighterBackground, Foreground = _theme.B_TextColor, BorderBrush = _theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 5), MinWidth = 70 };
        mainPlayPauseButton.Click += (s, e) =>
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Playing)
                    vm.PlaybackService.Pause();
                else
                    vm.PlaybackService.Resume();
            }
        };

        var playPauseContentBinding = new Binding("PlaybackService.IsPlaying")
        {
            Converter = BooleanToPlayPauseTextConverter.Instance
        };
        mainPlayPauseButton.Bind(Button.ContentProperty, playPauseContentBinding);
        mainPlayPauseButton.Bind(IsEnabledProperty, new Binding("PlaybackService.HasCurrentSong"));

        var toggleAdvPanelButton = new Button { Content = "+", Background = _theme.B_SlightlyLighterBackground, Foreground = _theme.B_TextColor, BorderBrush = _theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), MinWidth = 30, FontWeight = FontWeight.Bold, Margin = new Thickness(5, 0, 0, 0) };
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        toggleAdvPanelButton.Bind(IsEnabledProperty, new Binding("PlaybackService.HasCurrentSong"));

        var controlsButtonPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 10, 0) };
        controlsButtonPanel.Children.Add(mainPlayPauseButton); controlsButtonPanel.Children.Add(toggleAdvPanelButton);

        var topMainPlaybackControls = new DockPanel { LastChildFill = true, Height = 35, Margin = new Thickness(5, 0, 5, 0) };
        DockPanel.SetDock(controlsButtonPanel, Dock.Left);
        topMainPlaybackControls.Children.Add(controlsButtonPanel); topMainPlaybackControls.Children.Add(mainPlaybackSlider);

        var activeLoopDisplayText = new TextBlock { Foreground = _theme.B_SecondaryTextColor, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10, 0, 10, 2), MinHeight = 14 };
        activeLoopDisplayText.Bind(TextBlock.TextProperty, new Binding("ActiveLoopDisplayText"));

        var outerPanel = new StackPanel { Orientation = Orientation.Vertical, Background = _theme.B_BackgroundColor, Margin = new Thickness(0, 5, 0, 5) };
        outerPanel.Children.Add(topMainPlaybackControls); outerPanel.Children.Add(activeLoopDisplayText);
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

public class BooleanToPlayPauseTextConverter : IValueConverter
{
    public static readonly BooleanToPlayPauseTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isPlaying) return isPlaying ? "Pause" : "Play";
        return "Play"; // Default
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

public class NotNullToBooleanConverter : IValueConverter
{
    public static readonly NotNullToBooleanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

// This extension was here, assuming it's used or intended for use.
// If not, it can be removed. For now, keeping it as it was in the original file.
public static class BrushExtensions
{
    public static IBrush Multiply(this IBrush brush, double factor)
    {
        if (brush is ISolidColorBrush solidBrush)
        {
            var c = solidBrush.Color;
            return new SolidColorBrush(Color.FromArgb(c.A, (byte)Math.Clamp(c.R * factor, 0, 255), (byte)Math.Clamp(c.G * factor, 0, 255), (byte)Math.Clamp(c.B * factor, 0, 255)));
        }
        return brush;
    }
}