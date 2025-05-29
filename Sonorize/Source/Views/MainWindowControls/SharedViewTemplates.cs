using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sonorize.Models;
using Sonorize.ViewModels;
using System.Diagnostics;

namespace Sonorize.Views.MainWindowControls
{
    public class SharedViewTemplates
    {
        private readonly ThemeColors _theme;
        private LibraryViewModel? _libraryVM;
        private readonly SongContextMenuHelper _contextMenuHelper;


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
            // Pass a lambda to SongContextMenuHelper that will retrieve the current _libraryVM
            _contextMenuHelper = new SongContextMenuHelper(_theme, () => _libraryVM);
            Debug.WriteLine("[SharedViewTemplates] Constructor called.");
            InitializeSongTemplates();
            InitializeArtistTemplates();
            InitializeAlbumTemplates();
            InitializePanelTemplates();
        }

        public void SetLibraryViewModel(LibraryViewModel? libraryVM)
        {
            _libraryVM = libraryVM;
            Debug.WriteLine($"[SharedViewTemplates] SetLibraryViewModel explicitly called. _libraryVM (for helper's fallback func) is now {(_libraryVM == null ? "NULL" : "NOT NULL")}.");
        }

        private void InitializeSongTemplates()
        {
            // Detailed Song Template
            DetailedSongTemplate = new FuncDataTemplate<Song>((dataContext, nameScope) =>
            {
                var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 5, 0), Stretch = Stretch.UniformToFill };
                image.Bind(Image.SourceProperty, new Binding(nameof(Song.Thumbnail)));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

                var titleBlock = new TextBlock { FontSize = 14, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
                titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Title)));

                var artistBlock = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
                artistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Artist)));

                var durationBlock = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
                durationBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DurationString)));

                var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Children = { titleBlock, artistBlock } };
                var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center, Children = { image, textStack, durationBlock } };
                Grid.SetColumn(image, 0); Grid.SetColumn(textStack, 1); Grid.SetColumn(durationBlock, 2);

                var rootBorder = new Border { Padding = new Thickness(10, 6, 10, 6), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
                rootBorder.ContextMenu = _contextMenuHelper.CreateContextMenu(dataContext);
                return rootBorder;
            }, supportsRecycling: true);

            // Compact Song Template
            CompactSongTemplate = new FuncDataTemplate<Song>((dataContext, nameScope) =>
            {
                var titleBlock = new TextBlock { FontSize = 12, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Title)));

                var artistBlock = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(5, 0, 0, 0) };
                artistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Artist)) { StringFormat = " - {0}" });


                var titleArtistPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { titleBlock, artistBlock } };

                var durationBlock = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, Margin = new Thickness(5, 0, 0, 0) };
                durationBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DurationString)));

                var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), VerticalAlignment = VerticalAlignment.Center };
                itemGrid.Children.Add(titleArtistPanel); itemGrid.Children.Add(durationBlock);
                Grid.SetColumn(titleArtistPanel, 0); Grid.SetColumn(durationBlock, 1);

                var rootBorder = new Border { Padding = new Thickness(10, 4, 10, 4), MinHeight = 30, Background = Brushes.Transparent, Child = itemGrid };
                rootBorder.ContextMenu = _contextMenuHelper.CreateContextMenu(dataContext);
                return rootBorder;
            }, supportsRecycling: true);

            // Grid Song Template
            GridSongTemplate = new FuncDataTemplate<Song>((dataContext, nameScope) =>
            {
                var image = new Image { Width = 80, Height = 80, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center };
                image.Bind(Image.SourceProperty, new Binding(nameof(Song.Thumbnail)));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

                var titleBlock = new TextBlock
                {
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Height = 32,
                    MaxLines = 2,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0),
                    VerticalAlignment = VerticalAlignment.Top
                };
                titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Title)));

                var artistBlock = new TextBlock { FontSize = 10, Foreground = _theme.B_SecondaryTextColor, TextWrapping = TextWrapping.Wrap, MaxHeight = 15, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 1, 0, 0) };
                artistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Artist)));

                var contentStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2, Children = { image, titleBlock, artistBlock } };

                var rootBorder = new Border { Width = 120, Height = 150, Background = Brushes.Transparent, Padding = new Thickness(5), Child = contentStack, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                rootBorder.ContextMenu = _contextMenuHelper.CreateContextMenu(dataContext);
                return rootBorder;
            }, supportsRecycling: true);
        }

        private void InitializeArtistTemplates()
        {
            DetailedArtistTemplate = new FuncDataTemplate<ArtistViewModel>((dataContext, nameScope) =>
            {
                var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 10, 0), Stretch = Stretch.UniformToFill };
                image.Bind(Image.SourceProperty, new Binding(nameof(ArtistViewModel.Thumbnail)));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

                var artistNameBlock = new TextBlock { FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
                artistNameBlock.Bind(TextBlock.TextProperty, new Binding(nameof(ArtistViewModel.Name)));

                var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), VerticalAlignment = VerticalAlignment.Center };
                itemGrid.Children.Add(image); itemGrid.Children.Add(artistNameBlock);
                Grid.SetColumn(image, 0); Grid.SetColumn(artistNameBlock, 1);
                return new Border { Padding = new Thickness(10, 8), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
            }, supportsRecycling: true);

            CompactArtistTemplate = new FuncDataTemplate<ArtistViewModel>((dataContext, nameScope) =>
            {
                var artistNameBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                artistNameBlock.Bind(TextBlock.TextProperty, new Binding(nameof(ArtistViewModel.Name)));
                return new Border { Padding = new Thickness(10, 4, 10, 4), MinHeight = 30, Background = Brushes.Transparent, Child = artistNameBlock };
            }, supportsRecycling: true);

            GridArtistTemplate = new FuncDataTemplate<ArtistViewModel>((dataContext, nameScope) =>
            {
                var image = new Image { Width = 80, Height = 80, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center };
                image.Bind(Image.SourceProperty, new Binding(nameof(ArtistViewModel.Thumbnail)));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

                var artistNameBlock = new TextBlock { FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 30, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
                artistNameBlock.Bind(TextBlock.TextProperty, new Binding(nameof(ArtistViewModel.Name)));

                var contentStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2, Children = { image, artistNameBlock } };
                return new Border { Width = 120, Height = 130, Background = Brushes.Transparent, Padding = new Thickness(5), Child = contentStack, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            }, supportsRecycling: true);
        }

        private void InitializeAlbumTemplates()
        {
            DetailedAlbumTemplate = new FuncDataTemplate<AlbumViewModel>((dataContext, nameScope) =>
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
                    var img = new Image { Width = 28, Height = 28, Stretch = Stretch.UniformToFill };
                    img.Bind(Image.SourceProperty, new Binding($"SongThumbnailsForGrid[{i}]"));
                    RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
                    Grid.SetRow(img, i / 2);
                    Grid.SetColumn(img, i % 2);
                    imageGrid.Children.Add(img);
                }

                Grid.SetColumn(imageGrid, 0);
                itemGrid.Children.Add(imageGrid);

                var albumTitleBlock = new TextBlock { FontSize = 14, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
                albumTitleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(AlbumViewModel.Title)));

                var albumArtistBlock = new TextBlock { FontSize = 11, Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center };
                albumArtistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(AlbumViewModel.Artist)));

                var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Children = { albumTitleBlock, albumArtistBlock } };

                Grid.SetColumn(textStack, 1);
                itemGrid.Children.Add(textStack);

                return new Border { Padding = new Thickness(10, 6), MinHeight = 68, Background = Brushes.Transparent, Child = itemGrid };
            }, supportsRecycling: true);

            CompactAlbumTemplate = new FuncDataTemplate<AlbumViewModel>((dataContext, nameScope) =>
            {
                var albumTitleBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                albumTitleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(AlbumViewModel.Title)));

                var albumArtistBlock = new TextBlock { FontSize = 11, Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(5, 0, 0, 0) };
                albumArtistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(AlbumViewModel.Artist)) { StringFormat = " - {0}" });

                var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { albumTitleBlock, albumArtistBlock } };
                return new Border { Padding = new Thickness(10, 4, 10, 4), MinHeight = 30, Background = Brushes.Transparent, Child = panel };
            }, supportsRecycling: true);

            GridAlbumTemplate = new FuncDataTemplate<AlbumViewModel>((dataContext, nameScope) =>
            {
                var contentStack = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Spacing = 3
                };

                var imagePresenter = new Panel { Width = 80, Height = 80, HorizontalAlignment = HorizontalAlignment.Center };

                if (dataContext != null && dataContext.SongThumbnailsForGrid != null && dataContext.SongThumbnailsForGrid.Count(t => t != null) > 1)
                {
                    var imageGrid = new Grid
                    {
                        Width = 80,
                        Height = 80,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        ColumnDefinitions = new ColumnDefinitions("*,*"),
                        RowDefinitions = new RowDefinitions("*,*")
                    };

                    for (int i = 0; i < 4; i++)
                    {
                        var img = new Image { Width = 38, Height = 38, Stretch = Stretch.UniformToFill, Margin = new Thickness(1) };
                        img.Bind(Image.SourceProperty, new Binding($"SongThumbnailsForGrid[{i}]"));
                        RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
                        Grid.SetRow(img, i / 2);
                        Grid.SetColumn(img, i % 2);
                        imageGrid.Children.Add(img);
                    }
                    imagePresenter.Children.Add(imageGrid);
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
                    singleImage.Bind(Image.SourceProperty, new Binding(nameof(AlbumViewModel.RepresentativeThumbnail)));
                    RenderOptions.SetBitmapInterpolationMode(singleImage, BitmapInterpolationMode.HighQuality);
                    imagePresenter.Children.Add(singleImage);
                }
                contentStack.Children.Add(imagePresenter);

                var albumTitleBlock = new TextBlock
                {
                    FontSize = 12,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 30,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0)
                };
                albumTitleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(AlbumViewModel.Title)));
                contentStack.Children.Add(albumTitleBlock);

                var albumArtistBlock = new TextBlock
                {
                    FontSize = 10,
                    Foreground = _theme.B_SecondaryTextColor,
                    TextWrapping = TextWrapping.Wrap,
                    MaxHeight = 15,
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                albumArtistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(AlbumViewModel.Artist)));
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
            WrapPanelItemsPanelTemplate = new FuncTemplate<Panel?>(() => new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = 130, ItemHeight = 160 });
        }
    }
}