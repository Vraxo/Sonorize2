using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Sonorize.Models; // For ThemeColors, Song, ArtistViewModel, AlbumViewModel
using Sonorize.ViewModels; // For SongDisplayMode
using System.Diagnostics; // For Debug

namespace Sonorize.Views.MainWindowControls
{
    public class MainTabViewControls
    {
        private readonly ThemeColors _theme;
        private readonly SongListTemplates _songListTemplates;
        private ListBox? _songListBoxInstance; // Instance of the song ListBox created by this class

        public MainTabViewControls(ThemeColors theme, SongListTemplates songListTemplates)
        {
            _theme = theme;
            _songListTemplates = songListTemplates;
        }

        public TabControl CreateMainTabView(out ListBox songListBox)
        {
            var tabControl = new TabControl
            {
                Background = _theme.B_BackgroundColor,
                Margin = new Thickness(10, 5, 10, 5),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };
            tabControl.Bind(TabControl.SelectedIndexProperty, new Binding("ActiveTabIndex", BindingMode.TwoWay));

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

            var libraryTab = new TabItem
            {
                Header = "LIBRARY",
                Content = CreateSongListScrollViewer() // This will set _songListBoxInstance
            };

            var artistsTab = new TabItem
            {
                Header = "ARTISTS",
                Content = CreateArtistsListScrollViewer()
            };

            var albumsTab = new TabItem
            {
                Header = "ALBUMS",
                Content = CreateAlbumsListScrollViewer()
            };

            tabControl.Items.Add(libraryTab);
            tabControl.Items.Add(artistsTab);
            tabControl.Items.Add(albumsTab);

            songListBox = _songListBoxInstance!; // Assign the created ListBox to the out parameter
            return tabControl;
        }

        private ScrollViewer CreateSongListScrollViewer()
        {
            _songListBoxInstance = new ListBox
            {
                Background = _theme.B_ListBoxBackground,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(10),
                Name = "SongListBox"
            };

            _songListBoxInstance.Styles.Add(new Style(s => s.Is<ListBoxItem>())
            {
                Setters = {
                    new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground),
                    new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor),
                    new Setter(ListBoxItem.PaddingProperty, new Thickness(3))
                }
            });
            _songListBoxInstance.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) } });
            _songListBoxInstance.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
            {
                Setters = {
                    new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
                    new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
                }
            });
            _songListBoxInstance.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
            {
                Setters = {
                    new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor),
                    new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground)
                }
            });

            _songListBoxInstance.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.FilteredSongs"));
            _songListBoxInstance.Bind(ListBox.SelectedItemProperty, new Binding("Library.SelectedSong", BindingMode.TwoWay));

            // Initial template is set by MainWindow after DataContext is available.
            // Setting a default here might be briefly visible but will be overridden.
            _songListBoxInstance.ItemTemplate = _songListTemplates.DetailedSongTemplate;
            _songListBoxInstance.ItemsPanel = _songListTemplates.StackPanelItemsPanelTemplate;

            return new ScrollViewer { Content = _songListBoxInstance, Padding = new Thickness(0, 0, 0, 5) };
        }

        public void UpdateSongListDisplayMode(SongDisplayMode mode, ListBox songListBox)
        {
            if (songListBox == null)
            {
                Debug.WriteLine("[MainTabViewControls] UpdateSongListDisplayMode called but songListBox is null.");
                return;
            }

            Debug.WriteLine($"[MainTabViewControls] Applying song display mode: {mode}");
            var scrollViewer = songListBox.Parent as ScrollViewer;

            switch (mode)
            {
                case SongDisplayMode.Detailed:
                    songListBox.ItemTemplate = _songListTemplates.DetailedSongTemplate;
                    songListBox.ItemsPanel = _songListTemplates.StackPanelItemsPanelTemplate;
                    if (scrollViewer != null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    break;
                case SongDisplayMode.Compact:
                    songListBox.ItemTemplate = _songListTemplates.CompactSongTemplate;
                    songListBox.ItemsPanel = _songListTemplates.StackPanelItemsPanelTemplate;
                    if (scrollViewer != null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    break;
                case SongDisplayMode.Grid:
                    songListBox.ItemTemplate = _songListTemplates.GridSongTemplate;
                    songListBox.ItemsPanel = _songListTemplates.WrapPanelItemsPanelTemplate;
                    if (scrollViewer != null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled; // Changed from .Auto to .Disabled
                    break;
            }
        }

        private ScrollViewer CreateArtistsListScrollViewer()
        {
            var artistsListBox = new ListBox
            {
                Background = _theme.B_ListBoxBackground,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(10)
            };

            artistsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>())
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground), new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor) } });
            artistsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) } });
            artistsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor), new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground) } });
            artistsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor), new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground) } });

            artistsListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.Artists"));
            artistsListBox.Bind(ListBox.SelectedItemProperty, new Binding("Library.SelectedArtist", BindingMode.TwoWay));

            artistsListBox.ItemTemplate = new FuncDataTemplate<ArtistViewModel>((artistVM, nameScope) =>
            {
                var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 10, 0), Source = artistVM.Thumbnail, Stretch = Stretch.UniformToFill };
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                var artistNameBlock = new TextBlock { Text = artistVM.Name, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
                var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Center };
                itemGrid.Children.Add(image); itemGrid.Children.Add(artistNameBlock);
                Grid.SetColumn(image, 0); Grid.SetColumn(artistNameBlock, 1);
                return new Border { Padding = new Thickness(10, 8), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
            }, supportsRecycling: true);

            return new ScrollViewer { Content = artistsListBox, Padding = new Thickness(0, 0, 0, 5) };
        }

        private ScrollViewer CreateAlbumsListScrollViewer()
        {
            var albumsListBox = new ListBox
            {
                Background = _theme.B_ListBoxBackground,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(10)
            };

            albumsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>())
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ListBoxBackground), new Setter(TextBlock.ForegroundProperty, _theme.B_TextColor) } });
            albumsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":pointerover").Not(xx => xx.Class(":selected")))
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_ControlBackgroundColor) } });
            albumsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected"))
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor), new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground) } });
            albumsListBox.Styles.Add(new Style(s => s.Is<ListBoxItem>().Class(":selected").Class(":pointerover"))
            { Setters = { new Setter(TemplatedControl.BackgroundProperty, _theme.B_AccentColor), new Setter(TextBlock.ForegroundProperty, _theme.B_AccentForeground) } });

            albumsListBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.Albums"));
            albumsListBox.Bind(ListBox.SelectedItemProperty, new Binding("Library.SelectedAlbum", BindingMode.TwoWay));

            albumsListBox.ItemTemplate = new FuncDataTemplate<AlbumViewModel>((albumVM, nameScope) =>
            {
                var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 10, 0), Source = albumVM.Thumbnail, Stretch = Stretch.UniformToFill };
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                var albumTitleBlock = new TextBlock { Text = albumVM.Title, FontSize = 14, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
                var albumArtistBlock = new TextBlock { Text = albumVM.Artist, FontSize = 11, Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center };
                var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Children = { albumTitleBlock, albumArtistBlock } };
                var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Center };
                itemGrid.Children.Add(image); itemGrid.Children.Add(textStack);
                Grid.SetColumn(image, 0); Grid.SetColumn(textStack, 1);
                return new Border { Padding = new Thickness(10, 6), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
            }, supportsRecycling: true);

            return new ScrollViewer { Content = albumsListBox, Padding = new Thickness(0, 0, 0, 5) };
        }
    }
}