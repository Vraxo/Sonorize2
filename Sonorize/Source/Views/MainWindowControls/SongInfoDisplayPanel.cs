using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging; // Required for BitmapInterpolationMode
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.MainWindowControls;

public static class SongInfoDisplayPanel
{
    public static Grid Create(ThemeColors theme)
    {
        var songInfoGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto, *"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(10, 0, 10, 0),
        };
        songInfoGrid.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        var thumbnailImage = new Image
        {
            Width = 64,
            Height = 64,
            Source = null, // Will be bound
            Stretch = Stretch.UniformToFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        RenderOptions.SetBitmapInterpolationMode(thumbnailImage, BitmapInterpolationMode.HighQuality);
        thumbnailImage.Bind(Image.SourceProperty, new Binding("Playback.CurrentSong.Thumbnail"));
        Grid.SetColumn(thumbnailImage, 0);

        var textStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 1,
            Margin = new Thickness(8, 0, 0, 0) // Replaces ColumnSpacing
        };
        Grid.SetColumn(textStack, 1);

        var titleTextBlock = new TextBlock
        {
            Text = "Unknown Title", // Default, will be bound
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.B_TextColor,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        titleTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Title"));

        var artistTextBlock = new TextBlock
        {
            Text = "Unknown Artist", // Default, will be bound
            FontSize = 11,
            Foreground = theme.B_SecondaryTextColor,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        artistTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Artist"));

        textStack.Children.Add(titleTextBlock);
        textStack.Children.Add(artistTextBlock);

        songInfoGrid.Children.Add(thumbnailImage);
        songInfoGrid.Children.Add(textStack);

        return songInfoGrid;
    }
}