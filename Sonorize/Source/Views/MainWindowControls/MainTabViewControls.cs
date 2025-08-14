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
using System.Linq;
using System.Collections;
using Avalonia.Threading;
using Avalonia.VisualTree;

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

        ApplyTabControlStyles(tabControl);

        var libraryTab = new TabItem
        {
            Header = "LIBRARY",
            Content = CreateTabContent(
                "Library.FilteredSongs",
                "Library.SelectedSong",
                "Library.LibraryViewMode",
                new DataGridColumn[]
                {
                    new DataGridTextColumn { Header = "Title", Binding = new Binding("Title"), Width = new DataGridLength(2.5, DataGridLengthUnitType.Star), CanUserSort = true },
                    new DataGridTextColumn { Header = "Artist", Binding = new Binding("Artist"), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star), CanUserSort = true },
                    new DataGridTextColumn { Header = "Album", Binding = new Binding("Album"), Width = new DataGridLength(1.5, DataGridLengthUnitType.Star), CanUserSort = true },
                    new DataGridTextColumn { Header = "Duration", Binding = new Binding("DurationString"), CanUserSort = true }
                },
                lb => _songListBoxInstance = lb,
                _sharedViewTemplates.SongTemplates.DetailedSongTemplate,
                _sharedViewTemplates.StackPanelItemsPanelTemplate
            )
        };

        var artistsTab = new TabItem
        {
            Header = "ARTISTS",
            Content = CreateTabContent(
                "Library.Groupings.Artists",
                "Library.FilterState.SelectedArtist",
                "Library.ArtistViewMode",
                new DataGridColumn[]
                {
                    new DataGridTextColumn { Header = "Artist", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), CanUserSort = true },
                    new DataGridTextColumn { Header = "Songs", Binding = new Binding("SongCount"), CanUserSort = true }
                },
                lb => _artistsListBoxInstance = lb,
                _sharedViewTemplates.ArtistTemplates.DetailedArtistTemplate,
                _sharedViewTemplates.StackPanelItemsPanelTemplate
            )
        };

        var albumsTab = new TabItem
        {
            Header = "ALBUMS",
            Content = CreateTabContent(
                "Library.Groupings.Albums",
                "Library.FilterState.SelectedAlbum",
                "Library.AlbumViewMode",
                new DataGridColumn[]
                {
                    new DataGridTextColumn { Header = "Album", Binding = new Binding("Title"), Width = new DataGridLength(2, DataGridLengthUnitType.Star), CanUserSort = true },
                    new DataGridTextColumn { Header = "Artist", Binding = new Binding("Artist"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), CanUserSort = true },
                    new DataGridTextColumn { Header = "Songs", Binding = new Binding("SongCount"), CanUserSort = true }
                },
                lb => _albumsListBoxInstance = lb,
                _sharedViewTemplates.DetailedAlbumTemplate,
                _sharedViewTemplates.StackPanelItemsPanelTemplate
            )
        };

        var playlistsTab = new TabItem
        {
            Header = "PLAYLISTS",
            Content = CreateTabContent(
                "Library.Groupings.Playlists",
                "Library.FilterState.SelectedPlaylist",
                "Library.PlaylistViewMode",
                new DataGridColumn[]
                {
                    new DataGridTextColumn { Header = "Playlist", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star), CanUserSort = true },
                    new DataGridTextColumn { Header = "Songs", Binding = new Binding("SongCount"), CanUserSort = true }
                },
                lb => _playlistsListBoxInstance = lb,
                _sharedViewTemplates.DetailedPlaylistTemplate,
                _sharedViewTemplates.StackPanelItemsPanelTemplate
            )
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

    private Panel CreateTabContent(
        string itemsSourcePath,
        string selectedItemPath,
        string viewModePath,
        DataGridColumn[] dataGridColumns,
        System.Action<ListBox> storeListBoxInstanceCallback,
        IDataTemplate initialItemTemplate,
        ITemplate<Panel> initialItemsPanelTemplate)
    {
        var container = new Grid(); // Use a Grid as the container for proper layouting.

        // Create ListBox for Detailed/Grid views
        var listBox = new ListBox
        {
            Background = _theme.B_ListBoxBackground,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10),
            ItemTemplate = initialItemTemplate,
            ItemsPanel = initialItemsPanelTemplate
        };
        ApplyListBoxItemStyles(listBox, _theme);
        listBox.Bind(ItemsControl.ItemsSourceProperty, new Binding(itemsSourcePath));
        listBox.Bind(ListBox.SelectedItemProperty, new Binding(selectedItemPath, BindingMode.TwoWay));
        storeListBoxInstanceCallback(listBox);

        var scrollViewer = new ScrollViewer
        {
            Content = listBox,
            Padding = new Thickness(0, 0, 0, 5),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        // Create DataGrid for Compact view
        var dataGrid = new DataGrid
        {
            IsReadOnly = true,
            CanUserReorderColumns = true,
            CanUserResizeColumns = true,
            AutoGenerateColumns = false,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            Margin = new Thickness(5),
        };
        ApplyDataGridStyles(dataGrid, _theme);
        foreach (var col in dataGridColumns) dataGrid.Columns.Add(col);

        dataGrid.Bind(ItemsControl.ItemsSourceProperty, new Binding(itemsSourcePath));
        dataGrid.Bind(DataGrid.SelectedItemProperty, new Binding(selectedItemPath, BindingMode.TwoWay));

        // Visibility will now be controlled imperatively by UpdateListViewMode

        // Add DataGrid first to the container, then the ScrollViewer.
        container.Children.Add(dataGrid);
        container.Children.Add(scrollViewer);

        return container;
    }

    private void ApplyTabControlStyles(TabControl tabControl)
    {
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
    }

    private void ApplyListBoxItemStyles(ListBox listBox, ThemeColors theme)
    {
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>())
        {
            Setters = {
                new Setter(TemplatedControl.BackgroundProperty, theme.B_ListBoxBackground),
                new Setter(TextBlock.ForegroundProperty, theme.B_TextColor),
                new Setter(ListBoxItem.PaddingProperty, new Thickness(3))
            }
        });
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
        { Setters = { new Setter(TemplatedControl.BackgroundProperty, theme.B_ControlBackgroundColor) } });
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
        {
            Setters = {
                new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, theme.B_AccentForeground)
            }
        });
        listBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
        {
            Setters = {
                new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, theme.B_AccentForeground)
            }
        });
    }

    private void ApplyDataGridStyles(DataGrid dataGrid, ThemeColors theme)
    {
        dataGrid.Background = _theme.B_ListBoxBackground;
        dataGrid.Foreground = _theme.B_TextColor;
        dataGrid.BorderBrush = _theme.B_ControlBackgroundColor;
        dataGrid.BorderThickness = new Thickness(0);
        dataGrid.HorizontalGridLinesBrush = _theme.B_ControlBackgroundColor;
        dataGrid.VerticalGridLinesBrush = _theme.B_ControlBackgroundColor;

        dataGrid.Styles.Add(new Style(s => s.OfType<DataGridColumnHeader>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor),
                new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor),
                new Setter(TemplatedControl.BorderBrushProperty, _theme.B_SlightlyLighterBackground),
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0,0,1,1)),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(8,5))
            }
        });

        dataGrid.Styles.Add(new Style(s => s.OfType<DataGridRow>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor),
            }
        });

        dataGrid.Styles.Add(new Style(s => s.OfType<DataGridRow>().Class(":pointerover").Not(x => x.Class(":selected")))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) }
        });

        dataGrid.Styles.Add(new Style(s => s.OfType<DataGridRow>().Class(":selected"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
            }
        });
        dataGrid.Styles.Add(new Style(s => s.OfType<DataGridRow>().Class(":selected").Class(":pointerover"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
                new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
            }
        });
    }

    public void UpdateListViewMode(SongDisplayMode mode, Panel container, IDataTemplate detailedTemplate, IDataTemplate compactTemplate, IDataTemplate gridTemplate, MainWindowViewModel? mvm = null)
    {
        Debug.WriteLine($"[MainTabViewControls] UpdateListViewMode called for mode '{mode}'. Container has {container.Children.Count} children.");

        // Find controls
        if (container.Children.OfType<ScrollViewer>().FirstOrDefault() is not { Content: ListBox listBox } scrollViewer ||
            container.Children.OfType<DataGrid>().FirstOrDefault() is not { } dataGrid)
        {
            Debug.WriteLine("[MainTabViewControls] UpdateListViewMode failed to find ScrollViewer/ListBox or DataGrid inside container.");
            return;
        }

        // Imperatively set visibility
        scrollViewer.IsVisible = mode != SongDisplayMode.Compact;
        dataGrid.IsVisible = mode == SongDisplayMode.Compact;

        if (mode == SongDisplayMode.Compact)
        {
            // Log to confirm
            var items = dataGrid.ItemsSource as IEnumerable;
            var itemCount = items?.Cast<object>().Count() ?? 0;
            Debug.WriteLine($"[MainTabViewControls] Switched to Compact mode. DataGrid is now visible. Its ItemsSource has {itemCount} items.");
            return; // Nothing more to do for ListBox when DataGrid is visible.
        }

        Debug.WriteLine($"[MainTabViewControls] Updating ListBox template for mode '{mode}'.");
        switch (mode)
        {
            case SongDisplayMode.Detailed:
                listBox.ItemTemplate = detailedTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.StackPanelItemsPanelTemplate;
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
            case SongDisplayMode.Grid:
                listBox.ItemTemplate = gridTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.WrapPanelItemsPanelTemplate;
                scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
        }
    }
}