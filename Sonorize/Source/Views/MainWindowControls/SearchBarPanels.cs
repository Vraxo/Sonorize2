using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Styling;
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.MainWindowControls;

public static class SearchBarPanel
{
    public static Panel Create(ThemeColors theme)
    {
        var searchBox = new TextBox
        {
            Watermark = "Search songs by title, artist, or album...",
            Margin = new Thickness(10, 5, 10, 5),
            Padding = new Thickness(10, 7),
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            FontSize = 14
        };
        // Bind to Library.FilterState.SearchQuery
        searchBox.Bind(TextBox.TextProperty, new Binding("Library.FilterState.SearchQuery", BindingMode.TwoWay));

        searchBox.Styles.Add(new Style(s => s.Is<TextBox>().Class(":focus"))
        {
            Setters = { new Setter(TextBox.BorderBrushProperty, theme.B_AccentColor) }
        });

        var panel = new Panel
        {
            Children = { searchBox },
            Margin = new Thickness(0, 5, 0, 0)
        };
        return panel;
    }
}