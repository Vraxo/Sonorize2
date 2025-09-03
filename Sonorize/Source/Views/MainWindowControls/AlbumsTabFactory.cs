using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views.MainWindowControls;

internal static class AlbumsTabFactory
{
    public static Grid Create(ThemeColors theme, SharedViewTemplates sharedViewTemplates, out ListBox albumsListBox)
    {
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

        var albumsTabContent = new Grid();
        albumsTabContent.Children.Add(albumsListScrollViewer);
        albumsTabContent.Children.Add(albumsCompactGrid);

        return albumsTabContent;
    }
}