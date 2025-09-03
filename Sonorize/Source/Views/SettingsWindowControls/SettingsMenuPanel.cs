using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.SettingsWindowControls;

public static class SettingsMenuPanel
{
    public static StackPanel Create(ThemeColors theme)
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

        return menuStackPanel;
    }

    public static StackPanel CreateAppearanceSubMenu(ThemeColors theme)
    {
        var menuStackPanel = new StackPanel
        {
            Spacing = 5
        };

        var backButton = new Button { Content = "← Back", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor, FontWeight = FontWeight.Bold };
        backButton.Bind(Button.CommandProperty, new Binding("ShowMainSettingsViewCommand"));

        var libraryListButton = new Button { Content = "Library List", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        libraryListButton.Bind(Button.CommandProperty, new Binding("ShowLibraryListSettingsCommand"));

        var gridViewButton = new Button { Content = "Grid View", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        gridViewButton.Bind(Button.CommandProperty, new Binding("ShowGridViewSettingsCommand"));

        var uiLayoutButton = new Button { Content = "UI Layout", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        uiLayoutButton.Bind(Button.CommandProperty, new Binding("ShowUILayoutSettingsCommand"));

        menuStackPanel.Children.Add(backButton);
        menuStackPanel.Children.Add(new Separator { Margin = new Thickness(0, 5), Background = theme.B_ControlBackgroundColor });
        menuStackPanel.Children.Add(libraryListButton);
        menuStackPanel.Children.Add(gridViewButton);
        menuStackPanel.Children.Add(uiLayoutButton);

        return menuStackPanel;
    }
}