using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels;
using System.Diagnostics;

namespace Sonorize.Views.MainWindowControls;

public class SharedViewTemplates
{
    private readonly ThemeColors _theme;
    private readonly SongContextMenuHelper _contextMenuHelper;

    // Expose the provider for Song templates
    public SongItemTemplateProvider SongTemplates { get; private set; }
    // Expose the provider for Artist templates
    public ArtistItemTemplateProvider ArtistTemplates { get; private set; }

    public FuncDataTemplate<PlaylistViewModel> DetailedPlaylistTemplate { get; private set; }
    public FuncDataTemplate<PlaylistViewModel> CompactPlaylistTemplate { get; private set; }
    public FuncDataTemplate<PlaylistViewModel> GridPlaylistTemplate { get; private set; }

    public FuncDataTemplate<AlbumViewModel> DetailedAlbumTemplate { get; private set; }
    public FuncDataTemplate<AlbumViewModel> CompactAlbumTemplate { get; private set; }
    public FuncDataTemplate<AlbumViewModel> GridAlbumTemplate { get; private set; }

    public ITemplate<Panel> StackPanelItemsPanelTemplate { get; private set; }
    public ITemplate<Panel> WrapPanelItemsPanelTemplate { get; private set; }

    public SharedViewTemplates(ThemeColors theme)
    {
        _theme = theme;
        _contextMenuHelper = new SongContextMenuHelper(_theme);
        SongTemplates = new SongItemTemplateProvider(_theme, _contextMenuHelper);
        ArtistTemplates = new ArtistItemTemplateProvider(_theme);

        Debug.WriteLine("[SharedViewTemplates] Constructor called.");
        InitializeAlbumTemplates();
        InitializePlaylistTemplates();
        InitializePanelTemplates();
    }

    private void InitializePlaylistTemplates()
    {
        DetailedPlaylistTemplate = new FuncDataTemplate<PlaylistViewModel>((dataContext, nameScope) =>
        {
            var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center };

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
                var img = new Image { Width = 28, Height = 28, Stretch = Stretch.UniformToFill, Margin = new Thickness(1) };
                img.Bind(Image.SourceProperty, new Binding($"SongThumbnailsForGrid[{i}]"));
                RenderOptions.SetBitmapInterpolationMode(img, BitmapInterpolationMode.HighQuality);
                Grid.SetRow(img, i / 2); Grid.SetColumn(img, i % 2);
                imageGrid.Children.Add(img);
            }
            Grid.SetColumn(imageGrid, 0); itemGrid.Children.Add(imageGrid);

            var nameBlock = new TextBlock { FontSize = 14, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
            nameBlock.Bind(TextBlock.TextProperty, new Binding(nameof(PlaylistViewModel.Name)));
            Grid.SetColumn(nameBlock, 1); itemGrid.Children.Add(nameBlock);

            var countBlock = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            countBlock.Bind(TextBlock.TextProperty, new Binding(nameof(PlaylistViewModel.SongCount)) { StringFormat = "{0} songs" });
            Grid.SetColumn(countBlock, 2); itemGrid.Children.Add(countBlock);

            return new Border { Padding = new Thickness(10, 6), MinHeight = 68, Background = Brushes.Transparent, Child = itemGrid };
        }, supportsRecycling: true);

