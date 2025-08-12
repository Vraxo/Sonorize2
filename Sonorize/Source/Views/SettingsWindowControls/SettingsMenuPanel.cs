using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.SettingsWindowControls;

public static class SettingsMenuPanel
{
    public static Border Create(ThemeColors theme)
    {
        var menuStackPanel = new StackPanel
        {
            Spacing = 5
        };

        var directoriesButton = new Button { Content = "Directories", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        directoriesButton.Bind(Button.CommandProperty, new Binding("ShowDirectoriesSettingsCommand"));

        var themeButton = new Button { Content = "Theme", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        themeButton.Bind(Button.CommandProperty, new Binding("ShowThemeSettingsCommand"));
        
        var appearanceButton = new Button { Content = "Appearance", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        appearanceButton.Bind(Button.CommandProperty, new Binding("ShowAppearanceSettingsCommand"));

        var scrobblingButton = new Button { Content = "Scrobbling", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        scrobblingButton.Bind(Button.CommandProperty, new Binding("ShowScrobblingSettingsCommand"));

        menuStackPanel.Children.Add(directoriesButton);
        menuStackPanel.Children.Add(themeButton);
        menuStackPanel.Children.Add(appearanceButton);
        menuStackPanel.Children.Add(scrobblingButton);

        var menuBorder = new Border
        {
            Background = theme.B_BackgroundColor,
            Padding = new Thickness(10),
            Child = menuStackPanel
        };

        return menuBorder;
    }
}
