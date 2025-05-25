using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sonorize.Models; // For Song, ArtistViewModel, AlbumViewModel, ThemeColors
using Sonorize.ViewModels; // For SongDisplayMode (though not directly used here, context is relevant)

namespace Sonorize.Views.MainWindowControls
{
    public class SharedViewTemplates
    {
        private readonly ThemeColors _theme;

        // Song Templates
        public FuncDataTemplate<Song> DetailedSongTemplate { get; private set; }
        public FuncDataTemplate<Song> CompactSongTemplate { get; private set; }
        public FuncDataTemplate<Song> GridSongTemplate { get; private set; }

        // Artist Templates
        public FuncDataTemplate<ArtistViewModel> DetailedArtistTemplate { get; private set; }
        public FuncDataTemplate<ArtistViewModel> CompactArtistTemplate { get; private set; }
        public FuncDataTemplate<ArtistViewModel> GridArtistTemplate { get; private set; }

        // Album Templates
        public FuncDataTemplate<AlbumViewModel> DetailedAlbumTemplate { get; private set; }
        public FuncDataTemplate<AlbumViewModel> CompactAlbumTemplate { get; private set; }
        public FuncDataTemplate<AlbumViewModel> GridAlbumTemplate { get; private set; }


        // Panel Templates (reusable)
        public ITemplate<Panel?> StackPanelItemsPanelTemplate { get; private set; }
        public ITemplate<Panel?> WrapPanelItemsPanelTemplate { get; private set; }

        public SharedViewTemplates(ThemeColors theme)
        {
            _theme = theme;
            InitializeSongTemplates();
            InitializeArtistTemplates();
            InitializeAlbumTemplates();
            InitializePanelTemplates();
        }

        private void InitializeSongTemplates()
        {
            // Detailed Song Template
            DetailedSongTemplate = new FuncDataTemplate<Song>((song, nameScope) => {
                var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 5, 0), Stretch = Stretch.UniformToFill };
                image[!Image.SourceProperty] = new Binding(nameof(Song.Thumbnail));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                var titleBlock = new TextBlock { Text = song.Title, FontSize = 14, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
                var artistBlock = new TextBlock { Text = song.Artist, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
                var durationBlock = new TextBlock { Text = song.DurationString, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
                var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Children = { titleBlock, artistBlock } };
                var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center, Children = { image, textStack, durationBlock } };
                Grid.SetColumn(image, 0); Grid.SetColumn(textStack, 1); Grid.SetColumn(durationBlock, 2);
                return new Border { Padding = new Thickness(10, 6, 10, 6), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
            }, supportsRecycling: true);

            // Compact Song Template
            CompactSongTemplate = new FuncDataTemplate<Song>((song, nameScope) => {
                var titleBlock = new TextBlock { Text = song.Title, FontSize = 12, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                var artistBlock = new TextBlock { Text = $" - {song.Artist}", FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(5, 0, 0, 0) };
                var titleArtistPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { titleBlock, artistBlock } };
                var durationBlock = new TextBlock { Text = song.DurationString, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, Margin = new Thickness(5, 0, 0, 0) };
                var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), VerticalAlignment = VerticalAlignment.Center };
                itemGrid.Children.Add(titleArtistPanel); itemGrid.Children.Add(durationBlock);
                Grid.SetColumn(titleArtistPanel, 0); Grid.SetColumn(durationBlock, 1);
                return new Border { Padding = new Thickness(10, 4, 10, 4), MinHeight = 30, Background = Brushes.Transparent, Child = itemGrid };
            }, supportsRecycling: true);

