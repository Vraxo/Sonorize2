using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views.MainWindowControls;

public class MainTabViewControls
{
    private readonly ThemeColors _theme;
    private readonly SharedViewTemplates _sharedViewTemplates;

    // View controls for each tab
    private Control _songsDetailedView, _songsCompactView, _songsGridView;
    private Control _artistsDetailedView, _artistsCompactView, _artistsGridView;
    private Control _albumsDetailedView, _albumsCompactView, _albumsGridView;
    private Control _playlistsDetailedView, _playlistsCompactView, _playlistsGridView;


    public MainTabViewControls(ThemeColors theme, SharedViewTemplates sharedViewTemplates)
    {
        _theme = theme;
        _sharedViewTemplates = sharedViewTemplates;
        CreateAllViews();
    }

    private void CreateAllViews()
    {
        // Library (Song) Views
        _songsDetailedView = _sharedViewTemplates.DataGridTemplates.CreateSongsDataGrid("Library.FilteredSongs", "Library.SelectedSong");
        _songsCompactView = _sharedViewTemplates.DataGridTemplates.CreateCompactSongsDataGrid("Library.FilteredSongs", "Library.SelectedSong");
        var (songsGridScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "SongGridListBox", "Library.FilteredSongs", "Library.SelectedSong",
            _sharedViewTemplates.SongTemplates.GridSongTemplate, _sharedViewTemplates.WrapPanelItemsPanelTemplate,
            lb => { });
        _songsGridView = songsGridScrollViewer;

        // Artists Views
        _artistsDetailedView = _sharedViewTemplates.DataGridTemplates.CreateArtistsDataGrid("Library.Groupings.Artists", "Library.FilterState.SelectedArtist");
        var (artistsCompactScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "ArtistCompactListBox", "Library.Groupings.Artists", "Library.FilterState.SelectedArtist",
            _sharedViewTemplates.ArtistTemplates.CompactArtistTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate,
            lb => { });
        _artistsCompactView = artistsCompactScrollViewer;
        var (artistsGridScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "ArtistsGridListBox", "Library.Groupings.Artists", "Library.FilterState.SelectedArtist",
            _sharedViewTemplates.ArtistTemplates.GridArtistTemplate, _sharedViewTemplates.WrapPanelItemsPanelTemplate,
             lb => { });
        _artistsGridView = artistsGridScrollViewer;

        // Albums Views
        _albumsDetailedView = _sharedViewTemplates.DataGridTemplates.CreateAlbumsDataGrid("Library.Groupings.Albums", "Library.FilterState.SelectedAlbum");
        var (albumsCompactScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "AlbumCompactListBox", "Library.Groupings.Albums", "Library.FilterState.SelectedAlbum",
            _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate,
             lb => { });
        _albumsCompactView = albumsCompactScrollViewer;
        var (albumsGridScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "AlbumsGridListBox", "Library.Groupings.Albums", "Library.FilterState.SelectedAlbum",
            _sharedViewTemplates.GridAlbumTemplate, _sharedViewTemplates.WrapPanelItemsPanelTemplate,
            lb => { });
        _albumsGridView = albumsGridScrollViewer;

        // Playlists Views
        _playlistsDetailedView = _sharedViewTemplates.DataGridTemplates.CreatePlaylistsDataGrid("Library.Groupings.Playlists", "Library.FilterState.SelectedPlaylist");
        var (playlistsCompactScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "PlaylistCompactListBox", "Library.Groupings.Playlists", "Library.FilterState.SelectedPlaylist",
            _sharedViewTemplates.CompactPlaylistTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate,
            lb => { });
        _playlistsCompactView = playlistsCompactScrollViewer;
        var (playlistsGridScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "PlaylistsGridListBox", "Library.Groupings.Playlists", "Library.FilterState.SelectedPlaylist",
            _sharedViewTemplates.GridPlaylistTemplate, _sharedViewTemplates.WrapPanelItemsPanelTemplate,
            lb => { });
        _playlistsGridView = playlistsGridScrollViewer;
    }


    public TabControl CreateMainTabView()
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

        var viewModeToIntConverter = new FuncValueConverter<SongDisplayMode, int>(mode => (int)mode);

        // Library Tab
        var libraryCarousel = new Carousel { ItemsSource = new Control[] { _songsDetailedView, _songsCompactView, _songsGridView } };
        libraryCarousel.Bind(Carousel.SelectedIndexProperty, new Binding("Library.LibraryViewMode", BindingMode.OneWay) { Converter = viewModeToIntConverter });
        var libraryTab = new TabItem { Header = "LIBRARY", Content = libraryCarousel };

        // Artists Tab
        var artistsCarousel = new Carousel { ItemsSource = new Control[] { _artistsDetailedView, _artistsCompactView, _artistsGridView } };
        artistsCarousel.Bind(Carousel.SelectedIndexProperty, new Binding("Library.ArtistViewMode", BindingMode.OneWay) { Converter = viewModeToIntConverter });
        var artistsTab = new TabItem { Header = "ARTISTS", Content = artistsCarousel };

        // Albums Tab
        var albumsCarousel = new Carousel { ItemsSource = new Control[] { _albumsDetailedView, _albumsCompactView, _albumsGridView } };
        albumsCarousel.Bind(Carousel.SelectedIndexProperty, new Binding("Library.AlbumViewMode", BindingMode.OneWay) { Converter = viewModeToIntConverter });
        var albumsTab = new TabItem { Header = "ALBUMS", Content = albumsCarousel };

        // Playlists Tab
        var playlistsCarousel = new Carousel { ItemsSource = new Control[] { _playlistsDetailedView, _playlistsCompactView, _playlistsGridView } };
        playlistsCarousel.Bind(Carousel.SelectedIndexProperty, new Binding("Library.PlaylistViewMode", BindingMode.OneWay) { Converter = viewModeToIntConverter });
        var playlistsTab = new TabItem { Header = "PLAYLISTS", Content = playlistsCarousel };

        tabControl.Items.Add(libraryTab);
        tabControl.Items.Add(artistsTab);
        tabControl.Items.Add(albumsTab);
        tabControl.Items.Add(playlistsTab);

        return tabControl;
    }

    public void UpdateListViewMode(string viewName, SongDisplayMode mode)
    {
        // This method is now obsolete as the Carousel handles view switching via binding.
        // It is no longer called by MainWindow.
    }
}