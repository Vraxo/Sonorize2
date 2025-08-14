using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views.MainWindowControls;

public class MainTabViewControls
{
    private readonly ThemeColors _theme;
    private readonly SharedViewTemplates _sharedViewTemplates;

    // Presenters for each tab's content
    private ContentControl _libraryContentPresenter;
    private ContentControl _artistsContentPresenter;
    private ContentControl _albumsContentPresenter;
    private ContentControl _playlistsContentPresenter;

    // View controls for each tab (DataGrid and Grid ListBox)
    private Control _songsDataGrid, _artistsDataGrid, _albumsDataGrid, _playlistsDataGrid;
    private Control _songsGridView, _artistsGridView, _albumsGridView, _playlistsGridView;


    public MainTabViewControls(ThemeColors theme, SharedViewTemplates sharedViewTemplates)
    {
        _theme = theme;
        _sharedViewTemplates = sharedViewTemplates;
        CreateAllViews();
    }

    private void CreateAllViews()
    {
        // Library Views
        _songsDataGrid = _sharedViewTemplates.DataGridTemplates.CreateSongsDataGrid("Library.FilteredSongs", "Library.SelectedSong");
        var (songsScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "SongListBox", "Library.FilteredSongs", "Library.SelectedSong",
            _sharedViewTemplates.SongTemplates.GridSongTemplate, _sharedViewTemplates.WrapPanelItemsPanelTemplate,
            lb => { /* Callback no longer needed here */ });
        _songsGridView = songsScrollViewer;

        // Artists Views
        _artistsDataGrid = _sharedViewTemplates.DataGridTemplates.CreateArtistsDataGrid("Library.Groupings.Artists", "Library.FilterState.SelectedArtist");
        var (artistsScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "ArtistsListBox", "Library.Groupings.Artists", "Library.FilterState.SelectedArtist",
            _sharedViewTemplates.ArtistTemplates.GridArtistTemplate, _sharedViewTemplates.WrapPanelItemsPanelTemplate,
             lb => { });
        _artistsGridView = artistsScrollViewer;

        // Albums Views
        _albumsDataGrid = _sharedViewTemplates.DataGridTemplates.CreateAlbumsDataGrid("Library.Groupings.Albums", "Library.FilterState.SelectedAlbum");
        var (albumsScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "AlbumsListBox", "Library.Groupings.Albums", "Library.FilterState.SelectedAlbum",
            _sharedViewTemplates.GridAlbumTemplate, _sharedViewTemplates.WrapPanelItemsPanelTemplate,
            lb => { });
        _albumsGridView = albumsScrollViewer;

        // Playlists Views
        _playlistsDataGrid = _sharedViewTemplates.DataGridTemplates.CreatePlaylistsDataGrid("Library.Groupings.Playlists", "Library.FilterState.SelectedPlaylist");
        var (playlistsScrollViewer, _) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "PlaylistsListBox", "Library.Groupings.Playlists", "Library.FilterState.SelectedPlaylist",
            _sharedViewTemplates.GridPlaylistTemplate, _sharedViewTemplates.WrapPanelItemsPanelTemplate,
            lb => { });
        _playlistsGridView = playlistsScrollViewer;
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

        // Create ContentPresenters for each tab
        _libraryContentPresenter = new ContentControl();
        var libraryTab = new TabItem { Header = "LIBRARY", Content = _libraryContentPresenter };

        _artistsContentPresenter = new ContentControl();
        var artistsTab = new TabItem { Header = "ARTISTS", Content = _artistsContentPresenter };

        _albumsContentPresenter = new ContentControl();
        var albumsTab = new TabItem { Header = "ALBUMS", Content = _albumsContentPresenter };

        _playlistsContentPresenter = new ContentControl();
        var playlistsTab = new TabItem { Header = "PLAYLISTS", Content = _playlistsContentPresenter };

        tabControl.Items.Add(libraryTab);
        tabControl.Items.Add(artistsTab);
        tabControl.Items.Add(albumsTab);
        tabControl.Items.Add(playlistsTab);

        return tabControl;
    }

    public void UpdateListViewMode(string viewName, SongDisplayMode mode)
    {
        bool useDataGrid = mode == SongDisplayMode.Detailed || mode == SongDisplayMode.Compact;
        Debug.WriteLine($"[MainTabViewControls] Updating view '{viewName}' to mode '{mode}'. Use DataGrid: {useDataGrid}");

        switch (viewName)
        {
            case "Library":
                _libraryContentPresenter.Content = useDataGrid ? _songsDataGrid : _songsGridView;
                break;
            case "Artists":
                _artistsContentPresenter.Content = useDataGrid ? _artistsDataGrid : _artistsGridView;
                break;
            case "Albums":
                _albumsContentPresenter.Content = useDataGrid ? _albumsDataGrid : _albumsGridView;
                break;
            case "Playlists":
                _playlistsContentPresenter.Content = useDataGrid ? _playlistsDataGrid : _playlistsGridView;
                break;
        }
    }
}