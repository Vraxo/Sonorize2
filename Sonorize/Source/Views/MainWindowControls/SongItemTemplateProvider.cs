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

    private void InitializeSongTemplates()
    {
        var boolToOpacityConverter = new FuncValueConverter<bool, double>(v => v ? 1.0 : 0.0);

        // Detailed Song Template
        DetailedSongTemplate = new FuncDataTemplate<Song>((dataContext, nameScope) =>
        {
            var itemGrid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            itemGrid.Bind(Control.TagProperty, new Binding("Tag")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });

            // --- Column Definitions (PERF: Now static) ---
            itemGrid.ColumnDefinitions = new ColumnDefinitions
            {
                new(GridLength.Auto),            // 0: Image
                new(3, GridUnitType.Star),       // 1: Title
                new(2, GridUnitType.Star),       // 2: Artist
                new(2, GridUnitType.Star),       // 3: Album
                new(0.6, GridUnitType.Star),     // 4: Play Count
                new(1.2, GridUnitType.Star),     // 5: Date Added
                new(0.8, GridUnitType.Star)      // 6: Duration
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
            // PERF: Bind Opacity instead of IsVisible to avoid layout recalculation.
            artistBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowArtist") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(artistBlock, 2);
            itemGrid.Children.Add(artistBlock);

            var albumBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            albumBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Album)));
            // PERF: Bind Opacity instead of IsVisible.
            albumBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowAlbum") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(albumBlock, 3);
            itemGrid.Children.Add(albumBlock);

            var playCountBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Right };
            playCountBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.PlayCount)));
            // PERF: Bind Opacity instead of IsVisible.
            playCountBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowPlayCount") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(playCountBlock, 4);
            itemGrid.Children.Add(playCountBlock);

            var dateAddedBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Right };
            dateAddedBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DateAdded)) { StringFormat = "{0:yyyy-MM-dd}" });
            // PERF: Bind Opacity instead of IsVisible.
            dateAddedBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowDateAdded") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(dateAddedBlock, 5);
            itemGrid.Children.Add(dateAddedBlock);

            var durationBlock = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            durationBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DurationString)));
            // PERF: Bind Opacity instead of IsVisible.
            durationBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowDuration") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(durationBlock, 6);
            itemGrid.Children.Add(durationBlock);

            var rootBorder = new Border { Padding = new Thickness(10, 8), Background = Brushes.Transparent, Child = itemGrid };
            rootBorder.Bind(Border.MinHeightProperty, new Binding("Tag.RowHeight")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });

            rootBorder.ContextRequested += OnRootBorderContextRequested;
            return rootBorder;
        }, supportsRecycling: true);


        // Compact Song Template
        CompactSongTemplate = new FuncDataTemplate<Song>((dataContext, nameScope) =>
        {
            var itemGrid = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center,
            };

            itemGrid.Bind(Control.TagProperty, new Binding("Tag")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });

            // --- Column Definitions (PERF: Now static) ---
            itemGrid.ColumnDefinitions = new ColumnDefinitions
            {
                new(2, GridUnitType.Star),       // 0: Title
                new(1.5, GridUnitType.Star),     // 1: Artist
                new(1.5, GridUnitType.Star),     // 2: Album
                new(0.6, GridUnitType.Star),     // 3: Play Count
                new(1.2, GridUnitType.Star),     // 4: Date Added
                new(0.8, GridUnitType.Star)      // 5: Duration
            };

            // --- Controls ---
            var titleBlock = new TextBlock { FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            titleBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Title)));
            Grid.SetColumn(titleBlock, 0);
            itemGrid.Children.Add(titleBlock);

            var artistBlock = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            artistBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Artist)));
            // PERF: Bind Opacity instead of IsVisible.
            artistBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowArtist") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(artistBlock, 1);
            itemGrid.Children.Add(artistBlock);

            var albumBlock = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0) };
            albumBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.Album)));
            // PERF: Bind Opacity instead of IsVisible.
            albumBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowAlbum") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(albumBlock, 2);
            itemGrid.Children.Add(albumBlock);

            var playCountBlock = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Right };
            playCountBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.PlayCount)));
            // PERF: Bind Opacity instead of IsVisible.
            playCountBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowPlayCount") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(playCountBlock, 3);
            itemGrid.Children.Add(playCountBlock);

            var dateAddedBlock = new TextBlock { FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 10, 0), HorizontalAlignment = HorizontalAlignment.Right };
            dateAddedBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DateAdded)) { StringFormat = "{0:yyyy-MM-dd}" });
            // PERF: Bind Opacity instead of IsVisible.
            dateAddedBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowDateAdded") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(dateAddedBlock, 4);
            itemGrid.Children.Add(dateAddedBlock);

            var durationBlock = new TextBlock { FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            durationBlock.Bind(TextBlock.TextProperty, new Binding(nameof(Song.DurationString)));
            // PERF: Bind Opacity instead of IsVisible.
            durationBlock.Bind(Visual.OpacityProperty, new Binding("Tag.ShowDuration") { Source = itemGrid, Converter = boolToOpacityConverter });
            Grid.SetColumn(durationBlock, 5);
            itemGrid.Children.Add(durationBlock);

            var rootBorder = new Border { Padding = new Thickness(10, 4, 10, 4), Background = Brushes.Transparent, Child = itemGrid };
            rootBorder.Bind(Border.MinHeightProperty, new Binding("Tag.RowHeight")
            {
                RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
            });

            rootBorder.ContextRequested += OnRootBorderContextRequested;
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

            rootBorder.ContextRequested += OnRootBorderContextRequested;
            return rootBorder;
        }, supportsRecycling: true);
    }
}