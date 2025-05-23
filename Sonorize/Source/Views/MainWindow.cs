using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb, ToggleButton
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Sonorize.Controls;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels;
using Sonorize.Views.MainWindowControls;
using System; // Required for FuncDataTemplate
using System.Linq; // Required for Linq extensions
using System.ComponentModel;
using Avalonia.Interactivity; // Required for PropertyChangedEventArgs
using System.Diagnostics; // Required for Debug.WriteLine

namespace Sonorize.Views;
public class MainWindow : Window
{
    private readonly ThemeColors _theme;
    private readonly Bitmap? _defaultThumbnail; // Needed for grid template converter
    private ListBox? _songListBox; // Field to store the ListBox instance

    // Converters instances
    private readonly ViewModeToItemTemplateConverter _itemTemplateConverter;
    private readonly ViewModeToItemsPanelTemplateConverter _itemsPanelTemplateConverter;
    // Removed ViewModeToClassConverter instance as we're managing classes in code-behind


    public MainWindow(ThemeColors theme, Bitmap? defaultThumbnail = null)
    {
        _theme = theme;
        _defaultThumbnail = defaultThumbnail; // Pass default thumbnail from App if available

        // Initialize converters, passing theme and default thumbnail if necessary
        _itemTemplateConverter = new ViewModeToItemTemplateConverter(_theme, _defaultThumbnail);
        _itemsPanelTemplateConverter = ViewModeToItemsPanelTemplateConverter.Instance; // Singleton instance

        Title = "Sonorize";
        Width = 950;
        Height = 750;
        MinWidth = 700;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = _theme.B_BackgroundColor;

        var mainGrid = new Grid
        {
            RowDefinitions =
            [
                new(GridLength.Auto), // Menu
                new(GridLength.Auto), // Search Bar
                new(GridLength.Auto), // View Selector
                new(GridLength.Star), // Tab Control
                new(GridLength.Auto), // Advanced Playback Panel
                new(GridLength.Auto), // Main Playback Controls
                new(GridLength.Auto)  // Status Bar
            ]
        };

        var menu = MainMenu.Create(_theme, this);
        Grid.SetRow(menu, 0);
        mainGrid.Children.Add(menu);

        var searchBarPanel = SearchBarPanel.Create(_theme);
        Grid.SetRow(searchBarPanel, 1);
        mainGrid.Children.Add(searchBarPanel);

        var viewSelectorPanel = CreateViewSelectorPanel();
        Grid.SetRow(viewSelectorPanel, 2);
        mainGrid.Children.Add(viewSelectorPanel);


        var tabControl = CreateMainTabView(); // This will populate _songListBox
        Grid.SetRow(tabControl, 3);
        mainGrid.Children.Add(tabControl);

        var advancedPlaybackPanel = CreateAdvancedPlaybackPanel();
        advancedPlaybackPanel.Bind(Visual.IsVisibleProperty, new Binding("IsAdvancedPanelVisible"));
        Grid.SetRow(advancedPlaybackPanel, 4);
        mainGrid.Children.Add(advancedPlaybackPanel);

        var mainPlaybackControls = CreateMainPlaybackControls();
        Grid.SetRow(mainPlaybackControls, 5);
        mainGrid.Children.Add(mainPlaybackControls);

        var statusBar = CreateStatusBar();
        Grid.SetRow(statusBar, 6);
        mainGrid.Children.Add(statusBar);

        Content = mainGrid;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        // When DataContext is set (likely to MainWindowViewModel)
        if (DataContext is MainWindowViewModel vm)
        {
            // Initial sync of ListBox classes based on current view mode
            UpdateListBoxClasses(vm.Library.CurrentViewMode);

            // Subscribe to Library.CurrentViewMode changes
            vm.Library.PropertyChanged += Library_PropertyChanged;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);

        // Unsubscribe from event when the window is unloaded
        if (DataContext is MainWindowViewModel vm)
        {
            vm.Library.PropertyChanged -= Library_PropertyChanged;
        }
    }

