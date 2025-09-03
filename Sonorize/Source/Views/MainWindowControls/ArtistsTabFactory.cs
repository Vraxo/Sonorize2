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

internal static class ArtistsTabFactory
{
    public static Grid Create(ThemeColors theme, SharedViewTemplates sharedViewTemplates, out ListBox artistsListBox)
    {
        var artistsListView = new Grid();
        var (artistsListScrollViewer, artistsLb) = ListBoxViewFactory.CreateStyledListBoxScrollViewer(
            theme, sharedViewTemplates, "ArtistsListBox", "Library.Groupings.Artists", "Library.FilterState.SelectedArtist",
            sharedViewTemplates.DetailedArtistTemplate, sharedViewTemplates.StackPanelItemsPanelTemplate, lb => { });
        artistsListBox = artistsLb;
        artistsListScrollViewer.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.ArtistViewMode")
        {
            Converter = new FuncValueConverter<SongDisplayMode, bool>(m => m != SongDisplayMode.Compact)
        });
        var artistsCompactGrid = CompactDataGridFactory.CreateArtistsCompactDataGrid(theme);
        artistsCompactGrid.Bind(Visual.IsVisibleProperty, new Binding("LibraryDisplayModeService.ArtistViewMode")
        {
            Converter = EnumToBooleanConverter.Instance,
            ConverterParameter = SongDisplayMode.Compact
        });
        artistsListView.Children.Add(artistsListScrollViewer);
        artistsListView.Children.Add(artistsCompactGrid);
        artistsListView.Bind(Visual.IsVisibleProperty, new Binding("Library.FilterState.SelectedArtist")
        {
            Converter = new FuncValueConverter<ArtistViewModel?, bool>(vm => vm == null)
        });

        var artistDrillDownView = CreateArtistDrillDownView(theme);
        artistDrillDownView.Bind(Visual.IsVisibleProperty, new Binding("Library.FilterState.SelectedArtist")
        {
            Converter = NotNullToBooleanConverter.Instance
        });

        var artistsTabContent = new Grid();
        artistsTabContent.Children.Add(artistsListView);
        artistsTabContent.Children.Add(artistDrillDownView);

        return artistsTabContent;
    }

    private static Grid CreateArtistDrillDownView(ThemeColors theme)
    {
        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto, *")
        };

        // Header with Back button and Artist Name
        var headerPanel = new DockPanel
        {
            Margin = new Thickness(10, 0, 10, 5)
        };
        var backButton = new Button
        {
            Content = "← Back to Artists",
            Background = theme.B_ControlBackgroundColor,
            Foreground = theme.B_TextColor,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        backButton.Bind(Button.CommandProperty, new Binding("Library.ClearArtistFilterCommand"));
        DockPanel.SetDock(backButton, Dock.Left);

        var artistNameBlock = new TextBlock
        {
            FontSize = 16,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = theme.B_TextColor
        };
        artistNameBlock.Bind(TextBlock.TextProperty, new Binding("Library.FilterState.SelectedArtist.Name"));
        DockPanel.SetDock(artistNameBlock, Dock.Right);

        headerPanel.Children.Add(backButton);
        headerPanel.Children.Add(artistNameBlock);

        // DataGrid for songs
        var songsDataGrid = LibraryDataGridFactory.Create(theme); // Reuse the same styling and column setup
        songsDataGrid.Name = "ArtistSongsDataGrid";

        Grid.SetRow(headerPanel, 0);
        Grid.SetRow(songsDataGrid, 1);
        grid.Children.Add(headerPanel);
        grid.Children.Add(songsDataGrid);

        return grid;
    }
}