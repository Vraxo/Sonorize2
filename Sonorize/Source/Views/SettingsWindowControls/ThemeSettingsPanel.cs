using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media; // Required for FontStyle, TextWrapping
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.SettingsWindowControls;

public static class ThemeSettingsPanel
{
    public static StackPanel Create(ThemeColors theme)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = "Application Theme",
            FontSize = 18,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.B_TextColor,
            Margin = new Thickness(0, 0, 0, 10)
        });

        var themeComboBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Background = theme.B_ControlBackgroundColor,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_SecondaryTextColor
        };
        themeComboBox.Bind(ItemsControl.ItemsSourceProperty, new Binding("AvailableThemes"));
        themeComboBox.Bind(ComboBox.SelectedItemProperty, new Binding("SelectedThemeFile", BindingMode.TwoWay));
        panel.Children.Add(themeComboBox);

        panel.Children.Add(new TextBlock
        {
            Text = "A restart is required for theme changes to take full effect.",
            FontSize = 10,
            Foreground = theme.B_SecondaryTextColor,
            FontStyle = FontStyle.Italic,
            Margin = new Thickness(0, 5, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });
        return panel;
    }
}