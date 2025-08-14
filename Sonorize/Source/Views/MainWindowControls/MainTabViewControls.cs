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
using Sonorize.Models;
using Sonorize.ViewModels;

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

        var libraryTab = new TabItem
        {
            Header = "LIBRARY",
            Content = songListScrollViewer // The content is now just the scroll viewer
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
}