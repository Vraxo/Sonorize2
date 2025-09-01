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

    public FuncDataTemplate<Song> GridSongTemplate { get; private set; }

    public SongItemTemplateProvider(ThemeColors theme)
    {
        _theme = theme;
        Debug.WriteLine("[SongItemTemplateProvider] Initialized.");
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