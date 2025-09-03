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

        // --- Appearance Sub-Menu ---
        var appearanceSubMenuPanel = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(15, 5, 0, 5) // Indent the sub-menu
        };
        appearanceSubMenuPanel.Bind(Visual.IsVisibleProperty, new Binding("IsShowingAppearanceSubView"));

        var libraryListButton = new Button { Content = "Library List", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        libraryListButton.Bind(Button.CommandProperty, new Binding("ShowLibraryListSettingsCommand"));

        var gridViewButton = new Button { Content = "Grid View", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        gridViewButton.Bind(Button.CommandProperty, new Binding("ShowGridViewSettingsCommand"));

        var uiLayoutButton = new Button { Content = "UI Layout", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        uiLayoutButton.Bind(Button.CommandProperty, new Binding("ShowUILayoutSettingsCommand"));

        appearanceSubMenuPanel.Children.Add(libraryListButton);
        appearanceSubMenuPanel.Children.Add(gridViewButton);
        appearanceSubMenuPanel.Children.Add(uiLayoutButton);


        // --- Build Main Panel ---
        menuStackPanel.Children.Add(directoriesButton);
        menuStackPanel.Children.Add(themeButton);
        menuStackPanel.Children.Add(appearanceButton);
        menuStackPanel.Children.Add(appearanceSubMenuPanel); // Add the sub-panel here
        menuStackPanel.Children.Add(scrobblingButton);

        return menuStackPanel;
    }
}