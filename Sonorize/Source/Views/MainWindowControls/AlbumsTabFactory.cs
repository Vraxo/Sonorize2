using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views.MainWindowControls;

internal static class AlbumsTabFactory
{
    public static Grid Create(ThemeColors theme, SharedViewTemplates sharedViewTemplates, out ListBox albumsListBox)
    {
        var albumsListView = new Grid();
        var (albumsListScrollViewer, albumsLb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            theme, sharedViewTemplates, "AlbumsListBox", "Library.Groupings.Albums", "Library.FilterState.SelectedAlbum",
            sharedViewTemplates.DetailedAlbumTemplate, sharedViewTemplates.StackPanelItemsPanelTemplate, lb => { });
        albumsListBox = albumsLb;
        albumsListScrollViewer.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.AlbumViewMode")
        {
            Converter = new FuncValueConverter<SongDisplayMode, bool>(m => m != SongDisplayMode.Compact)
        });

        var albumsCompactGrid = CompactDataGridFactory.CreateAlbumsCompactDataGrid(theme);
        albumsCompactGrid.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.AlbumViewMode")
        {
            Converter = EnumToBooleanConverter.Instance,
            ConverterParameter = SongDisplayMode.Compact
        });

        albumsListView.Children.Add(albumsListScrollViewer);
        albumsListView.Children.Add(albumsCompactGrid);

        albumsListView.Bind(Visual.IsVisibleProperty, new Binding("Library.FilterState.SelectedAlbum")
        {
            Converter = new FuncValueConverter<AlbumViewModel?, bool>(vm => vm == null)
        });

        var albumDrillDownView = CreateAlbumDrillDownView(theme);
        albumDrillDownView.Bind(Visual.IsVisibleProperty, new Binding("Library.FilterState.SelectedAlbum")
        {
            Converter = NotNullToBooleanConverter.Instance
        });

        var albumsTabContent = new Grid();
        albumsTabContent.Children.Add(albumsListView);
        albumsTabContent.Children.Add(albumDrillDownView);

        return albumsTabContent;
    }

    private static Grid CreateAlbumDrillDownView(ThemeColors theme)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *")
        };

        // Header with Back button and Album Name
        var headerPanel = new DockPanel
        {
            Margin = new Thickness(10, 0, 10, 5)
        };
        var backButton = new Button
        {
            Content = "← Back to Albums",
            Background = theme.B_ControlBackgroundColor,
            Foreground = theme.B_TextColor,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        backButton.Bind(Button.CommandProperty, new Binding("Library.ClearAlbumFilterCommand"));
        DockPanel.SetDock(backButton, Dock.Left);

        var albumNameBlock = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = theme.B_TextColor
        };
        albumNameBlock.Bind(TextBlock.TextProperty, new Binding("Library.FilterState.SelectedAlbum.DisplayText"));
        DockPanel.SetDock(albumNameBlock, Dock.Right);

        headerPanel.Children.Add(backButton);
        headerPanel.Children.Add(albumNameBlock);

        // DataGrid for songs
        var songsDataGrid = LibraryDataGridFactory.Create(theme);
        songsDataGrid.Name = "AlbumSongsDataGrid";

        Grid.SetRow(headerPanel, 0);
        Grid.SetRow(songsDataGrid, 1);
        grid.Children.Add(headerPanel);
        grid.Children.Add(songsDataGrid);

        return grid;
    }
}