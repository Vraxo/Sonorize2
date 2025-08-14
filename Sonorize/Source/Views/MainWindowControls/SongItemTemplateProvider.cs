using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Sonorize.Converters;
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
            var itemGrid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            // Bind the Grid's Tag to the ViewOptions ViewModel. This works because the Grid is a visual element.
            // Using AncestorType = Window is more robust.
            itemGrid.Bind(Control.TagProperty, new Binding("DataContext.Library.ViewOptions")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) }
            });

            // --- Column Definitions ---
            var artistColumn = new ColumnDefinition();
            artistColumn.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.ShowArtist")
            {
                Source = itemGrid, // Source the binding from the Grid itself, not an ancestor
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = new GridLength(2, GridUnitType.Star)
            });

            var albumColumn = new ColumnDefinition();
            albumColumn.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.ShowAlbum")
            {
                Source = itemGrid,
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = new GridLength(2, GridUnitType.Star)
            });

            var durationColumn = new ColumnDefinition();
            durationColumn.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.ShowDuration")
            {
                Source = itemGrid,
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = GridLength.Auto
            });

            itemGrid.ColumnDefinitions = new ColumnDefinitions
            {
                new(GridLength.Auto), // 0: Image
                new(3, GridUnitType.Star), // 1: Title
                artistColumn, // 2: Artist
                albumColumn, // 3: Album
                durationColumn // 4: Duration
            };

            // --- Controls ---
            var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 15, 0), Stretch = Stretch.UniformToFill };
            image.Bind(Image.SourceProperty, new Binding(nameof(Song.Thumbnail)));
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
            Grid.SetColumn(image, 0);
            itemGrid.Children.Add(image);

            var titleBlock = new TextBlock { FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Title)));
            Grid.SetColumn(titleBlock, 1);
            itemGrid.Children.Add(titleBlock);

            var artistBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            artistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Artist)));
            Grid.SetColumn(artistBlock, 2);
            itemGrid.Children.Add(artistBlock);

            var albumBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            albumBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Album)));
            Grid.SetColumn(albumBlock, 3);
            itemGrid.Children.Add(albumBlock);

            var durationBlock = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            durationBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DurationString)));
            Grid.SetColumn(durationBlock, 4);
            itemGrid.Children.Add(durationBlock);

            var rootBorder = new Border { Padding = new Thickness(10, 8), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
            rootBorder.ContextMenu = _contextMenuHelper.CreateContextMenu(dataContext);
            return rootBorder;
        }, supportsRecycling: true);


        // Compact Song Template
        CompactSongTemplate = new FuncDataTemplate<Song>((dataContext, nameScope) =>
        {
            var itemGrid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
            };

            itemGrid.Bind(Control.TagProperty, new Binding("DataContext.Library.ViewOptions")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor) { AncestorType = typeof(Window) }
            });

            // --- Column Definitions ---
            var artistColumn = new ColumnDefinition();
            artistColumn.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.ShowArtist")
            {
                Source = itemGrid,
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = GridLength.Star
            });

            var albumColumn = new ColumnDefinition();
            albumColumn.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.ShowAlbum")
            {
                Source = itemGrid,
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = GridLength.Star
            });

            var durationColumn = new ColumnDefinition();
            durationColumn.Bind(ColumnDefinition.WidthProperty, new Binding("Tag.ShowDuration")
            {
                Source = itemGrid,
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = GridLength.Auto
            });

            itemGrid.ColumnDefinitions = new ColumnDefinitions
            {
                new(GridLength.Star), // 0: Title
                artistColumn, // 1: Artist
                albumColumn, // 2: Album
                durationColumn // 3: Duration
            };

            // --- Controls ---
            var titleBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Title)));
            Grid.SetColumn(titleBlock, 0);
            itemGrid.Children.Add(titleBlock);

            var artistBlock = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            artistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Artist)));
            Grid.SetColumn(artistBlock, 1);
            itemGrid.Children.Add(artistBlock);

            var albumBlock = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            albumBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Album)));
            Grid.SetColumn(albumBlock, 2);
            itemGrid.Children.Add(albumBlock);

            var durationBlock = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            durationBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DurationString)));
            Grid.SetColumn(durationBlock, 3);
            itemGrid.Children.Add(durationBlock);

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
}