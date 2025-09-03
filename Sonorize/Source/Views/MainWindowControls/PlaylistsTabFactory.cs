using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views.MainWindowControls;

internal static class PlaylistsTabFactory
{
    public static Grid Create(ThemeColors theme, SharedViewTemplates sharedViewTemplates, out ListBox playlistsListBox)
    {
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

        var playlistsTabContent = new Grid();
        playlistsTabContent.Children.Add(playlistsListScrollViewer);
        playlistsTabContent.Children.Add(playlistsCompactGrid);

        return playlistsTabContent;
    }
}