using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sonorize.Models;
using Sonorize.ViewModels; // For Song model if not already included via Sonorize.Models
using System.Diagnostics;

namespace Sonorize.Views.MainWindowControls;

public class SongItemTemplateProvider
{
    private readonly ThemeColors _theme;
    private readonly SongContextMenuHelper _contextMenuHelper;

    public FuncDataTemplate<Song> DetailedSongTemplate { get; private set; }
    public FuncDataTemplate<Song> CompactSongTemplate { get; private set; }
    public FuncDataTemplate<Song> GridSongTemplate { get; private set; }

    public SongItemTemplateProvider(ThemeColors theme, SongContextMenuHelper contextMenuHelper)
    {
        _theme = theme;
        _contextMenuHelper = contextMenuHelper ?? throw new System.ArgumentNullException(nameof(contextMenuHelper));
        Debug.WriteLine("[SongItemTemplateProvider] Initialized.");
        InitializeSongTemplates();
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
            rootBorder.ContextMenu = _contextMenuHelper.CreateSongContextMenu();
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
            rootBorder.ContextMenu = _contextMenuHelper.CreateSongContextMenu();
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
            rootBorder.ContextMenu = _contextMenuHelper.CreateSongContextMenu();
            return rootBorder;
        }, supportsRecycling: true);
    }
}