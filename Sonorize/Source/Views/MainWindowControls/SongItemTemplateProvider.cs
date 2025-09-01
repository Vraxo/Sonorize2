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
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Data.Converters;
using Sonorize.Converters; // Added for BooleanToGridLengthConverter

namespace Sonorize.Views.MainWindowControls;

public class SongItemTemplateProvider
{
    private readonly ThemeColors _theme;

    public FuncDataTemplate<Song> DetailedSongTemplate { get; private set; }
    public FuncDataTemplate<Song> CompactSongTemplate { get; private set; }
    public FuncDataTemplate<Song> GridSongTemplate { get; private set; }

    public SongItemTemplateProvider(ThemeColors theme)
    {
        _theme = theme;
        Debug.WriteLine("[SongItemTemplateProvider] Initialized.");
        InitializeSongTemplates();
    }

    private void InitializeSongTemplates()
    {
        DetailedSongTemplate = CreateListSongTemplate(isDetailed: true);
        CompactSongTemplate = CreateListSongTemplate(isDetailed: false);
        GridSongTemplate = CreateGridSongTemplate();
    }

    private void OnRootBorderContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not Border border || border.DataContext is not Song song)
        {
            return;
        }

        var mainWindowViewModel = (border.FindAncestorOfType<Window>())?.DataContext as MainWindowViewModel;
        if (mainWindowViewModel == null)
        {
            return;
        }

        var flyout = new MenuFlyout();
        var editMenuItem = new MenuItem
        {
            Header = "Edit Metadata",
            Command = mainWindowViewModel.OpenEditSongMetadataDialogCommand,
            CommandParameter = song
        };

        flyout.Items.Add(editMenuItem);

        flyout.ShowAt(border);
        e.Handled = true;
    }

    private FuncDataTemplate<Song> CreateListSongTemplate(bool isDetailed)
    {
        return new FuncDataTemplate<Song>((dataContext, nameScope) =>
        {
            var itemGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };

            // Create a proxy element. It will be in the visual tree and get a DataContext.
            var proxy = new Border { Name = "Proxy", IsVisible = false, DataContext = dataContext };
            itemGrid.Children.Add(proxy);

            var rootBorder = new Border
            {
                Padding = new Thickness(0, isDetailed ? 8 : 4),
                Background = Brushes.Transparent,
                Child = itemGrid
            };

            // Bind MinHeight using the proxy as a stable binding source.
            rootBorder.Bind(Border.MinHeightProperty, new Binding("DataContext.ViewOptions.RowHeight")
            {
                Source = proxy,
                FallbackValue = 44.0
            });

            // --- Column Definitions ---
            var columns = itemGrid.ColumnDefinitions;

            if (isDetailed)
            {
                columns.Add(new ColumnDefinition(32 + 10 + 10, GridUnitType.Pixel)); // 0: Image
                columns.Add(new ColumnDefinition(3, GridUnitType.Star));             // 1: Title
            }
            else
            {
                columns.Add(new ColumnDefinition(10, GridUnitType.Pixel)); // 0: Left Padding
                columns.Add(new ColumnDefinition(2, GridUnitType.Star));    // 1: Title
            }

            // Artist Column
            var artistCol = new ColumnDefinition();
            artistCol.Bind(ColumnDefinition.WidthProperty, new Binding("DataContext.ViewOptions.ShowArtist")
            {
                Source = proxy,
                FallbackValue = GridLength.Parse("1.5*"),
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = "1.5*"
            });
            columns.Add(artistCol);

            // Album Column
            var albumCol = new ColumnDefinition();
            albumCol.Bind(ColumnDefinition.WidthProperty, new Binding("DataContext.ViewOptions.ShowAlbum")
            {
                Source = proxy,
                FallbackValue = GridLength.Parse("1.5*"),
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = "1.5*"
            });
            columns.Add(albumCol);

            // Spacer Column
            columns.Add(new ColumnDefinition(GridLength.Star));

            // Play Count Column
            var playCountCol = new ColumnDefinition();
            playCountCol.Bind(ColumnDefinition.WidthProperty, new Binding("DataContext.ViewOptions.ShowPlayCount")
            {
                Source = proxy,
                FallbackValue = new GridLength(0),
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = "Auto"
            });
            columns.Add(playCountCol);

            // Date Added Column
            var dateAddedCol = new ColumnDefinition();
            dateAddedCol.Bind(ColumnDefinition.WidthProperty, new Binding("DataContext.ViewOptions.ShowDateAdded")
            {
                Source = proxy,
                FallbackValue = new GridLength(0),
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = "Auto"
            });
            columns.Add(dateAddedCol);

            // Duration Column
            var durationCol = new ColumnDefinition();
            durationCol.Bind(ColumnDefinition.WidthProperty, new Binding("DataContext.ViewOptions.ShowDuration")
            {
                Source = proxy,
                FallbackValue = GridLength.Auto,
                Converter = BooleanToGridLengthConverter.Instance,
                ConverterParameter = "Auto"
            });
            columns.Add(durationCol);


            // --- Controls ---
            int currentColumn = 0;

            if (isDetailed)
            {
                var image = new Image { Width = 32, Height = 32, Margin = new Thickness(10, 0), Stretch = Stretch.UniformToFill };
                image.Bind(Image.SourceProperty, new Binding(nameof(Song.Thumbnail)));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                Grid.SetColumn(image, currentColumn++);
                itemGrid.Children.Add(image);
            }
            else
            {
                currentColumn++; // Skip the padding column
            }

            var titleBlock = new TextBlock { Padding = new Thickness(10, 0), HorizontalAlignment = HorizontalAlignment.Stretch, TextAlignment = TextAlignment.Left, FontSize = isDetailed ? 14 : 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Title)));
            Grid.SetColumn(titleBlock, currentColumn++);
            itemGrid.Children.Add(titleBlock);

            var artistBlock = new TextBlock { Padding = new Thickness(10, 0), HorizontalAlignment = HorizontalAlignment.Stretch, TextAlignment = TextAlignment.Left, FontSize = isDetailed ? 12 : 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis };
            artistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Artist)));
            Grid.SetColumn(artistBlock, currentColumn++);
            itemGrid.Children.Add(artistBlock);

            var albumBlock = new TextBlock { Padding = new Thickness(10, 0), HorizontalAlignment = HorizontalAlignment.Stretch, TextAlignment = TextAlignment.Left, FontSize = isDetailed ? 12 : 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis };
            albumBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Album)));
            Grid.SetColumn(albumBlock, currentColumn++);
            itemGrid.Children.Add(albumBlock);

            currentColumn++; // Skip Spacer column

            var playCountBlock = new TextBlock { Padding = new Thickness(10, 0), HorizontalAlignment = HorizontalAlignment.Stretch, TextAlignment = TextAlignment.Right, FontSize = isDetailed ? 12 : 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis };
            playCountBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.PlayCount)));
            Grid.SetColumn(playCountBlock, currentColumn++);
            itemGrid.Children.Add(playCountBlock);

            var dateAddedBlock = new TextBlock { Padding = new Thickness(10, 0), HorizontalAlignment = HorizontalAlignment.Stretch, TextAlignment = TextAlignment.Right, FontSize = isDetailed ? 12 : 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis };
            dateAddedBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DateAdded)) { StringFormat = "{0:yyyy-MM-dd}" });
            Grid.SetColumn(dateAddedBlock, currentColumn++);
            itemGrid.Children.Add(dateAddedBlock);

            var durationBlock = new TextBlock { Padding = new Thickness(10, 0), HorizontalAlignment = HorizontalAlignment.Stretch, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            durationBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DurationString)));
            Grid.SetColumn(durationBlock, currentColumn++);
            itemGrid.Children.Add(durationBlock);

            rootBorder.ContextRequested += OnRootBorderContextRequested;
            return rootBorder;
        }, supportsRecycling: true);
    }

    private FuncDataTemplate<Song> CreateGridSongTemplate()
    {
        return new FuncDataTemplate<Song>((dataContext, nameScope) =>
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

            rootBorder.ContextRequested += OnRootBorderContextRequested;
            return rootBorder;
        }, supportsRecycling: true);
    }
}