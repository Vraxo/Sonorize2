using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Sonorize.Models;
using Sonorize.ViewModels;
using Sonorize.ViewModels.LibraryManagement;

namespace Sonorize.Views.MainWindowControls;

public class MainTabViewControls
{
    private readonly ThemeColors _theme;
    private readonly SharedViewTemplates _sharedViewTemplates;

    public MainTabViewControls(ThemeColors theme, SharedViewTemplates sharedViewTemplates)
    {
        _theme = theme;
        _sharedViewTemplates = sharedViewTemplates;
    }

    public TabControl CreateMainTabView(
        out ListBox artistsListBox,
        out ListBox albumsListBox,
        out ListBox playlistsListBox)
    {
        var tabControl = new TabControl
        {
            Background = _theme.B_BackgroundColor,
            Margin = new Thickness(10, 5, 10, 5),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };

        // Ensure DataGrid has its control theme in this subtree (belt-and-suspenders)
        tabControl.Styles.Add(new StyleInclude(new Uri("avares://Application"))
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml")
        });

        tabControl.Bind(TabControl.SelectedIndexProperty, new Binding("ActiveTabIndex", BindingMode.TwoWay));

        // TabItem styling
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

        // --- LIBRARY tab (DataGrid directly; no outer ScrollViewer) ---
        var libraryDataGrid = CreateLibraryDataGrid(_theme);
        var libraryTab = new TabItem
        {
            Header = "LIBRARY",
            Content = libraryDataGrid
        };

        // --- ARTISTS tab ---
        var (artistsListScrollViewer, artistsLb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme,
            _sharedViewTemplates,
            "ArtistsListBox",
            "Library.Groupings.Artists",
            "Library.FilterState.SelectedArtist",
            _sharedViewTemplates.DetailedArtistTemplate,
            _sharedViewTemplates.StackPanelItemsPanelTemplate,
            lb => { });
        artistsListBox = artistsLb;

        var artistsTab = new TabItem
        {
            Header = "ARTISTS",
            Content = artistsListScrollViewer
        };

        // --- ALBUMS tab ---
        var (albumsListScrollViewer, albumsLb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme,
            _sharedViewTemplates,
            "AlbumsListBox",
            "Library.Groupings.Albums",
            "Library.FilterState.SelectedAlbum",
            _sharedViewTemplates.DetailedAlbumTemplate,
            _sharedViewTemplates.StackPanelItemsPanelTemplate,
            lb => { });
        albumsListBox = albumsLb;

        var albumsTab = new TabItem
        {
            Header = "ALBUMS",
            Content = albumsListScrollViewer
        };

        // --- PLAYLISTS tab ---
        var (playlistsListScrollViewer, playlistsLb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme,
            _sharedViewTemplates,
            "PlaylistsListBox",
            "Library.Groupings.Playlists",
            "Library.FilterState.SelectedPlaylist",
            _sharedViewTemplates.DetailedPlaylistTemplate,
            _sharedViewTemplates.StackPanelItemsPanelTemplate,
            lb => { });
        playlistsListBox = playlistsLb;

        var playlistsTab = new TabItem
        {
            Header = "PLAYLISTS",
            Content = playlistsListScrollViewer
        };

        // Populate tabs via Items.Add (Items is read-only, but the collection supports Add)
        tabControl.Items.Add(libraryTab);
        tabControl.Items.Add(artistsTab);
        tabControl.Items.Add(albumsTab);
        tabControl.Items.Add(playlistsTab);

        // Debug: see when TabControl is measured
        tabControl.AttachedToVisualTree += (_, __) =>
        {
            Debug.WriteLine("[MainTabViewControls] TabControl attached. Size: " +
                            $"{tabControl.Bounds.Width}x{tabControl.Bounds.Height}");
        };

        return tabControl;
    }

    public void UpdateListViewMode(
        SongDisplayMode mode,
        ListBox listBox,
        IDataTemplate detailedTemplate,
        IDataTemplate compactTemplate,
        IDataTemplate gridTemplate)
    {
        if (listBox == null)
        {
            Debug.WriteLine("[MainTabViewControls] UpdateListViewMode called but target ListBox is null.");
            return;
        }

        Debug.WriteLine($"[MainTabViewControls] Applying display mode: {mode} to ListBox: {listBox.Name}");

        var scrollViewer = listBox.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault()
                           ?? listBox.Parent as ScrollViewer;

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

    private DataGrid CreateLibraryDataGrid(ThemeColors theme)
    {
        var dataGrid = new DataGrid
        {
            Name = "LibraryDataGrid",

            // Make it impossible to miss visually while debugging
            Background = theme.B_ListBoxBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = Brushes.Magenta,
            BorderThickness = new Thickness(2),
            MinHeight = 250,

            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.All,
            RowHeight = 36,
            IsReadOnly = true,
            CanUserSortColumns = true
        };

        // Bindings relative to MainWindowViewModel
        dataGrid.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.FilteredSongs"));
        dataGrid.Bind(DataGrid.SelectedItemProperty, new Binding("Library.SelectedSong", BindingMode.TwoWay));

        // Columns
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Title",
            Binding = new Binding("Title"),
            Width = new DataGridLength(3, DataGridLengthUnitType.Star)
        });

        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Artist",
            Binding = new Binding("Artist"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Album",
            Binding = new Binding("Album"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star)
        });

        // Debug hooks
        dataGrid.AttachedToVisualTree += (_, __) =>
        {
            Debug.WriteLine("[MainTabViewControls] LibraryDataGrid attached.");
            // Force template application and log row/col presenters existence
            dataGrid.ApplyTemplate();
            Debug.WriteLine("[MainTabViewControls] Template applied.");
        };

        dataGrid.LayoutUpdated += (_, __) =>
        {
            var b = dataGrid.Bounds;
            Debug.WriteLine($"[MainTabViewControls] DataGrid size: {b.Width}x{b.Height}");
        };

        return dataGrid;
    }
}
