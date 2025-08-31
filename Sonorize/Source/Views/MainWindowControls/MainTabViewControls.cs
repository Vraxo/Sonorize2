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
        var libraryHeader = CreateLibraryHeaderGrid(_theme);
        DockPanel.SetDock(libraryHeader, Dock.Top);
        libraryTabContent.Children.Add(libraryHeader);
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
            Margin = new Thickness(10, 0, 10, 0),
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
            Converter = new FuncValueConverter<SongDisplayMode, bool>(m => m == SongDisplayMode.Detailed || m == SongDisplayMode.Compact)
        });

        var columns = headerGrid.ColumnDefinitions;

        var imageCol = new ColumnDefinition();
        imageCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.LibraryViewMode")
        {
            Source = proxy,
            Converter = new FuncValueConverter<SongDisplayMode, GridLength>(m => m == SongDisplayMode.Detailed ? new GridLength(32 + 5 + 15) : new GridLength(0))
        });
        columns.Add(imageCol);

        var titleCol = new ColumnDefinition();
        titleCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.LibraryViewMode")
        {
            Source = proxy,
            Converter = new FuncValueConverter<SongDisplayMode, GridLength>(m => m == SongDisplayMode.Detailed ? new GridLength(3, GridUnitType.Star) : new GridLength(2, GridUnitType.Star))
        });
        columns.Add(titleCol);

        var artistCol = new ColumnDefinition();
        artistCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowArtist") { Source = proxy, Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "1.5*" });
        columns.Add(artistCol);

        var albumCol = new ColumnDefinition();
        albumCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowAlbum") { Source = proxy, Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "1.5*" });
        columns.Add(albumCol);

        var playCountCol = new ColumnDefinition();
        playCountCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowPlayCount") { Source = proxy, Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "0.6*" });
        columns.Add(playCountCol);

        var dateAddedCol = new ColumnDefinition();
        dateAddedCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowDateAdded") { Source = proxy, Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "1.2*" });
        columns.Add(dateAddedCol);

        var durationCol = new ColumnDefinition();
        durationCol.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.Library.ViewOptions.ShowDuration") { Source = proxy, Converter = BooleanToGridLengthConverter.Instance, ConverterParameter = "0.8*" });
        columns.Add(durationCol);

        int currentCol = 1;
        headerGrid.Children.Add(CreateHeaderButton(theme, "Title", SortProperty.Title, currentCol++, proxy));
        headerGrid.Children.Add(CreateHeaderButton(theme, "Artist", SortProperty.Artist, currentCol++, proxy));
        headerGrid.Children.Add(CreateHeaderButton(theme, "Album", SortProperty.Album, currentCol++, proxy));
        headerGrid.Children.Add(CreateHeaderButton(theme, "Plays", SortProperty.PlayCount, currentCol++, proxy, HorizontalAlignment.Right));
        headerGrid.Children.Add(CreateHeaderButton(theme, "Date Added", SortProperty.DateAdded, currentCol++, proxy, HorizontalAlignment.Right));
        headerGrid.Children.Add(CreateHeaderButton(theme, "Duration", SortProperty.Duration, currentCol++, proxy, HorizontalAlignment.Right));

        return headerGrid;
    }

    private Button CreateHeaderButton(ThemeColors theme, string text, SortProperty sortProperty, int column, Border proxy, HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        var button = new Button
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(10, 5, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = alignment
        };
        button.Bind(Button.CommandProperty, new Binding("Tag.Library.SortCommand") { Source = proxy });
        button.CommandParameter = sortProperty;
        Grid.SetColumn(button, column);

        var contentPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeight.Bold,
            Foreground = theme.B_SecondaryTextColor
        };

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
            Converter = new FuncValueConverter<SortProperty, bool>(p => p == sortProperty)
        };
        sortIndicator.Bind(Visual.IsVisibleProperty, visibilityBinding);
        sortIndicator.Bind(TextBlock.TextProperty, new Binding("Tag.Library.CurrentSortDirection")
        {
            Source = proxy,
            Converter = new FuncValueConverter<SortDirection, string>(d => d == SortDirection.Ascending ? "▲" : "▼")
        });

        contentPanel.Children.Add(textBlock);
        contentPanel.Children.Add(sortIndicator);
        button.Content = contentPanel;

        return button;
    }
}