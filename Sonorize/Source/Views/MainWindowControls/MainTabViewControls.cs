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
    private ListBox? _songListBoxInstance;
    private ListBox? _artistsListBoxInstance;
    private ListBox? _albumsListBoxInstance;

    public MainTabViewControls(ThemeColors theme, SharedViewTemplates sharedViewTemplates)
    {
        _theme = theme;
        _sharedViewTemplates = sharedViewTemplates;
    }

    public TabControl CreateMainTabView(out ListBox songListBox, out ListBox artistsListBox, out ListBox albumsListBox)
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

        songListBox = _songListBoxInstance!;
        artistsListBox = _artistsListBoxInstance!;
        albumsListBox = _albumsListBoxInstance!;
        return tabControl;
    }

    private ScrollViewer CreateSongListScrollViewer()
    {
        _songListBoxInstance = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10),
            Name = "SongListBox"
        };

        ApplyListBoxItemStyles(_songListBoxInstance);

        _songListBoxInstance.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.FilteredSongs"));
        _songListBoxInstance.Bind(ListBox.SelectedItemProperty, new Binding("Library.SelectedSong", BindingMode.TwoWay));

        _songListBoxInstance.ItemTemplate = _sharedViewTemplates.SongTemplates.DetailedSongTemplate;
        _songListBoxInstance.ItemsPanel = _sharedViewTemplates.StackPanelItemsPanelTemplate;

        return new ScrollViewer { Content = _songListBoxInstance, Padding = new Thickness(0, 0, 0, 5), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private ScrollViewer CreateArtistsListScrollViewer()
    {
        _artistsListBoxInstance = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10),
            Name = "ArtistsListBox"
        };
        ApplyListBoxItemStyles(_artistsListBoxInstance);

        _artistsListBoxInstance.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.Groupings.Artists"));
        _artistsListBoxInstance.Bind(ListBox.SelectedItemProperty, new Binding("Library.FilterState.SelectedArtist", BindingMode.TwoWay));

        _artistsListBoxInstance.ItemTemplate = _sharedViewTemplates.ArtistTemplates.DetailedArtistTemplate; // Updated path
        _artistsListBoxInstance.ItemsPanel = _sharedViewTemplates.StackPanelItemsPanelTemplate;

        return new ScrollViewer { Content = _artistsListBoxInstance, Padding = new Thickness(0, 0, 0, 5), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private ScrollViewer CreateAlbumsListScrollViewer()
    {
        _albumsListBoxInstance = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10),
            Name = "AlbumsListBox"
        };
        ApplyListBoxItemStyles(_albumsListBoxInstance);

        _albumsListBoxInstance.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.Groupings.Albums"));
        _albumsListBoxInstance.Bind(ListBox.SelectedItemProperty, new Binding("Library.FilterState.SelectedAlbum", BindingMode.TwoWay));

        _albumsListBoxInstance.ItemTemplate = _sharedViewTemplates.DetailedAlbumTemplate;
        _albumsListBoxInstance.ItemsPanel = _sharedViewTemplates.StackPanelItemsPanelTemplate;

        return new ScrollViewer { Content = _albumsListBoxInstance, Padding = new Thickness(0, 0, 0, 5), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    private void ApplyListBoxItemStyles(ListBox listBox)
    {
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>())
        {
            Setters = {
                new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground),
                new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor),
                new Setter(ListBoxItem.PaddingProperty, new Thickness(3))
            }
        });
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) } });
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
        {
            Setters = {
                new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
            }
        });
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
        {
            Setters = {
                new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
            }
        });
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
                if (scrollViewer != null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
            case SongDisplayMode.Compact:
                listBox.ItemTemplate = compactTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.StackPanelItemsPanelTemplate;
                if (scrollViewer != null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
            case SongDisplayMode.Grid:
                listBox.ItemTemplate = gridTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.WrapPanelItemsPanelTemplate;
                if (scrollViewer != null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
        }
    }
}