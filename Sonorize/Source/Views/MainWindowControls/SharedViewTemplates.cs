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
using Avalonia.VisualTree; // Required for FindAncestorOfType
using System.Diagnostics; // For Debug.WriteLine

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

        private ContextMenu CreateSongContextMenu()
        {
            var contextMenu = new ContextMenu();
            var editMenuItem = new MenuItem { Header = "View/Edit Metadata" };

            // Command will bind to EditSongMetadataCommand on the LibraryViewModel
            // (which will be set as ContextMenu.DataContext or MenuItem.DataContext)
            editMenuItem.Bind(MenuItem.CommandProperty, new Binding("EditSongMetadataCommand"));

            // CommandParameter will be set directly in the Opening event.
            // So, no binding needed here for CommandParameterProperty.

            contextMenu.Items.Add(editMenuItem);

            contextMenu.Opening += (sender, args) => {
                var cm = sender as ContextMenu;
                if (cm == null) return;

                MenuItem? currentEditMenuItem = cm.Items.OfType<MenuItem>().FirstOrDefault(mi => mi.Header?.ToString() == "View/Edit Metadata");
                if (currentEditMenuItem == null) return;

                if (cm.PlacementTarget is Control placementTarget)
                {
                    var songObject = placementTarget.DataContext;
                    currentEditMenuItem.CommandParameter = songObject;

                    var window = placementTarget.FindAncestorOfType<Window>();
                    if (window?.DataContext is MainWindowViewModel mwvm && mwvm.Library != null)
                    {
                        // Set DataContext directly on the MenuItem for more robust binding resolution
                        currentEditMenuItem.DataContext = mwvm.Library;
                    }
                    else
                    {
                        Debug.WriteLine("[ContextMenuOpening] Failed to find LibraryViewModel for MenuItem.DataContext. EditSongMetadataCommand might not work.");
                        currentEditMenuItem.DataContext = null; // Ensure DataContext is cleared if source is not found
                        currentEditMenuItem.CommandParameter = null;
                    }
                }
                else
                {
                    currentEditMenuItem.DataContext = null;
                    currentEditMenuItem.CommandParameter = null;
                }
            };

            return contextMenu;
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
                rootBorder.ContextMenu = CreateSongContextMenu();
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
                rootBorder.ContextMenu = CreateSongContextMenu();
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
                    Height = 32, // Adjusted height for two lines
                    MaxLines = 2, // Explicitly set max lines
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
                rootBorder.ContextMenu = CreateSongContextMenu();
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
                    // Binding to an indexed property of a List<Bitmap?>.
                    // This relies on AlbumViewModel.SongThumbnailsForGrid property itself raising PropertyChanged if the list instance changes,
                    // or if AlbumViewModel.SongThumbnailsForGrid was an ObservableCollection and its items change.
                    // Given current AlbumViewModel setup, this will show initial state.
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

                // Logic to decide 2x2 or single image based on bound data needs to be cleaner if done in C# template.
                // For simplicity, binding to RepresentativeThumbnail (which AlbumViewModel should prepare).
                // A more complex template might use a ContentControl with a StyleSelector or multiple DataTemplates.

                // Create a placeholder for the image part, could be a Grid or single Image
                var imagePresenter = new Panel { Width = 80, Height = 80, HorizontalAlignment = HorizontalAlignment.Center };


                // Attempting a dynamic switch based on data. This is complex in FuncDataTemplate.
                // A common approach is to have the ViewModel provide a single "display-ready" thumbnail or a type indicator.
                // Here, we'll just bind to RepresentativeThumbnail for grid view.
                // If more complex logic (like showing 2x2) is needed, it should ideally be handled by ViewModel state
                // or a custom control / more sophisticated templating.

                if (dataContext != null && dataContext.SongThumbnailsForGrid != null && dataContext.SongThumbnailsForGrid.Count(t => t != null) > 1)
                {
                    var imageGrid = new Grid
                    {
                        Width = 80, // Keep overall size consistent
                        Height = 80,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        ColumnDefinitions = new ColumnDefinitions("*,*"),
                        RowDefinitions = new RowDefinitions("*,*")
                    };

                    for (int i = 0; i < 4; i++)
                    {
                        var img = new Image { Width = 38, Height = 38, Stretch = Stretch.UniformToFill, Margin = new Thickness(1) }; // Smaller images for grid
                        img.Bind(Image.SourceProperty, new Binding($"SongThumbnailsForGrid[{i}]"));
                        RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
                        Grid.SetRow(img, i / 2);
                        Grid.SetColumn(img, i % 2);
                        imageGrid.Children.Add(img);
                    }
                    imagePresenter.Children.Add(imageGrid);
                }
                else // Show single representative thumbnail
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
            WrapPanelItemsPanelTemplate = new FuncTemplate<Panel?>(() => new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = 130, ItemHeight = 160 }); // Adjusted ItemWidth/Height for grid items
        }
    }
}