using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Converters;
using Sonorize.Models;
using System;
using System.Collections;
using System.Diagnostics;
using System.Linq;

namespace Sonorize.Views.MainWindowControls;

internal static class LibraryDataGridFactory
{
    public static DataGrid Create(ThemeColors theme)
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

        // ── FIX: snap column widths to whole pixels so header dividers never vanish
        dataGrid.LayoutUpdated += (_, __) => SnapColumnWidthsToPixels(dataGrid);

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