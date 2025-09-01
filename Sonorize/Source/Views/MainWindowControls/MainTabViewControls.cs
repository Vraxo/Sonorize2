using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels;
using Sonorize.ViewModels.LibraryManagement;
using System.Linq;

namespace Sonorize.Views.MainWindowControls;

public class MainTabViewControls
{
    private readonly ThemeColors _theme;
    private readonly SharedViewTemplates _sharedViewTemplates;
    private ListBox? _songListBoxInstance;
    private ListBox? _artistsListBoxInstance;
    private ListBox? _albumsListBoxInstance;
    private ListBox? _playlistsListBoxInstance;

    public MainTabViewControls(ThemeColors theme, SharedViewTemplates sharedViewTemplates)
    {
        _theme = theme;
        _sharedViewTemplates = sharedViewTemplates;
    }

    public TabControl CreateMainTabView(out ListBox songListBox, out ListBox artistsListBox, out ListBox albumsListBox, out ListBox playlistsListBox)
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

        var (songListScrollViewer, slb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "SongListBox", "Library.FilteredSongs", "Library.SelectedSong",
            _sharedViewTemplates.SongTemplates.DetailedSongTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate,
            lb => _songListBoxInstance = lb);
        _songListBoxInstance = slb;

        var libraryTabContent = new DockPanel();
        var libraryHeaderGrid = CreateLibraryHeaderGrid(_theme); // Note: Grid is created without horizontal margins here

        // Wrapper Border for the header, handling all horizontal margins and scrollbar compensation
        var headerWrapper = new Border
        {
            Child = libraryHeaderGrid,
            Background = libraryHeaderGrid.Background, // Transfer background from grid to wrapper
            Margin = new Thickness(10, 0, 10, 0), // Outer margin to align with ListBox's outer content area
            MinHeight = libraryHeaderGrid.MinHeight // Inherit MinHeight
        };
        libraryHeaderGrid.Background = Brushes.Transparent; // Make inner grid transparent if wrapper takes background

        // Bind the wrapper's *right padding* to the scrollbar's actual width
        // This effectively shrinks the content area of the header by the scrollbar's width
        // without affecting its left alignment.
        headerWrapper.Bind(Border.PaddingProperty, new Binding("TemplateSettings.VerticalScrollBarActualWidth")
        {
            Source = songListScrollViewer,
            Converter = new FuncValueConverter<double, Thickness>(width => new Thickness(0, 0, width, 0))
        });

        DockPanel.SetDock(headerWrapper, Dock.Top);
        libraryTabContent.Children.Add(headerWrapper);
        libraryTabContent.Children.Add(songListScrollViewer);

        var libraryTab = new TabItem
        {
            Header = "LIBRARY",
            Content = libraryTabContent
        };

        var (artistsListScrollViewer, alb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "ArtistsListBox", "Library.Groupings.Artists", "Library.FilterState.SelectedArtist",
            _sharedViewTemplates.ArtistTemplates.DetailedArtistTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate,
            lb => _artistsListBoxInstance = lb);
        _artistsListBoxInstance = alb;

        var artistsTab = new TabItem
        {
            Header = "ARTISTS",
            Content = artistsListScrollViewer
        };

        var (albumsListScrollViewer, alblb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "AlbumsListBox", "Library.Groupings.Albums", "Library.FilterState.SelectedAlbum",
            _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate,
            lb => _albumsListBoxInstance = lb);
        _albumsListBoxInstance = alblb;

        var albumsTab = new TabItem
        {
            Header = "ALBUMS",
            Content = albumsListScrollViewer
        };

        var (playlistsListScrollViewer, plb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "PlaylistsListBox", "Library.Groupings.Playlists", "Library.FilterState.SelectedPlaylist",
            _sharedViewTemplates.DetailedPlaylistTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate,
            lb => _playlistsListBoxInstance = lb);
        _playlistsListBoxInstance = plb;

        var playlistsTab = new TabItem
        {
            Header = "PLAYLISTS",
            Content = playlistsListScrollViewer
        };

        tabControl.Items.Add(libraryTab);
        tabControl.Items.Add(artistsTab);
        tabControl.Items.Add(albumsTab);
        tabControl.Items.Add(playlistsTab);

