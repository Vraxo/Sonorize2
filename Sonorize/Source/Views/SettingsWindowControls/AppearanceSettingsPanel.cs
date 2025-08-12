using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models;

namespace Sonorize.Views.SettingsWindowControls;

public static class AppearanceSettingsPanel
{
    public static StackPanel Create(ThemeColors theme)
    {
        var panel = new StackPanel { Spacing = 15 };

        panel.Children.Add(new TextBlock
        {
            Text = "Appearance",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 5)
        });

        panel.Children.Add(CreateGridImageTypeSelector(theme, "Artist", "AppearanceSettings.IsArtistGridSingle", "AppearanceSettings.IsArtistGridComposite"));
        panel.Children.Add(CreateGridImageTypeSelector(theme, "Album", "AppearanceSettings.IsAlbumGridSingle", "AppearanceSettings.IsAlbumGridComposite"));
        panel.Children.Add(CreateGridImageTypeSelector(theme, "Playlist", "AppearanceSettings.IsPlaylistGridSingle", "AppearanceSettings.IsPlaylistGridComposite"));

        return panel;
    }

    private static StackPanel CreateGridImageTypeSelector(ThemeColors theme, string label, string singleBindingPath, string compositeBindingPath)
    {
        var sectionPanel = new StackPanel { Spacing = 8 };

        var title = new TextBlock
        {
            Text = $"{label} Grid Image Style",
            FontSize = 14,
            FontWeight = FontWeight.Normal,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 5)
        };

        var singleRadio = new RadioButton
        {
            Content = "Single Representative Image",
            GroupName = $"{label}GridStyle",
            Foreground = theme.B_TextColor
        };
        singleRadio.Bind(RadioButton.IsCheckedProperty, new Binding(singleBindingPath, BindingMode.TwoWay));

        var compositeRadio = new RadioButton
        {
            Content = "4-Image Composite (if available)",
            GroupName = $"{label}GridStyle",
            Foreground = theme.B_TextColor
        };
        compositeRadio.Bind(RadioButton.IsCheckedProperty, new Binding(compositeBindingPath, BindingMode.TwoWay));

        var radioPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5, Margin = new Thickness(10, 0, 0, 0) };
        radioPanel.Children.Add(singleRadio);
        radioPanel.Children.Add(compositeRadio);

        sectionPanel.Children.Add(title);
        sectionPanel.Children.Add(radioPanel);
        sectionPanel.Children.Add(new Separator { Background = theme.B_ControlBackgroundColor, Margin = new Thickness(0, 5) });

        return sectionPanel;
    }
}
