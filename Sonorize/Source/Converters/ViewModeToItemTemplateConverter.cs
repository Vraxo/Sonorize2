using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Layout;
using System;
using System.Collections.Generic;
using System.Globalization;
using Sonorize.Models; // Assuming Song is in Models
using Sonorize.ViewModels; // Assuming LibraryViewMode is in ViewModels
using Avalonia.Media.Imaging;
using Avalonia; // For PixelSize, Vector, Rect, Size, Point

namespace Sonorize.Converters;

public class ViewModeToItemTemplateConverter : IValueConverter
{
    // Use dependency injection or pass theme during creation if needed,
    // but for a simple converter in a small app, hardcoded/global access might suffice if theme is static or globally available.
    // For now, let's assume we can access the theme contextually or pass it.
    // A better approach is to pass resources via ConverterParameter or a helper class.
    // Let's define templates here directly, which is less flexible but avoids passing theme.
    // Or, make the Converter accept theme properties via constructor or parameter.
    // Option: Pass the MainWindowViewModel/ThemeColors as ConverterParameter.

    private readonly ThemeColors _theme;
    private readonly Bitmap? _defaultThumbnail; // Use default thumbnail if needed

    public ViewModeToItemTemplateConverter(ThemeColors theme, Bitmap? defaultThumbnail)
    {
        _theme = theme;
        _defaultThumbnail = defaultThumbnail;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LibraryViewMode viewMode && targetType == typeof(IDataTemplate))
        {
            switch (viewMode)
            {
                case LibraryViewMode.Detailed:
                    return CreateDetailedTemplate();
                case LibraryViewMode.Compact:
                    return CreateCompactTemplate();
                case LibraryViewMode.Grid:
                    return CreateGridTemplate();
                default:
                    return CreateDetailedTemplate(); // Fallback
            }
        }
        // If the value isn't a recognized view mode or target isn't IDataTemplate, return null or throw
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Conversion back from template to view mode is not needed for this use case
        return AvaloniaProperty.UnsetValue;
    }

    private IDataTemplate CreateDetailedTemplate()
    {
        // This mirrors the existing detailed template from MainWindow.cs
        return new FuncDataTemplate<Song>((song, nameScope) => {
            var image = new Image { Width = 32, Height = 32, Margin = new Thickness(5, 0, 5, 0), Source = song.Thumbnail, Stretch = Stretch.UniformToFill };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);
            var titleBlock = new TextBlock { Text = song.Title, FontSize = 14, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 1) };
            var artistBlock = new TextBlock { Text = song.Artist, FontSize = 11, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            var durationBlock = new TextBlock { Text = song.DurationString, FontSize = 11, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0), Children = { titleBlock, artistBlock } };
            var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), VerticalAlignment = VerticalAlignment.Center, Children = { image, textStack, durationBlock } };
            Grid.SetColumn(image, 0); Grid.SetColumn(textStack, 1); Grid.SetColumn(durationBlock, 2);
            return new Border { Padding = new Thickness(10, 6, 10, 6), MinHeight = 44, Background = Brushes.Transparent, Child = itemGrid };
        }, supportsRecycling: true);
    }

    private IDataTemplate CreateCompactTemplate()
    {
        // Compact template: No image, thinner padding, smaller font for artist/duration
        return new FuncDataTemplate<Song>((song, nameScope) => {
            var titleBlock = new TextBlock { Text = song.Title, FontSize = 13, FontWeight = FontWeight.Normal, VerticalAlignment = VerticalAlignment.Center };
            var artistBlock = new TextBlock { Text = song.Artist, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            var durationBlock = new TextBlock { Text = song.DurationString, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Foreground = _theme.B_SecondaryTextColor };
            var textStack = new StackPanel { Orientation = Orientation.Vertical, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0), Spacing = 0, Children = { titleBlock, artistBlock } }; // Tighter spacing
            var itemGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), VerticalAlignment = VerticalAlignment.Center, Children = { textStack, durationBlock } };
            Grid.SetColumn(textStack, 0); Grid.SetColumn(durationBlock, 1);

            return new Border { Padding = new Thickness(10, 4, 10, 4), MinHeight = 30, Background = Brushes.Transparent, Child = itemGrid }; // Reduced padding/height
        }, supportsRecycling: true);
    }

    private IDataTemplate CreateGridTemplate()
    {
        // Grid template: Image (album art), Title, Artist below it. Fixed item size might be needed via ListBoxItem styles.
        return new FuncDataTemplate<Song>((song, nameScope) => {
            var image = new Image
            {
                Width = 120, // Example size for grid item thumbnail
                Height = 120,
                Margin = new Thickness(0, 0, 0, 5), // Space below image
                Source = song.Thumbnail ?? _defaultThumbnail, // Use default if song has no art
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

            var titleBlock = new TextBlock
            {
                Text = song.Title,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 1 // Prevent excessive wrapping
            };

            var artistBlock = new TextBlock
            {
                Text = song.Artist,
                FontSize = 10,
                Foreground = _theme.B_SecondaryTextColor,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 1 // Prevent excessive wrapping
            };

            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = 2, // Space between text blocks
                Children = { image, titleBlock, artistBlock }
            };

            // The ListBoxItem style in MainWindow will handle padding/margins/size for the grid cells.
            return new Border { Padding = new Thickness(0), Background = Brushes.Transparent, Child = stack };
        }, supportsRecycling: true);
    }
}