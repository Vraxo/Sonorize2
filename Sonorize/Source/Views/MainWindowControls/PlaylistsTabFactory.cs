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

internal static class PlaylistsTabFactory
{
    public static Grid Create(ThemeColors theme, SharedViewTemplates sharedViewTemplates, out ListBox playlistsListBox)
    {
        var playlistsListView = new Grid();
        var (playlistsListScrollViewer, playlistsLb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            theme, sharedViewTemplates, "PlaylistsListBox", "Library.Groupings.Playlists", "Library.FilterState.SelectedPlaylist",
            sharedViewTemplates.DetailedPlaylistTemplate, sharedViewTemplates.StackPanelItemsPanelTemplate, lb => { });
        playlistsListBox = playlistsLb;
        playlistsListScrollViewer.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.PlaylistViewMode")
        {
            Converter = new FuncValueConverter<SongDisplayMode, bool>(m => m != SongDisplayMode.Compact)
        });

        var playlistsCompactGrid = CompactDataGridFactory.CreatePlaylistsCompactDataGrid(theme);
        playlistsCompactGrid.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.PlaylistViewMode")
        {
            Converter = EnumToBooleanConverter.Instance,
            ConverterParameter = SongDisplayMode.Compact
        });

        playlistsListView.Children.Add(playlistsListScrollViewer);
        playlistsListView.Children.Add(playlistsCompactGrid);
        playlistsListView.Bind(Visual.IsVisibleProperty, new Binding("Library.FilterState.SelectedPlaylist")
        {
            Converter = new FuncValueConverter<PlaylistViewModel?, bool>(vm => vm == null)
        });

        var playlistDrillDownView = CreatePlaylistDrillDownView(theme);
        playlistDrillDownView.Bind(Visual.IsVisibleProperty, new Binding("Library.FilterState.SelectedPlaylist")
        {
            Converter = NotNullToBooleanConverter.Instance
        });

        var playlistsTabContent = new Grid();
        playlistsTabContent.Children.Add(playlistsListView);
        playlistsTabContent.Children.Add(playlistDrillDownView);

        return playlistsTabContent;
    }

    private static Grid CreatePlaylistDrillDownView(ThemeColors theme)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *")
        };

        // Header with Back button and Playlist Name
        var headerPanel = new DockPanel
        {
            Margin = new Thickness(10, 0, 10, 5)
        };
        var backButton = new Button
        {
            Content = "← Back to Playlists",
            Background = theme.B_ControlBackgroundColor,
            Foreground = theme.B_TextColor,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        backButton.Bind(Button.CommandProperty, new Binding("Library.ClearPlaylistFilterCommand"));
        DockPanel.SetDock(backButton, Dock.Left);

        var playlistNameBlock = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = theme.B_TextColor
        };
        playlistNameBlock.Bind(TextBlock.TextProperty, new Binding("Library.FilterState.SelectedPlaylist.Name"));
        DockPanel.SetDock(playlistNameBlock, Dock.Right);

        headerPanel.Children.Add(backButton);
        headerPanel.Children.Add(playlistNameBlock);

        // DataGrid for songs
        var songsDataGrid = LibraryDataGridFactory.Create(theme);
        songsDataGrid.Name = "PlaylistSongsDataGrid";

        Grid.SetRow(headerPanel, 0);
        Grid.SetRow(songsDataGrid, 1);
        grid.Children.Add(headerPanel);
        grid.Children.Add(songsDataGrid);

        return grid;
    }
}