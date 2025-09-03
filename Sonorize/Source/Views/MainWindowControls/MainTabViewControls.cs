using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels;

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

        // StyleInclude for DataGrid moved to App.cs to ensure it's loaded globally

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

        tabControl.SelectionChanged += (s, e) =>
        {
            if (e.Source is TabControl && tabControl.DataContext is MainWindowViewModel vm)
            {
                if (e.AddedItems.Count > 0)
                {
                    vm.NotifyUserInitiatedTabChange();
                }
            }
        };

        // --- LIBRARY tab (DataGrid directly; no outer ScrollViewer) ---
        var libraryDataGrid = CreateLibraryDataGrid(_theme);
        var libraryTab = new TabItem
        {
            Header = "LIBRARY",
            Content = libraryDataGrid
        };

        // --- ARTISTS tab ---
        var (artistsListScrollViewer, artistsLb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "ArtistsListBox", "Library.Groupings.Artists", "Library.FilterState.SelectedArtist",
            _sharedViewTemplates.DetailedArtistTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate, lb => { });
        artistsListBox = artistsLb;
        artistsListScrollViewer.Bind(Visual.IsVisibleProperty, new Binding("Library.ArtistDrillDownTarget") { Converter = new FuncValueConverter<object, bool>(v => v == null) });

        var artistDrillDownView = CreateArtistDrillDownView(_theme);
        artistDrillDownView.Bind(Visual.IsVisibleProperty, new Binding("Library.ArtistDrillDownTarget") { Converter = NotNullToBooleanConverter.Instance });

        var artistsTabContent = new Grid();
        artistsTabContent.Children.Add(artistsListScrollViewer);
        artistsTabContent.Children.Add(artistDrillDownView);

        var artistsTab = new TabItem { Header = "ARTISTS", Content = artistsTabContent };

        // --- ALBUMS tab ---
        var (albumsListScrollViewer, albumsLb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "AlbumsListBox", "Library.Groupings.Albums", "Library.FilterState.SelectedAlbum",
            _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate, lb => { });
        albumsListBox = albumsLb;
        albumsListScrollViewer.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.AlbumViewMode")
        {
            Converter = new FuncValueConverter<SongDisplayMode, bool>(m => m != SongDisplayMode.Compact)
        });

        var albumsCompactGrid = CreateAlbumsCompactDataGrid(_theme);
        albumsCompactGrid.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.AlbumViewMode")
        {
            Converter = EnumToBooleanConverter.Instance,
            ConverterParameter = SongDisplayMode.Compact
        });

        var albumsTabContent = new Grid();
        albumsTabContent.Children.Add(albumsListScrollViewer);
        albumsTabContent.Children.Add(albumsCompactGrid);

        var albumsTab = new TabItem { Header = "ALBUMS", Content = albumsTabContent };

        // --- PLAYLISTS tab ---
        var (playlistsListScrollViewer, playlistsLb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            _theme, _sharedViewTemplates, "PlaylistsListBox", "Library.Groupings.Playlists", "Library.FilterState.SelectedPlaylist",
            _sharedViewTemplates.DetailedPlaylistTemplate, _sharedViewTemplates.StackPanelItemsPanelTemplate, lb => { });
        playlistsListBox = playlistsLb;
        playlistsListScrollViewer.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.PlaylistViewMode")
        {
            Converter = new FuncValueConverter<SongDisplayMode, bool>(m => m != SongDisplayMode.Compact)
        });

        var playlistsCompactGrid = CreatePlaylistsCompactDataGrid(_theme);
        playlistsCompactGrid.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.PlaylistViewMode")
        {
            Converter = EnumToBooleanConverter.Instance,
            ConverterParameter = SongDisplayMode.Compact
        });

        var playlistsTabContent = new Grid();
        playlistsTabContent.Children.Add(playlistsListScrollViewer);
        playlistsTabContent.Children.Add(playlistsCompactGrid);

        var playlistsTab = new TabItem { Header = "PLAYLISTS", Content = playlistsTabContent };

        // Populate tabs via Items.Add (Items is read-only, but the collection supports Add)
        tabControl.Items.Add(libraryTab);
        tabControl.Items.Add(artistsTab);
        tabControl.Items.Add(albumsTab);
        tabControl.Items.Add(playlistsTab);

        tabControl.AttachedToVisualTree += (_, __) =>
        {
            Debug.WriteLine($"[MainTabViewControls] TabControl attached. Size: {tabControl.Bounds.Width}x{tabControl.Bounds.Height}");
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
                // Compact mode is now handled by a separate DataGrid, so the ListBox is hidden.
                // No changes are needed for the ListBox itself in this case.
                break;

            case SongDisplayMode.Grid:
                listBox.ItemTemplate = gridTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.WrapPanelItemsPanelTemplate;
                if (scrollViewer is not null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
        }
    }

    private DataGrid CreateBaseStyledDataGrid(ThemeColors theme)
    {
        var dataGrid = new DataGrid
        {
            Background = theme.B_ListBoxBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Margin = new Thickness(10),
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            IsReadOnly = true,
            CanUserSortColumns = false,
            RowHeight = 30
        };

        dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, theme.B_ListBoxBackground),
                new Setter(TemplatedControl.ForegroundProperty, theme.B_TextColor),
            }
        });
        dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>().Class(":pointerover").Not(x => x.Class(":selected")))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, theme.B_ControlBackgroundColor) }
        });
        dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>().Class(":selected"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor),
                new Setter(TemplatedControl.ForegroundProperty, theme.B_AccentForeground)
            }
        });
        dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>().Class(":selected").Class(":pointerover"))
        {
            Setters =
            {
                new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor),
                new Setter(TemplatedControl.ForegroundProperty, theme.B_AccentForeground)
            }
        });
        return dataGrid;
    }

    private Grid CreateArtistDrillDownView(ThemeColors theme)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *"),
            Margin = new Thickness(10)
        };

        // Header
        var headerPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 10)
        };
        var backButton = new Button
        {
            Content = "← Back",
            Background = theme.B_ControlBackgroundColor,
            Foreground = theme.B_TextColor
        };
        backButton.Bind(Button.CommandProperty, new Binding("Library.GoBackToArtistListCommand"));

        var artistNameLabel = new TextBlock
        {
            FontSize = 18,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center
        };
        artistNameLabel.Bind(TextBlock.TextProperty, new Binding("Library.ArtistDrillDownTarget.Name"));

        headerPanel.Children.Add(backButton);
        headerPanel.Children.Add(artistNameLabel);
        Grid.SetRow(headerPanel, 0);

        // Song List
        var songsGrid = CreateLibraryDataGrid(theme);
        songsGrid.Margin = new Thickness(0);
        songsGrid.Columns.Remove(songsGrid.Columns.First(c => (c.Header as string) == "Artist"));
        songsGrid.Bind(DataGrid.ItemsSourceProperty, new Binding("Library.SongsForArtistDrillDown"));
        songsGrid.Bind(DataGrid.SelectedItemProperty, new Binding("Library.SelectedSong", BindingMode.TwoWay));

        Grid.SetRow(songsGrid, 1);

        grid.Children.Add(headerPanel);
        grid.Children.Add(songsGrid);

        return grid;
    }

    private DataGrid CreateArtistsCompactDataGrid(ThemeColors theme)
    {
        var dataGrid = CreateBaseStyledDataGrid(theme);
        dataGrid.Name = "ArtistsCompactDataGrid";
        dataGrid.Bind(DataGrid.ItemsSourceProperty, new Binding("Library.Groupings.Artists"));
        dataGrid.Bind(DataGrid.SelectedItemProperty, new Binding("Library.FilterState.SelectedArtist", BindingMode.TwoWay));
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Artist",
            Binding = new Binding("Name"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
        });
        return dataGrid;
    }

    private DataGrid CreateAlbumsCompactDataGrid(ThemeColors theme)
    {
        var dataGrid = CreateBaseStyledDataGrid(theme);
        dataGrid.Name = "AlbumsCompactDataGrid";
        dataGrid.Bind(DataGrid.ItemsSourceProperty, new Binding("Library.Groupings.Albums"));
        dataGrid.Bind(DataGrid.SelectedItemProperty, new Binding("Library.FilterState.SelectedAlbum", BindingMode.TwoWay));
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Album",
            Binding = new Binding("Title"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star),
        });
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Artist",
            Binding = new Binding("Artist"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
        });
        return dataGrid;
    }

    private DataGrid CreatePlaylistsCompactDataGrid(ThemeColors theme)
    {
        var dataGrid = CreateBaseStyledDataGrid(theme);
        dataGrid.Name = "PlaylistsCompactDataGrid";
        dataGrid.Bind(DataGrid.ItemsSourceProperty, new Binding("Library.Groupings.Playlists"));
        dataGrid.Bind(DataGrid.SelectedItemProperty, new Binding("Library.FilterState.SelectedPlaylist", BindingMode.TwoWay));
        var nameColumn = new DataGridTemplateColumn
        {
            Header = "Playlist",
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            CellTemplate = new FuncDataTemplate<PlaylistViewModel>((vm, ns) =>
            {
                var icon = new TextBlock { FontSize = 12, Text = "✨", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) };
                icon.Bind(Visual.IsVisibleProperty, new Binding("IsAutoPlaylist"));
                var nameBlock = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
                nameBlock.Bind(TextBlock.TextProperty, new Binding("Name"));
                return new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, nameBlock } };
            })
        };
        dataGrid.Columns.Add(nameColumn);
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Songs",
            Binding = new Binding("SongCount"),
            Width = DataGridLength.Auto,
        });
        return dataGrid;
    }

    private DataGrid CreateLibraryDataGrid(ThemeColors theme)
    {
        var dataGrid = new DataGrid
        {
            Name = "LibraryDataGrid",
            Background = theme.B_ListBoxBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            MinHeight = 250,
            AutoGenerateColumns = false,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.None,
            IsReadOnly = true,
            CanUserSortColumns = true,
            CanUserReorderColumns = true,
        };

        // Bindings
        dataGrid.Bind(DataGrid.ItemsSourceProperty,
                      new Binding("Library.FilteredSongs"));
        dataGrid.Bind(DataGrid.SelectedItemProperty,
                      new Binding("Library.SelectedSong", BindingMode.TwoWay));
        dataGrid.Bind(DataGrid.RowHeightProperty,
                      new Binding("Library.ViewOptions.RowHeight"));

        // --- DEBUG SNIPPET: dump DataContext and ItemsSource count ---
        dataGrid.AttachedToVisualTree += (_, __) =>
        {
            Debug.WriteLine($"[Debug] LibraryDataGrid.DataContext = {dataGrid.DataContext}");
            var items = dataGrid.ItemsSource as IEnumerable;
            var count = items?.Cast<object>().Count() ?? -1;
            Debug.WriteLine($"[Debug] LibraryDataGrid.ItemsSource count = {count}");
        };

        // Row styles (alternating / hover / selected)
        dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>())
        {
            Setters =
        {
            new Setter(TemplatedControl.BackgroundProperty, new MultiBinding
            {
                Converter = new AlternatingRowBackgroundConverter
                {
                    DefaultBrush = theme.B_ListBoxBackground,
                    AlternateBrush = theme.B_ListBoxAlternateBackground
                },
                Bindings =
                {
                    new Binding("."), // Song instance
                    new Binding
                    {
                        Path = "DataContext.Library.ViewOptions.EnableAlternatingRowColors",
                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
                        { AncestorType = typeof(DataGrid) }
                    }
                }
            }),
            new Setter(TemplatedControl.ForegroundProperty, theme.B_TextColor),
        }
        });

        dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>()
                                             .Class(":pointerover")
                                             .Not(x => x.Class(":selected")))
        {
            Setters = { new Setter(TemplatedControl.BackgroundProperty, theme.B_ControlBackgroundColor) }
        });

        dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>().Class(":selected"))
        {
            Setters =
        {
            new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor),
            new Setter(TemplatedControl.ForegroundProperty, theme.B_AccentForeground)
        }
        });

        dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>()
                                             .Class(":selected")
                                             .Class(":pointerover"))
        {
            Setters =
        {
            new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor),
            new Setter(TemplatedControl.ForegroundProperty, theme.B_AccentForeground)
        }
        });

        // Columns
        dataGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Title",
            Binding = new Binding("Title"),
            Width = new DataGridLength(3, DataGridLengthUnitType.Star),
            SortMemberPath = "Title"
        });

        var artistColumn = new DataGridTextColumn
        {
            Header = "Artist",
            Binding = new Binding("Artist"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star),
            SortMemberPath = "Artist"
        };
        artistColumn.Bind(DataGridColumn.IsVisibleProperty,
                          new Binding("DataContext.Library.ViewOptions.ShowArtist")
                          { Source = dataGrid });
        dataGrid.Columns.Add(artistColumn);

        var albumColumn = new DataGridTextColumn
        {
            Header = "Album",
            Binding = new Binding("Album"),
            Width = new DataGridLength(2, DataGridLengthUnitType.Star),
            SortMemberPath = "Album"
        };
        albumColumn.Bind(DataGridColumn.IsVisibleProperty,
                         new Binding("DataContext.Library.ViewOptions.ShowAlbum")
                         { Source = dataGrid });
        dataGrid.Columns.Add(albumColumn);

        var durationColumn = new DataGridTextColumn
        {
            Header = "Duration",
            Binding = new Binding("DurationString"),
            Width = DataGridLength.Auto,
            SortMemberPath = "Duration"
        };
        durationColumn.Bind(DataGridColumn.IsVisibleProperty,
                            new Binding("DataContext.Library.ViewOptions.ShowDuration")
                            { Source = dataGrid });
        dataGrid.Columns.Add(durationColumn);

        var playCountColumn = new DataGridTextColumn
        {
            Header = "Plays",
            Binding = new Binding("PlayCount"),
            Width = DataGridLength.Auto,
            SortMemberPath = "PlayCount"
        };
        playCountColumn.Bind(DataGridColumn.IsVisibleProperty,
                             new Binding("DataContext.Library.ViewOptions.ShowPlayCount")
                             { Source = dataGrid });
        dataGrid.Columns.Add(playCountColumn);

        var dateAddedColumn = new DataGridTextColumn
        {
            Header = "Date Added",
            Binding = new Binding("DateAdded") { StringFormat = "yyyy-MM-dd" },
            Width = DataGridLength.Auto,
            SortMemberPath = "DateAdded"
        };
        dateAddedColumn.Bind(DataGridColumn.IsVisibleProperty,
                             new Binding("DataContext.Library.ViewOptions.ShowDateAdded")
                             { Source = dataGrid });
        dataGrid.Columns.Add(dateAddedColumn);

        return dataGrid;
    }
}