using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sonorize.Converters;
using Sonorize.Models; // For ThemeColors
using Sonorize.ViewModels; // For ArtistViewModel
using System.Diagnostics;

namespace Sonorize.Views.MainWindowControls;

public class ArtistItemTemplateProvider
{
    private readonly ThemeColors _theme;

    public FuncDataTemplate<ArtistViewModel> DetailedArtistTemplate { get; private set; }
    public FuncDataTemplate<ArtistViewModel> CompactArtistTemplate { get; private set; }
    public FuncDataTemplate<ArtistViewModel> GridArtistTemplate { get; private set; }

    public ArtistItemTemplateProvider(ThemeColors theme)
    {
        _theme = theme;
        Debug.WriteLine("[ArtistItemTemplateProvider] Initialized.");
        InitializeArtistTemplates();
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
                    new Binding(nameof(ArtistViewModel.SongThumbnailsForGrid)),
                    new Binding("LibraryDisplayModeService.ArtistGridDisplayType")
                    {
                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
                        {
                            AncestorType = typeof(Window)
                        }
                    }
                }
            });

            // Single representative image
            var singleImage = new Image { Width = 80, Height = 80, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Center };
            singleImage.Bind(Image.SourceProperty, new Binding(nameof(ArtistViewModel.Thumbnail)));
            RenderOptions.SetBitmapInterpolationMode(singleImage, BitmapInterpolationMode.HighQuality);
            singleImage.Bind(Visual.IsVisibleProperty, new MultiBinding
            {
                Converter = new GridViewImageVisibilityConverter { TargetType = GridViewImageType.Single },
                Bindings =
                {
                    new Binding(nameof(ArtistViewModel.SongThumbnailsForGrid)),
                    new Binding("LibraryDisplayModeService.ArtistGridDisplayType")
                    {
                        RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
                        {
                            AncestorType = typeof(Window)
                        }
                    }
                }
            });

            imagePresenter.Children.Add(imageGrid);
            imagePresenter.Children.Add(singleImage);

            var artistNameBlock = new TextBlock { FontSize = 12, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 30, TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
            artistNameBlock.Bind(TextBlock.TextProperty, new Binding(nameof(ArtistViewModel.Name)));

            var contentStack = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2, Children = { imagePresenter, artistNameBlock } };
            return new Border { Width = 120, Height = 130, Background = Brushes.Transparent, Padding = new Thickness(5), Child = contentStack, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }, supportsRecycling: true);
    }
}