    private void Library_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // React to changes in the LibraryViewModel that affect the UI structure/styles
        if (e.PropertyName == nameof(LibraryViewModel.CurrentViewMode))
        {
            if (DataContext is MainWindowViewModel vm)
            {
                // Update ListBox classes on the UI thread
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    UpdateListBoxClasses(vm.Library.CurrentViewMode);
                });
            }
        }
        // Other Library_PropertyChanged handling (e.g., SelectedSong) remains in MainVM
    }

    private void UpdateListBoxClasses(LibraryViewMode viewMode)
    {
        // Use the _songListBox field directly
        if (_songListBox != null)
        {
            // Clear existing view mode classes
            _songListBox.Classes.Remove("detailed-view");
            _songListBox.Classes.Remove("compact-view");
            _songListBox.Classes.Remove("grid-view");

            // Add class based on current mode
            switch (viewMode)
            {
                case LibraryViewMode.Detailed:
                    _songListBox.Classes.Add("detailed-view");
                    break;
                case LibraryViewMode.Compact:
                    _songListBox.Classes.Add("compact-view");
                    break;
                case LibraryViewMode.Grid:
                    _songListBox.Classes.Add("grid-view");
                    break;
            }
        }
        else
        {
            Debug.WriteLine("[MainWindow] UpdateListBoxClasses: _songListBox is null. Cannot update classes.");
        }
    }


    private Panel CreateViewSelectorPanel()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 10, 0), // Align with TabControl/Search bar
            Spacing = 5 // Space between label and combo box
        };

        var label = new TextBlock
        {
            Text = "View:",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _theme.B_TextColor,
            FontSize = 12
        };

        var comboBox = new ComboBox
        {
            Width = 100,
            VerticalAlignment = VerticalAlignment.Center,
            Background = _theme.B_ControlBackgroundColor,
            Foreground = _theme.B_TextColor,
            BorderBrush = _theme.B_SecondaryTextColor
        };

        // Bind to Library.AvailableViewModes and Library.CurrentViewMode
        comboBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.AvailableViewModes"));
        comboBox.Bind(ComboBox.SelectedItemProperty, new Binding("Library.CurrentViewMode", BindingMode.TwoWay));

        panel.Children.Add(label);
        panel.Children.Add(comboBox);

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
        // This binding seems incorrect based on MainVM having ActiveTabIndex property. Let's remove or correct it.
        // Assuming we want to track selected tab in MainVM, we'd need a property there. Let's leave it unbound for now.
        // tabControl.Bind(TabControl.SelectedIndexProperty, new Binding("ActiveTabIndex", BindingMode.TwoWay));


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
            Content = CreateSongListScrollViewer() // This call will assign to _songListBox
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
        // Assign to the field here
        _songListBox = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10), // Consistent margin
            Name = "SongListBox" // Name is still good for debugging or other purposes
        };

        // --- ListBoxItem Styles ---
        // Base style for any ListBoxItem
        _songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground),
                new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor),
                new Setter(ListBoxItem.PaddingProperty, new Thickness(0)), // Padding handled by item template/border
                new Setter(ListBoxItem.MinHeightProperty, 30.0), // Default minimum height for list items
                new Setter(ListBoxItem.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(ListBoxItem.VerticalAlignmentProperty, VerticalAlignment.Stretch)
            }
        });
        // PointerOver style (not selected)
        _songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) } });
        // Selected style
        _songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
            }
        });
        // Selected + PointerOver style
        _songListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
            }
        });

        // --- Styles specific to Grid View (applied when the ListBox has the 'grid-view' class) ---
        // Target ListBoxItems whose ListBox container has the class "grid-view"
        _songListBox.Styles.Add(new Style(s => s.OfType<ListBox>().Class("grid-view").Descendant().Is<ListBoxItem>())
        {
            Setters =
             {
                new Setter(ListBoxItem.WidthProperty, 140.0), // Fixed width for grid items
                new Setter(ListBoxItem.HeightProperty, 180.0), // Fixed height for grid items
                new Setter(ListBoxItem.MarginProperty, new Thickness(8)), // Margin around grid items
                new Setter(ListBoxItem.MinHeightProperty, 180.0) // Ensure min height matches height
             }
        });


        // --- ItemSource and Selection Binding ---
        // Bind to Library.FilteredSongs and Library.SelectedSong
        _songListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.FilteredSongs"));
        _songListBox.Bind(ListBox.SelectedItemProperty, new Binding("Library.SelectedSong", BindingMode.TwoWay));

        // --- Dynamic ItemTemplate and ItemsPanelTemplate Binding ---
        // Use converters to switch templates based on Library.CurrentViewMode
        _songListBox.Bind(ItemsControl.ItemTemplateProperty, new Binding("Library.CurrentViewMode") { Converter = _itemTemplateConverter });
        _songListBox.Bind(ItemsControl.ItemsPanelProperty, new Binding("Library.CurrentViewMode") { Converter = _itemsPanelTemplateConverter });

        // --- Class update handled in code-behind based on DataContext property change ---
        // Removed: _songListBox.Classes.Bind(...)


        return new ScrollViewer { Content = _songListBox, Padding = new Thickness(0, 0, 0, 5) };
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

        // Bind to Library.Artists and Library.SelectedArtist
        artistsListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.Artists"));
        artistsListBox.Bind(ListBox.SelectedItemProperty, new Binding("Library.SelectedArtist", BindingMode.TwoWay));


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

        // Bind to Library.Albums and Library.SelectedAlbum
        albumsListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.Albums"));
        albumsListBox.Bind(ListBox.SelectedItemProperty, new Binding("Library.SelectedAlbum", BindingMode.TwoWay));

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
            MinHeight = 180,
            ClipToBounds = true
        };
        var mainStack = new StackPanel { Spacing = 10 };

        var speedPitchGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,15,Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 5) };
        var speedLabel = new TextBlock { Text = "Tempo:", VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_TextColor, Margin = new Thickness(0, 0, 5, 0) };
        var speedSlider = new Slider { Minimum = 0.5, Maximum = 2.0, SmallChange = 0.05, LargeChange = 0.25, TickFrequency = 0.25, Foreground = _theme.B_AccentColor, Background = _theme.B_SecondaryTextColor };
        speedSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor) } });
        // Bind speed/pitch to Playback.PlaybackSpeed/Pitch
        speedSlider.Bind(Slider.ValueProperty, new Binding("Playback.PlaybackSpeed", BindingMode.TwoWay));
        var speedDisplay = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0), Foreground = _theme.B_TextColor, MinWidth = 35, HorizontalAlignment = HorizontalAlignment.Right };
        // Bind speed/pitch display to Playback.PlaybackSpeedDisplay/PitchDisplay
        speedDisplay.Bind(TextBlock.TextProperty, new Binding("Playback.PlaybackSpeedDisplay"));

        var pitchLabel = new TextBlock { Text = "Pitch:", VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_TextColor, Margin = new Thickness(0, 0, 5, 0) };
        var pitchSlider = new Slider { Minimum = -4, Maximum = 4, SmallChange = 0.1, LargeChange = 0.5, TickFrequency = 0.5, Foreground = _theme.B_AccentColor, Background = _theme.B_SecondaryTextColor };
        pitchSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor) } });
        // Bind speed/pitch to Playback.PlaybackSpeed/Pitch
        pitchSlider.Bind(Slider.ValueProperty, new Binding("Playback.PlaybackPitch", BindingMode.TwoWay));
        var pitchDisplay = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0), Foreground = _theme.B_TextColor, MinWidth = 45, HorizontalAlignment = HorizontalAlignment.Right };
        // Bind speed/pitch display to Playback.PlaybackSpeedDisplay/PitchDisplay
        pitchDisplay.Bind(TextBlock.TextProperty, new Binding("Playback.PlaybackPitchDisplay"));

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
        // Bind waveform data/state to Playback.Waveform properties
        waveformDisplay.Bind(WaveformDisplayControl.WaveformPointsProperty, new Binding("Playback.WaveformRenderData"));
        // Bind to PlaybackService properties via Playback property
        waveformDisplay.Bind(WaveformDisplayControl.CurrentPositionProperty, new Binding("Playback.CurrentPosition"));
        waveformDisplay.Bind(WaveformDisplayControl.DurationProperty, new Binding("Playback.CurrentSongDuration"));
        waveformDisplay.Bind(WaveformDisplayControl.ActiveLoopProperty, new Binding("PlaybackService.CurrentSong.SavedLoop")); // Active loop is on the Song model itself
        // Bind WaveformSeekCommand to the one in LoopEditorViewModel
        waveformDisplay.SeekRequested += (s, time) => { if (DataContext is MainWindowViewModel vm) vm.LoopEditor.WaveformSeekCommand.Execute(time); };


        var waveformLoadingIndicator = new ProgressBar { IsIndeterminate = true, Height = 5, Margin = new Thickness(0, -5, 0, 0), Foreground = _theme.B_AccentColor, Background = Brushes.Transparent };
        // Bind loading indicator visibility to Playback.IsWaveformLoading
        waveformLoadingIndicator.Bind(Visual.IsVisibleProperty, new Binding("Playback.IsWaveformLoading"));
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
        // Bind commands to LoopEditor property
        setStartBtn.Bind(Button.CommandProperty, new Binding("LoopEditor.CaptureLoopStartCandidateCommand"));
        var startDisp = new TextBlock { FontSize = 11, Margin = new Thickness(3, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, MinWidth = 60 };
        // Bind display text to LoopEditor property
        startDisp.Bind(TextBlock.TextProperty, new Binding("LoopEditor.NewLoopStartCandidateDisplay"));

        var setEndBtn = new Button { Content = "B", FontSize = 12, Padding = new Thickness(10, 5), MinWidth = 40, Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor };
        // Bind commands to LoopEditor property
        setEndBtn.Bind(Button.CommandProperty, new Binding("LoopEditor.CaptureLoopEndCandidateCommand"));
        var endDisp = new TextBlock { FontSize = 11, Margin = new Thickness(3, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, MinWidth = 60 };
        // Bind display text to LoopEditor property
        endDisp.Bind(TextBlock.TextProperty, new Binding("LoopEditor.NewLoopEndCandidateDisplay"));

        var saveLoopBtn = new Button { Content = "Save Loop", FontSize = 11, Padding = new Thickness(10, 5), Background = _theme.B_AccentColor, Foreground = _theme.B_AccentForeground };
        // Bind commands to LoopEditor property
        saveLoopBtn.Bind(Button.CommandProperty, new Binding("LoopEditor.SaveLoopCommand"));
        // Bind CanExecute state to LoopEditor property
        saveLoopBtn.Bind(Button.IsEnabledProperty, new Binding("LoopEditor.CanSaveLoopRegion"));

        var clearLoopBtn = new Button { Content = "Clear Loop", FontSize = 11, Padding = new Thickness(10, 5), Background = _theme.B_ControlBackgroundColor, Foreground = _theme.B_TextColor };
        // Bind commands to LoopEditor property
        clearLoopBtn.Bind(Button.CommandProperty, new Binding("LoopEditor.ClearLoopCommand"));
        // The IsEnabled binding for clear loop checks if a loop exists on the *currently playing* song.
        var clearLoopBinding = new Binding("PlaybackService.CurrentSong.SavedLoop") // Can still bind directly to PlaybackService via MainVM property
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

        var loopActiveTogglePanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0),
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var loopActiveCheckBox = new CheckBox
        {
            Content = "Activate Loop",
            Foreground = _theme.B_TextColor,
            VerticalAlignment = VerticalAlignment.Center
        };
        // Bind CheckBox IsChecked to LoopEditor property
        loopActiveCheckBox.Bind(ToggleButton.IsCheckedProperty, new Binding("LoopEditor.IsCurrentLoopActiveUiBinding", BindingMode.TwoWay));

        // IsEnabled binding for the checkbox should check if a loop exists on the *currently playing* song.
        var loopActiveCheckBoxIsEnabledBinding = new Binding("PlaybackService.CurrentSong.SavedLoop") // Can still bind directly to PlaybackService via MainVM property
        {
            Converter = NotNullToBooleanConverter.Instance
        };
        loopActiveCheckBox.Bind(IsEnabledProperty, loopActiveCheckBoxIsEnabledBinding);


        loopActiveTogglePanel.Children.Add(loopActiveCheckBox);

        loopControlsOuterPanel.Children.Add(loopDefinitionLabel);
        loopControlsOuterPanel.Children.Add(loopActionsPanel);
        loopControlsOuterPanel.Children.Add(loopActiveTogglePanel);

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
            VerticalAlignment = VerticalAlignment.Center,
            Background = _theme.B_SecondaryTextColor,
            Foreground = _theme.B_AccentColor
            // Margin removed, will be handled by DockPanel spacing or parent margin
        };
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor) } });
        // Bind to Playback.CurrentSongDurationSeconds and Playback.CurrentPositionSeconds
        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay)); // Bind 2-way for seeking
        // IsEnabled depends on Playback.HasCurrentSong
        mainPlaybackSlider.Bind(IsEnabledProperty, new Binding("Playback.HasCurrentSong"));


        var mainPlayPauseButton = new Button { Content = "Play", Background = _theme.B_SlightlyLighterBackground, Foreground = _theme.B_TextColor, BorderBrush = _theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 5), MinWidth = 70 };
        // Bind command to Playback.PlayPauseResumeCommand
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        // Bind content to Playback.IsPlaying
        var playPauseContentBinding = new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseTextConverter.Instance };
        mainPlayPauseButton.Bind(Button.ContentProperty, playPauseContentBinding);
        // IsEnabled depends on Playback.HasCurrentSong (handled by command CanExecute)
        // mainPlayPauseButton.Bind(IsEnabledProperty, new Binding("Playback.HasCurrentSong")); // Handled by Command CanExecute


        var toggleAdvPanelButton = new Button { Content = "+", Background = _theme.B_SlightlyLighterBackground, Foreground = _theme.B_TextColor, BorderBrush = _theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), MinWidth = 30, FontWeight = FontWeight.Bold };
        // This command is on MainVM
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        // IsEnabled should check if a song is selected or playing - depends on Playback.HasCurrentSong
        toggleAdvPanelButton.Bind(IsEnabledProperty, new Binding("Playback.HasCurrentSong"));


        var controlsButtonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0) // 5px right margin to space from slider
        };
        controlsButtonPanel.Children.Add(mainPlayPauseButton); controlsButtonPanel.Children.Add(toggleAdvPanelButton);

        var timeDisplayTextBlock = new TextBlock
        {
            Foreground = _theme.B_TextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0), // 8px left margin to space from slider
            MinWidth = 75 // "00:00 / 00:00"
        };
        // Bind to Playback.CurrentTimeTotalTimeDisplay
        timeDisplayTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentTimeTotalTimeDisplay"));
        // IsVisible depends on Playback.HasCurrentSong
        timeDisplayTextBlock.Bind(IsVisibleProperty, new Binding("Playback.HasCurrentSong"));


        var topMainPlaybackControls = new DockPanel
        {
            LastChildFill = true,
            Height = 35,
            Margin = new Thickness(10, 0) // Overall horizontal padding for the control group
        };
        DockPanel.SetDock(controlsButtonPanel, Dock.Left);
        DockPanel.SetDock(timeDisplayTextBlock, Dock.Right);

        topMainPlaybackControls.Children.Add(controlsButtonPanel);
        topMainPlaybackControls.Children.Add(timeDisplayTextBlock);
        topMainPlaybackControls.Children.Add(mainPlaybackSlider); // Added last to fill remaining space

        var activeLoopDisplayText = new TextBlock { Foreground = _theme.B_SecondaryTextColor, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10, 0, 10, 2), MinHeight = 14 };
        // Bind loop display text to LoopEditor property
        activeLoopDisplayText.Bind(TextBlock.TextProperty, new Binding("LoopEditor.ActiveLoopDisplayText"));

        var outerPanel = new StackPanel { Orientation = Orientation.Vertical, Background = _theme.B_BackgroundColor, Margin = new Thickness(0, 5, 0, 5) };
        outerPanel.Children.Add(topMainPlaybackControls); outerPanel.Children.Add(activeLoopDisplayText);
        return outerPanel;
    }

    private Border CreateStatusBar()
    {
        var statusBar = new Border { Background = _theme.B_SlightlyLighterBackground, Padding = new Thickness(10, 4), Height = 26 };
        var statusBarText = new TextBlock { Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
        // Bind status bar text to MainVM property
        statusBarText.Bind(TextBlock.TextProperty, new Binding("StatusBarText"));
        statusBar.Child = statusBarText;
        return statusBar;
    }
}