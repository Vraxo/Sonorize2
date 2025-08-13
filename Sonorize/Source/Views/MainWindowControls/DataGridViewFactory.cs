using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Models;
using Sonorize.ViewModels;
using System.Diagnostics;

namespace Sonorize.Views.MainWindowControls
{
    public class DataGridViewFactory
    {
        private readonly ThemeColors _theme;
        private readonly ContextMenu _sharedSongContextMenu;

        public DataGridViewFactory(ThemeColors theme, ContextMenu sharedSongContextMenu)
        {
            _theme = theme;
            _sharedSongContextMenu = sharedSongContextMenu;
            Debug.WriteLine("[DataGridViewFactory] Initialized.");
        }

        // Method for Songs DataGrid
        public Control CreateSongsDataGrid(string itemsSourcePath, string selectedItemPath)
        {
            var dataGrid = CreateBaseDataGrid(itemsSourcePath, selectedItemPath);

            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Title", Binding = new Binding("Title"), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Artist", Binding = new Binding("Artist"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Album", Binding = new Binding("Album"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Duration", Binding = new Binding("DurationString"), Width = new DataGridLength(0.7, DataGridLengthUnitType.Star) });

            ApplyDataGridStyles(dataGrid, _theme, true);
            return new DockPanel { Children = { dataGrid } };
        }

        public Control CreateCompactSongsDataGrid(string itemsSourcePath, string selectedItemPath)
        {
            var dataGrid = CreateBaseDataGrid(itemsSourcePath, selectedItemPath);

            dataGrid.RowHeight = 32;

            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Title", Binding = new Binding("Title"), Width = new DataGridLength(4, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Artist", Binding = new Binding("Artist"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Duration", Binding = new Binding("DurationString"), Width = new DataGridLength(0.7, DataGridLengthUnitType.Star) });

            ApplyDataGridStyles(dataGrid, _theme, true);
            return new DockPanel { Children = { dataGrid } };
        }

        // Method for Artists DataGrid
        public Control CreateArtistsDataGrid(string itemsSourcePath, string selectedItemPath)
        {
            var dataGrid = CreateBaseDataGrid(itemsSourcePath, selectedItemPath);
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Artist", Binding = new Binding("Name"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Songs", Binding = new Binding("SongCount"), Width = new DataGridLength(0.5, DataGridLengthUnitType.Star) });
            ApplyDataGridStyles(dataGrid, _theme, false);
            return new DockPanel { Children = { dataGrid } };
        }

        // Method for Albums DataGrid
        public Control CreateAlbumsDataGrid(string itemsSourcePath, string selectedItemPath)
        {
            var dataGrid = CreateBaseDataGrid(itemsSourcePath, selectedItemPath);
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Album", Binding = new Binding("Title"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Artist", Binding = new Binding("Artist"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Songs", Binding = new Binding("SongCount"), Width = new DataGridLength(0.5, DataGridLengthUnitType.Star) });
            ApplyDataGridStyles(dataGrid, _theme, false);
            return new DockPanel { Children = { dataGrid } };
        }

        // Method for Playlists DataGrid
        public Control CreatePlaylistsDataGrid(string itemsSourcePath, string selectedItemPath)
        {
            var dataGrid = CreateBaseDataGrid(itemsSourcePath, selectedItemPath);
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Playlist", Binding = new Binding("Name"), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            dataGrid.Columns.Add(new DataGridTextColumn { Header = "Songs", Binding = new Binding("SongCount"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
            // For auto-playlist icon
            var typeColumn = new DataGridTemplateColumn
            {
                Header = "Type",
                Width = new DataGridLength(0.5, DataGridLengthUnitType.Star)
            };
            typeColumn.CellTemplate = new FuncDataTemplate<PlaylistViewModel>((vm, ns) =>
            {
                var icon = new TextBlock { FontSize = 12, Text = "Auto", VerticalAlignment = VerticalAlignment.Center };
                icon.Bind(Visual.IsVisibleProperty, new Binding(nameof(PlaylistViewModel.IsAutoPlaylist)));
                return icon;
            });
            dataGrid.Columns.Add(typeColumn);
            ApplyDataGridStyles(dataGrid, _theme, false);
            return new DockPanel { Children = { dataGrid } };
        }


        private DataGrid CreateBaseDataGrid(string itemsSourcePath, string selectedItemPath)
        {
            var dataGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserResizeColumns = true,
                CanUserReorderColumns = true,
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                Background = _theme.B_ListBoxBackground,
                BorderThickness = new Thickness(0),
                HeadersVisibility = DataGridHeadersVisibility.Column,
                RowHeight = 36 // A bit more spacious for readability
            };

            dataGrid.Bind(ItemsControl.ItemsSourceProperty, new Binding(itemsSourcePath));
            dataGrid.Bind(DataGrid.SelectedItemProperty, new Binding(selectedItemPath, BindingMode.TwoWay));

            return dataGrid;
        }

        private void ApplyDataGridStyles(DataGrid dataGrid, ThemeColors theme, bool addSongContextMenu)
        {
            // Column Header Style
            dataGrid.Styles.Add(new Style(s => s.Is<DataGridColumnHeader>())
            {
                Setters =
                {
                    new Setter(DataGridColumnHeader.BackgroundProperty, theme.B_SlightlyLighterBackground),
                    new Setter(DataGridColumnHeader.ForegroundProperty, theme.B_TextColor),
                    new Setter(DataGridColumnHeader.BorderBrushProperty, theme.B_ControlBackgroundColor),
                    new Setter(DataGridColumnHeader.BorderThicknessProperty, new Thickness(0,0,1,1)),
                    new Setter(DataGridColumnHeader.PaddingProperty, new Thickness(8,5)),
                    new Setter(DataGridColumnHeader.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                }
            });

            // Row Style
            var rowStyle = new Style(s => s.Is<DataGridRow>())
            {
                Setters =
                {
                    new Setter(DataGridRow.BackgroundProperty, Brushes.Transparent),
                    new Setter(DataGridRow.ForegroundProperty, theme.B_TextColor),
                    new Setter(DataGridRow.BorderThicknessProperty, new Thickness(0)),
                }
            };

            dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>().Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(DataGridRow.BackgroundProperty, theme.B_ControlBackgroundColor)
                }
            });

            dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>().Class(":selected"))
            {
                Setters =
                {
                    new Setter(DataGridRow.BackgroundProperty, theme.B_AccentColor),
                    new Setter(DataGridRow.ForegroundProperty, theme.B_AccentForeground)
                }
            });

            // Cell Style
            dataGrid.Styles.Add(new Style(s => s.Is<DataGridCell>())
            {
                Setters =
                {
                    new Setter(DataGridCell.BorderThicknessProperty, new Thickness(0)),
                    new Setter(DataGridCell.PaddingProperty, new Thickness(8)),
                    new Setter(DataGridCell.VerticalContentAlignmentProperty, VerticalAlignment.Center),
                }
            });

            // Selected cell foreground needs to be accent foreground
            dataGrid.Styles.Add(new Style(s => s.Is<DataGridRow>().Class(":selected").Descendant().Is<TextBlock>())
            {
                Setters =
                {
                    new Setter(TextBlock.ForegroundProperty, theme.B_AccentForeground),
                }
            });

            if (addSongContextMenu)
            {
                rowStyle.Setters.Add(new Setter(Control.ContextMenuProperty, _sharedSongContextMenu));
            }

            dataGrid.Styles.Add(rowStyle);
        }
    }
}