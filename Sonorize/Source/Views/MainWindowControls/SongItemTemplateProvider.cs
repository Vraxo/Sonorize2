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
        var boolToOpacityConverter = new FuncValueConverter<bool, double>(v => v ? 1.0 : 0.0);

        return new FuncDataTemplate<Song>((dataContext, nameScope) =>
        {
            var itemGrid = new Grid { VerticalAlignment = VerticalAlignment.Center };

            var rootBorder = new Border
            {
                Padding = isDetailed ? new Thickness(10, 8) : new Thickness(10, 4, 10, 4),
                Background = Brushes.Transparent,
                Child = itemGrid
            };

            rootBorder.Bind(Border.MinHeightProperty, new Binding("Tag.RowHeight")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });

            // --- Column Definitions ---
            var columns = new ColumnDefinitions();
            if (isDetailed)
            {
                columns.Add(new(GridLength.Auto));          // 0: Image
                columns.Add(new(3, GridUnitType.Star));     // 1: Title
                columns.Add(new(2, GridUnitType.Star));     // 2: Artist
                columns.Add(new(2, GridUnitType.Star));     // 3: Album
            }
            else
            {
                columns.Add(new(2, GridUnitType.Star));     // 0: Title
                columns.Add(new(1.5, GridUnitType.Star));   // 1: Artist
                columns.Add(new(1.5, GridUnitType.Star));   // 2: Album
            }
            columns.Add(new(0.6, GridUnitType.Star));       // Play Count
            columns.Add(new(1.2, GridUnitType.Star));       // Date Added
            columns.Add(new(0.8, GridUnitType.Star));       // Duration
            itemGrid.ColumnDefinitions = columns;

            // --- Controls ---
            int currentColumn = 0;

            if (isDetailed)
            {
                var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 15, 0), Stretch = Stretch.UniformToFill };
                image.Bind(Image.SourceProperty, new Binding(nameof(Song.Thumbnail)));
                RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
                Grid.SetColumn(image, currentColumn++);
                itemGrid.Children.Add(image);
            }

            var titleBlock = new TextBlock { FontSize = isDetailed ? 14 : 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Title)));
            Grid.SetColumn(titleBlock, currentColumn++);
            itemGrid.Children.Add(titleBlock);

            var artistBlock = new TextBlock { FontSize = isDetailed ? 12 : 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            artistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Artist)));
            artistBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowArtist") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Converter = boolToOpacityConverter });
            Grid.SetColumn(artistBlock, currentColumn++);
            itemGrid.Children.Add(artistBlock);

            var albumBlock = new TextBlock { FontSize = isDetailed ? 12 : 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            albumBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Album)));
            albumBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowAlbum") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Converter = boolToOpacityConverter });
            Grid.SetColumn(albumBlock, currentColumn++);
            itemGrid.Children.Add(albumBlock);

            var playCountBlock = new TextBlock { FontSize = isDetailed ? 12 : 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Right };
            playCountBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.PlayCount)));
            playCountBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowPlayCount") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Converter = boolToOpacityConverter });
            Grid.SetColumn(playCountBlock, currentColumn++);
            itemGrid.Children.Add(playCountBlock);

            var dateAddedBlock = new TextBlock { FontSize = isDetailed ? 12 : 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Right };
            dateAddedBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DateAdded)) { StringFormat = "{0:yyyy-MM-dd}" });
            dateAddedBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowDateAdded") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Converter = boolToOpacityConverter });
            Grid.SetColumn(dateAddedBlock, currentColumn++);
            itemGrid.Children.Add(dateAddedBlock);

            var durationBlock = new TextBlock { FontSize = isDetailed ? 12 : 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            durationBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DurationString)));
            durationBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowDuration") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent), Converter = boolToOpacityConverter });
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