        songListBox = _songListBoxInstance!;
        artistsListBox = _artistsListBoxInstance!;
        albumsListBox = _albumsListBoxInstance!;
        playlistsListBox = _playlistsListBoxInstance!;
        return tabControl;
    }

    public void UpdateListViewMode(SongDisplayMode mode, ListBox listBox, IDataTemplate detailedTemplate, IDataTemplate compactTemplate, IDataTemplate gridTemplate)
    {
        if (listBox == null)
        {
            Debug.WriteLine($"[MainTabViewControls] UpdateListViewMode called but target ListBox is null.");
            return;
        }

        Debug.WriteLine($"[MainTabViewControls] Applying display mode: {mode} to ListBox: {listBox.Name}");
        var scrollViewer = listBox.Parent as ScrollViewer;

        switch (mode)
        {
            case SongDisplayMode.Detailed:
                listBox.ItemTemplate = detailedTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.StackPanelItemsPanelTemplate;
                if (scrollViewer is not null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
            case SongDisplayMode.Compact:
                listBox.ItemTemplate = compactTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.StackPanelItemsPanelTemplate;
                if (scrollViewer is not null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
            case SongDisplayMode.Grid:
                listBox.ItemTemplate = gridTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.WrapPanelItemsPanelTemplate;
                if (scrollViewer is not null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
        }
    }

    private Grid CreateLibraryHeaderGrid(ThemeColors theme)
    {
        var headerGrid = new Grid
        {
            Background = theme.B_SlightlyLighterBackground,
            // Margin is now handled by the parent wrapper Border
            MinHeight = 30
        };

        // This proxy element will safely get the DataContext from an ancestor once the UI is attached.
        var proxy = new Border { Name = "DataContextProxy", IsVisible = false };
        proxy.Bind(Border.TagProperty, new Binding("DataContext")
        {
            RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) }
        });
        headerGrid.Children.Add(proxy);

        // Bindings now source from the proxy's Tag property, which will hold the main view model.
        headerGrid.Bind(Visual.IsVisibleProperty, new Binding("Tag.Library.LibraryViewMode")
        {
            Source = proxy,
            FallbackValue = false,
            Converter = new FuncValueConverter<SongDisplayMode, bool>(m => m == SongDisplayMode.Detailed || m == SongDisplayMode.Compact)
        });

        var columns = headerGrid.ColumnDefinitions;

        // Column 0: Image (fixed width)
        var imageCol = new ColumnDefinition();
        imageCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.LibraryViewMode")
        {
            Source = proxy,
            FallbackValue = new GridLength(0),
            Converter = new FuncValueConverter<SongDisplayMode, GridLength>(m => m == SongDisplayMode.Detailed ? new GridLength(32 + 10 + 10) : new GridLength(10))
        });
        columns.Add(imageCol);

        // Column 1: Title (star-sized)
        var titleCol = new ColumnDefinition();
        titleCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.LibraryViewMode")
        {
            Source = proxy,
            FallbackValue = new GridLength(2, GridUnitType.Star),
            Converter = new FuncValueConverter<SongDisplayMode, GridLength>(m => m == SongDisplayMode.Detailed ? new GridLength(3, GridUnitType.Star) : new GridLength(2, GridUnitType.Star))
        });
        columns.Add(titleCol);

        // Column 2: Artist (star-sized, visibility-controlled)
        var artistCol = new ColumnDefinition();
        artistCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowArtist") { Source = proxy, FallbackValue = GridLength.Parse("1.5*"), Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "1.5*" });
        columns.Add(artistCol);

        // Column 3: Album (star-sized, visibility-controlled)
        var albumCol = new ColumnDefinition();
        albumCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowAlbum") { Source = proxy, FallbackValue = GridLength.Parse("1.5*"), Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "1.5*" });
        columns.Add(albumCol);

        // Column 4: Spacer (star-sized)
        columns.Add(new ColumnDefinition(GridLength.Star));

        // Column 5: Play Count (Auto-sized, visibility-controlled)
        var playCountCol = new ColumnDefinition();
        playCountCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowPlayCount") { Source = proxy, FallbackValue = new GridLength(0), Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "Auto" });
        columns.Add(playCountCol);

        // Column 6: Date Added (Auto-sized, visibility-controlled)
        var dateAddedCol = new ColumnDefinition();
        dateAddedCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowDateAdded") { Source = proxy, FallbackValue = new GridLength(0), Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "Auto" });
        columns.Add(dateAddedCol);

        // Column 7: Duration (Auto-sized, visibility-controlled)
        var durationCol = new ColumnDefinition();
        durationCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowDuration") { Source = proxy, FallbackValue = GridLength.Auto, Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "Auto" });
        columns.Add(durationCol);

        int currentColIndex = 0; // Start at 0 for actual grid column placement

        // Image column content (if detailed)
        if (proxy.DataContext is MainWindowViewModel vm && vm.Library.LibraryDisplayModeService.LibraryViewMode == SongDisplayMode.Detailed)
        {
            // No direct header content for the image column itself, just increment.
            currentColIndex++;
        }
        else // Compact mode, first column in item template is just 10px padding
        {
            currentColIndex++; // Skip the 10px padding column that exists in compact mode for the song items.
        }

        // Apply corrected CreateHeaderButton for Title, Artist, Album, etc.
        headerGrid.Children.Add(CreateHeaderButton(theme, "Title", SortProperty.Title, currentColIndex++, proxy));
        headerGrid.Children.Add(CreateHeaderButton(theme, "Artist", SortProperty.Artist, currentColIndex++, proxy));
        headerGrid.Children.Add(CreateHeaderButton(theme, "Album", SortProperty.Album, currentColIndex++, proxy));

        currentColIndex++; // Skip spacer column (Column 4)

        headerGrid.Children.Add(CreateHeaderButton(theme, "Plays", SortProperty.PlayCount, currentColIndex++, proxy, HorizontalAlignment.Right));
        headerGrid.Children.Add(CreateHeaderButton(theme, "Date Added", SortProperty.DateAdded, currentColIndex++, proxy, HorizontalAlignment.Right));
        headerGrid.Children.Add(CreateHeaderButton(theme, "Duration", SortProperty.Duration, currentColIndex++, proxy, HorizontalAlignment.Right));

        return headerGrid;
    }

    private Button CreateHeaderButton(ThemeColors theme, string text, SortProperty sortProperty, int column, Border proxy, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var button = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            // For left-aligned headers, force HorizontalAlignment.Left to prevent button's visual area from stretching.
            // For right-aligned headers, HorizontalAlignment.Right is appropriate.
            HorizontalAlignment = (alignment == HorizontalAlignment.Left) ? HorizontalAlignment.Left : HorizontalAlignment.Right,
            // HorizontalContentAlignment positions the _content_ (StackPanel) within the button's own bounds.
            HorizontalContentAlignment = alignment
        };
        button.Bind(Button.CommandProperty, new Binding("Tag.Library.SortCommand") { Source = proxy });
        button.CommandParameter = sortProperty;
        Grid.SetColumn(button, column);

        // Define button's margin and textblock's padding based on the specific header
        Thickness buttonMargin = new Thickness(0); // Default to no margin on the button
        Thickness textBlockPadding = new Thickness(0); // Default to no padding on the text block

        switch (sortProperty)
        {
            case SortProperty.Title:
                // User feedback: Title header needs to move left by 10 pixels.
                // Resetting its internal TextBlock padding to 0, since it had 10px before.
                buttonMargin = new Thickness(0);
                textBlockPadding = new Thickness(0, 0, 0, 0);
                Debug.WriteLine($"[MainTabViewControls] Title header: ButtonMargin={buttonMargin}, TextPadding={textBlockPadding}");
                break;
            case SortProperty.Artist:
                // Artist configuration from previous step (text aligned, but button shape too wide left)
                // Retain: buttonMargin = 0, textBlockPadding = 20,0,0,0
                // User feedback for "extra mass on the left" implied that the button's interactive area was too wide left.
                // Setting HorizontalAlignment.Left and Margin=0 on the button means the button's interactive area
                // will hug its content. The padding then pushes the text.
                buttonMargin = new Thickness(0);
                textBlockPadding = new Thickness(20, 0, 0, 0);
                Debug.WriteLine($"[MainTabViewControls] Artist header: ButtonMargin={buttonMargin}, TextPadding={textBlockPadding}");
                break;
            case SortProperty.Album:
                // Album configuration from previous step.
                buttonMargin = new Thickness(0);
                textBlockPadding = new Thickness(10, 0, 0, 0);
                Debug.WriteLine($"[MainTabViewControls] Album header: ButtonMargin={buttonMargin}, TextPadding={textBlockPadding}");
                break;
            case SortProperty.PlayCount:
            case SortProperty.DateAdded:
            case SortProperty.Duration:
                // Right-aligned headers from previous step.
                buttonMargin = new Thickness(0);
                textBlockPadding = new Thickness(0, 0, 10, 0);
                Debug.WriteLine($"[MainTabViewControls] Right-aligned header '{text}': ButtonMargin={buttonMargin}, TextPadding={textBlockPadding}");
                break;
        }

        button.Margin = buttonMargin; // Apply the calculated margin to the button

        var contentPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = theme.B_SecondaryTextColor,
            Padding = textBlockPadding // Apply the calculated padding to the text block
        };
        contentPanel.Children.Add(textBlock); // Add the now-configured textBlock

        var sortIndicator = new TextBlock
        {
            FontSize = 10,
            FontWeight = FontWeight.Bold,
            Foreground = theme.B_TextColor,
            VerticalAlignment = VerticalAlignment.Center
        };

        var visibilityBinding = new Binding("Tag.Library.CurrentSortProperty")
        {
            Source = proxy,
            FallbackValue = false,
            Converter = new FuncValueConverter<SortProperty, bool>(p => p == sortProperty)
        };
        sortIndicator.Bind(Visual.IsVisibleProperty, visibilityBinding);
        sortIndicator.Bind(TextBlock.TextProperty, new Binding("Tag.Library.CurrentSortDirection")
        {
            Source = proxy,
            FallbackValue = SortDirection.Ascending,
            Converter = new FuncValueConverter<SortDirection, string>(d => d == SortDirection.Ascending ? "▲" : "▼")
        });

        contentPanel.Children.Add(sortIndicator);
        button.Content = contentPanel;

        return button;
    }
}