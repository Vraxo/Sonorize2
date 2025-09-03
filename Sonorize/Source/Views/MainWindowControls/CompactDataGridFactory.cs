using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;

namespace Sonorize.Views.MainWindowControls;

internal static class CompactDataGridFactory
{
    private static DataGrid CreateBaseStyledDataGrid(ThemeColors theme)
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

        // ── FIX: snap column widths to whole pixels so header dividers never vanish
        dataGrid.LayoutUpdated += (_, __) => SnapColumnWidthsToPixels(dataGrid);

        return dataGrid;
    }

    public static DataGrid CreateArtistsCompactDataGrid(ThemeColors theme)
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

    public static DataGrid CreateAlbumsCompactDataGrid(ThemeColors theme)
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

    public static DataGrid CreatePlaylistsCompactDataGrid(ThemeColors theme)
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

    /// <summary>
    /// Rounds every column width to an integral number of physical pixels
    /// so that vertical header dividers are always drawn.
    /// </summary>
    private static void SnapColumnWidthsToPixels(DataGrid grid)
    {
        if (grid?.Columns is null) return;

        var topLevel = TopLevel.GetTopLevel(grid);
        if (topLevel is null) return;

        double scale = topLevel.RenderScaling;

        foreach (var col in grid.Columns)
        {
            if (!col.IsVisible) continue;

            double current = col.ActualWidth;
            double devicePixels = current * scale;
            double snapped = Math.Round(devicePixels) / scale;
            if (Math.Abs(current - snapped) > 0.05)
                col.Width = new DataGridLength(snapped);
        }
    }
}