            // Grid Song Template
            GridSongTemplate = new FuncDataTemplate<Song>((song, nameScope) => {
                var image = new Image { Width = 80, Height = 80, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center };
                image[!Image.SourceProperty] = new Binding(nameof(Song.Thumbnail));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                var titleBlock = new TextBlock { Text = song.Title, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 30, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
                var artistBlock = new TextBlock { Text = song.Artist, FontSize = 10, Foreground = _theme.B_SecondaryTextColor, TextWrapping = TextWrapping.Wrap, MaxHeight = 15, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
                var contentStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2, Children = { image, titleBlock, artistBlock } };
                return new Border { Width = 120, Height = 150, Background = Brushes.Transparent, Padding = new Thickness(5), Child = contentStack, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }, supportsRecycling: true);
        }

        private void InitializeArtistTemplates()
        {
            DetailedArtistTemplate = new FuncDataTemplate<ArtistViewModel>((artistVM, nameScope) =>
            {
                var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 10, 0), Stretch = Stretch.UniformToFill };
                image[!Image.SourceProperty] = new Binding(nameof(ArtistViewModel.Thumbnail));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                var artistNameBlock = new TextBlock { Text = artistVM.Name, FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
                var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Center };
                itemGrid.Children.Add(image); itemGrid.Children.Add(artistNameBlock);
                Grid.SetColumn(image, 0); Grid.SetColumn(artistNameBlock, 1);
                return new Border { Padding = new Thickness(10, 8), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
            }, supportsRecycling: true);

            CompactArtistTemplate = new FuncDataTemplate<ArtistViewModel>((artistVM, nameScope) =>
            {
                var artistNameBlock = new TextBlock { Text = artistVM.Name, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                return new Border { Padding = new Thickness(10, 4, 10, 4), MinHeight = 30, Background = Brushes.Transparent, Child = artistNameBlock };
            }, supportsRecycling: true);

            GridArtistTemplate = new FuncDataTemplate<ArtistViewModel>((artistVM, nameScope) =>
            {
                var image = new Image { Width = 80, Height = 80, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center };
                image[!Image.SourceProperty] = new Binding(nameof(ArtistViewModel.Thumbnail));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                var artistNameBlock = new TextBlock { Text = artistVM.Name, FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 30, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
                var contentStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2, Children = { image, artistNameBlock } };
                return new Border { Width = 120, Height = 130, Background = Brushes.Transparent, Padding = new Thickness(5), Child = contentStack, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }, supportsRecycling: true);
        }

        private void InitializeAlbumTemplates()
        {
            DetailedAlbumTemplate = new FuncDataTemplate<AlbumViewModel>((albumVM, nameScope) =>
            {
                var itemGrid = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var imageGrid = new Grid
                {
                    Width = 58,
                    Height = 58,
                    Margin = new Thickness(5, 0, 10, 0),
                    ColumnDefinitions = new ColumnDefinitions("*,*"),
                    RowDefinitions = new RowDefinitions("*,*")
                };

                for (int i = 0; i < 4; i++)
                {
                    var img = new Image
                    {
                        Width = 28,
                        Height = 28,
                        Stretch = Stretch.UniformToFill
                    };
                    img.Bind(Image.SourceProperty, new Binding($"SongThumbnailsForGrid[{i}]"));
                    RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
                    Grid.SetRow(img, i / 2);
                    Grid.SetColumn(img, i % 2);
                    imageGrid.Children.Add(img);
                }

                Grid.SetColumn(imageGrid, 0);
                itemGrid.Children.Add(imageGrid);

                var albumTitleBlock = new TextBlock { Text = albumVM.Title, FontSize = 14, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
                var albumArtistBlock = new TextBlock { Text = albumVM.Artist, FontSize = 11, Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center };
                var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Children = { albumTitleBlock, albumArtistBlock } };

                Grid.SetColumn(textStack, 1);
                itemGrid.Children.Add(textStack);

                return new Border { Padding = new Thickness(10, 6), MinHeight = 68, Background = Brushes.Transparent, Child = itemGrid };
            }, supportsRecycling: true);

            CompactAlbumTemplate = new FuncDataTemplate<AlbumViewModel>((albumVM, nameScope) =>
            {
                var albumTitleBlock = new TextBlock { Text = albumVM.Title, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                var albumArtistBlock = new TextBlock { Text = $" - {albumVM.Artist}", FontSize = 11, Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(5, 0, 0, 0) };
                var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { albumTitleBlock, albumArtistBlock } };
                return new Border { Padding = new Thickness(10, 4, 10, 4), MinHeight = 30, Background = Brushes.Transparent, Child = panel };
            }, supportsRecycling: true);

            GridAlbumTemplate = new FuncDataTemplate<AlbumViewModel>((albumVM, nameScope) =>
            {
                var contentStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 3
                };

                bool show2x2Grid = albumVM.SongThumbnailsForGrid != null &&
                                  albumVM.SongThumbnailsForGrid.Count(thumb => thumb != null) >= 2;

                if (show2x2Grid)
                {
                    var imageGrid = new Grid
                    {
                        Width = 84,
                        Height = 84,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        ColumnDefinitions = new ColumnDefinitions("*,*"),
                        RowDefinitions = new RowDefinitions("*,*")
                    };

                    for (int i = 0; i < 4; i++)
                    {
                        var img = new Image
                        {
                            Width = 40,
                            Height = 40,
                            Stretch = Stretch.UniformToFill
                        };
                        img.Bind(Image.SourceProperty, new Binding($"SongThumbnailsForGrid[{i}]"));
                        RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
                        Grid.SetRow(img, i / 2);
                        Grid.SetColumn(img, i % 2);
                        imageGrid.Children.Add(img);
                    }
                    contentStack.Children.Add(imageGrid);
                }
                else
                {
                    var singleImage = new Image
                    {
                        Width = 80,
                        Height = 80,
                        Stretch = Stretch.UniformToFill,
                        HorizontalAlignment = HorizontalAlignment.Center
                    };
                    singleImage[!Image.SourceProperty] = new Binding(nameof(AlbumViewModel.RepresentativeThumbnail));
                    RenderOptions.SetBitmapInterpolationMode(singleImage, BitmapInterpolationMode.HighQuality);
                    contentStack.Children.Add(singleImage);
                }

                var albumTitleBlock = new TextBlock
                {
                    Text = albumVM.Title,
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 30,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                contentStack.Children.Add(albumTitleBlock);

                var albumArtistBlock = new TextBlock
                {
                    Text = albumVM.Artist,
                    FontSize = 10,
                    Foreground = _theme.B_SecondaryTextColor,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 15,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                contentStack.Children.Add(albumArtistBlock);

                return new Border
                {
                    Width = 120,
                    Height = 150,
                    Background = Brushes.Transparent,
                    Padding = new Thickness(5),
                    Child = contentStack,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
            }, supportsRecycling: true);
        }

        private void InitializePanelTemplates()
        {
            StackPanelItemsPanelTemplate = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel { Orientation = Orientation.Vertical });
            WrapPanelItemsPanelTemplate = new FuncTemplate<Panel?>(() => new WrapPanel { Orientation = Orientation.Horizontal });
        }
    }
}