        CompactPlaylistTemplate = new FuncDataTemplate<PlaylistViewModel>((dataContext, nameScope) =>
        {
            var nameBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            nameBlock.Bind(TextBlock.TextProperty, new Binding(nameof(PlaylistViewModel.Name)));

            var countBlock = new TextBlock { FontSize = 11, Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(5, 0, 0, 0) };
            countBlock.Bind(TextBlock.TextProperty, new Binding(nameof(PlaylistViewModel.SongCount)) { StringFormat = " - {0} songs" });

            var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Children = { nameBlock, countBlock } };
            return new Border { Padding = new Thickness(10, 4, 10, 4), MinHeight = 30, Background = Brushes.Transparent, Child = panel };
        }, supportsRecycling: true);

        GridPlaylistTemplate = new FuncDataTemplate<PlaylistViewModel>((dataContext, nameScope) =>
        {
            var imagePresenter = new Panel { Width = 80, Height = 80, HorizontalAlignment = HorizontalAlignment.Center };

            // Composite 4-image grid
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
                Grid.SetRow(img, i / 2); Grid.SetColumn(img, i % 2);
                imageGrid.Children.Add(img);
            }
            imageGrid.Bind(Visual.IsVisibleProperty, new MultiBinding
            {
                Converter = new GridViewImageVisibilityConverter { TargetType = GridViewImageType.Composite },
                Bindings =
                {
                    new Binding(nameof(PlaylistViewModel.SongThumbnailsForGrid)),
                    new Binding("DataContext.Library.LibraryDisplayModeService.PlaylistGridDisplayType")
                    {
                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) }
                    }
                }
            });

            // Single representative image
            var singleImage = new Image { Width = 80, Height = 80, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center };
            singleImage.Bind(Image.SourceProperty, new Binding(nameof(PlaylistViewModel.RepresentativeThumbnail)));
            RenderOptions.SetBitmapInterpolationMode(singleImage, BitmapInterpolationMode.HighQuality);
            singleImage.Bind(Visual.IsVisibleProperty, new MultiBinding
            {
                Converter = new GridViewImageVisibilityConverter { TargetType = GridViewImageType.Single },
                Bindings =
                {
                    new Binding(nameof(PlaylistViewModel.SongThumbnailsForGrid)),
                    new Binding("DataContext.Library.LibraryDisplayModeService.PlaylistGridDisplayType")
                    {
                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) }
                    }
                }
            });

            imagePresenter.Children.Add(imageGrid);
            imagePresenter.Children.Add(singleImage);

            var contentStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 3
            };
            contentStack.Children.Add(imagePresenter);

            var nameBlock = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 30,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 2, 0, 0)
            };
            nameBlock.Bind(TextBlock.TextProperty, new Binding(nameof(PlaylistViewModel.Name)));
            contentStack.Children.Add(nameBlock);

            var countBlock = new TextBlock
            {
                FontSize = 10,
                Foreground = _theme.B_SecondaryTextColor,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 15,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            countBlock.Bind(TextBlock.TextProperty, new Binding(nameof(PlaylistViewModel.SongCount)) { StringFormat = "{0} songs" });
            contentStack.Children.Add(countBlock);

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
            var imagePresenter = new Panel { Width = 80, Height = 80, HorizontalAlignment = HorizontalAlignment.Center };

            // Composite 4-image grid
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
                Grid.SetRow(img, i / 2); Grid.SetColumn(img, i % 2);
                imageGrid.Children.Add(img);
            }
            imageGrid.Bind(Visual.IsVisibleProperty, new MultiBinding
            {
                Converter = new GridViewImageVisibilityConverter { TargetType = GridViewImageType.Composite },
                Bindings =
                {
                    new Binding(nameof(AlbumViewModel.SongThumbnailsForGrid)),
                    new Binding("DataContext.Library.LibraryDisplayModeService.AlbumGridDisplayType")
                    {
                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) }
                    }
                }
            });

            // Single representative image
            var singleImage = new Image { Width = 80, Height = 80, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center };
            singleImage.Bind(Image.SourceProperty, new Binding(nameof(AlbumViewModel.RepresentativeThumbnail)));
            RenderOptions.SetBitmapInterpolationMode(singleImage, BitmapInterpolationMode.HighQuality);
            singleImage.Bind(Visual.IsVisibleProperty, new MultiBinding
            {
                Converter = new GridViewImageVisibilityConverter { TargetType = GridViewImageType.Single },
                Bindings =
                {
                    new Binding(nameof(AlbumViewModel.SongThumbnailsForGrid)),
                    new Binding("DataContext.Library.LibraryDisplayModeService.AlbumGridDisplayType")
                    {
                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) }
                    }
                }
            });

            imagePresenter.Children.Add(imageGrid);
            imagePresenter.Children.Add(singleImage);

            var contentStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 3
            };
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
        StackPanelItemsPanelTemplate = new FuncTemplate<Panel>(() => new VirtualizingStackPanel { Orientation = Orientation.Vertical });
        WrapPanelItemsPanelTemplate = new FuncTemplate<Panel>(() => new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = 130, ItemHeight = 160 });
    }
}