using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models; // For ThemeColors
using Sonorize.ViewModels; // For SettingsViewSection enum
using Sonorize.Views; // For BrushExtensions

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
        directoriesButton[!Button.BackgroundProperty] = new Binding("CurrentSettingsViewSection")
        {
            Converter = new FuncValueConverter<SettingsViewSection, IBrush>(section => section == SettingsViewSection.Directories ? theme.B_AccentColor.Multiply(0.8) : theme.B_ControlBackgroundColor)
        };
        directoriesButton[!Button.ForegroundProperty] = new Binding("CurrentSettingsViewSection")
        {
            Converter = new FuncValueConverter<SettingsViewSection, IBrush>(section => section == SettingsViewSection.Directories ? theme.B_AccentForeground : theme.B_TextColor)
        };

        var themeButton = new Button { Content = "Theme", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        themeButton.Bind(Button.CommandProperty, new Binding("ShowThemeSettingsCommand"));
        themeButton[!Button.BackgroundProperty] = new Binding("CurrentSettingsViewSection")
        {
            Converter = new FuncValueConverter<SettingsViewSection, IBrush>(section => section == SettingsViewSection.Theme ? theme.B_AccentColor.Multiply(0.8) : theme.B_ControlBackgroundColor)
        };
        themeButton[!Button.ForegroundProperty] = new Binding("CurrentSettingsViewSection")
        {
            Converter = new FuncValueConverter<SettingsViewSection, IBrush>(section => section == SettingsViewSection.Theme ? theme.B_AccentForeground : theme.B_TextColor)
        };

        var appearanceButton = new Button { Content = "Appearance", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        appearanceButton.Bind(Button.CommandProperty, new Binding("ShowAppearanceSettingsCommand"));
        appearanceButton[!Button.BackgroundProperty] = new Binding("CurrentSettingsViewSection")
        {
            Converter = new FuncValueConverter<SettingsViewSection, IBrush>(section => section == SettingsViewSection.Appearance ? theme.B_AccentColor.Multiply(0.8) : theme.B_ControlBackgroundColor)
        };
        appearanceButton[!Button.ForegroundProperty] = new Binding("CurrentSettingsViewSection")
        {
            Converter = new FuncValueConverter<SettingsViewSection, IBrush>(section => section == SettingsViewSection.Appearance ? theme.B_AccentForeground : theme.B_TextColor)
        };

        var scrobblingButton = new Button { Content = "Scrobbling", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        scrobblingButton.Bind(Button.CommandProperty, new Binding("ShowScrobblingSettingsCommand"));
        scrobblingButton[!Button.BackgroundProperty] = new Binding("CurrentSettingsViewSection")
        {
            Converter = new FuncValueConverter<SettingsViewSection, IBrush>(section => section == SettingsViewSection.Scrobbling ? theme.B_AccentColor.Multiply(0.8) : theme.B_ControlBackgroundColor)
        };
        scrobblingButton[!Button.ForegroundProperty] = new Binding("CurrentSettingsViewSection")
        {
            Converter = new FuncValueConverter<SettingsViewSection, IBrush>(section => section == SettingsViewSection.Scrobbling ? theme.B_AccentForeground : theme.B_TextColor)
        };

        // --- Appearance Sub-Menu ---
        var appearanceSubMenuPanel = new StackPanel
        {
            Spacing = 5,
            Margin = new Thickness(15, 5, 0, 5) // Indent the sub-menu
        };
        appearanceSubMenuPanel.Bind(Visual.IsVisibleProperty, new Binding("IsShowingAppearanceSubView"));

        var libraryListButton = new Button { Content = "Library List", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        libraryListButton.Bind(Button.CommandProperty, new Binding("ShowLibraryListSettingsCommand"));
        libraryListButton[!Button.BackgroundProperty] = new Binding("CurrentAppearanceSettingsViewSection")
        {
            Converter = new FuncValueConverter<AppearanceSettingsViewSection, IBrush>(section => section == AppearanceSettingsViewSection.LibraryList ? theme.B_AccentColor.Multiply(0.8) : theme.B_ControlBackgroundColor)
        };
        libraryListButton[!Button.ForegroundProperty] = new Binding("CurrentAppearanceSettingsViewSection")
        {
            Converter = new FuncValueConverter<AppearanceSettingsViewSection, IBrush>(section => section == AppearanceSettingsViewSection.LibraryList ? theme.B_AccentForeground : theme.B_TextColor)
        };

        var gridViewButton = new Button { Content = "Grid View", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        gridViewButton.Bind(Button.CommandProperty, new Binding("ShowGridViewSettingsCommand"));
        gridViewButton[!Button.BackgroundProperty] = new Binding("CurrentAppearanceSettingsViewSection")
        {
            Converter = new FuncValueConverter<AppearanceSettingsViewSection, IBrush>(section => section == AppearanceSettingsViewSection.GridView ? theme.B_AccentColor.Multiply(0.8) : theme.B_ControlBackgroundColor)
        };
        gridViewButton[!Button.ForegroundProperty] = new Binding("CurrentAppearanceSettingsViewSection")
        {
            Converter = new FuncValueConverter<AppearanceSettingsViewSection, IBrush>(section => section == AppearanceSettingsViewSection.GridView ? theme.B_AccentForeground : theme.B_TextColor)
        };

        var uiLayoutButton = new Button { Content = "UI Layout", HorizontalAlignment = HorizontalAlignment.Stretch, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        uiLayoutButton.Bind(Button.CommandProperty, new Binding("ShowUILayoutSettingsCommand"));
        uiLayoutButton[!Button.BackgroundProperty] = new Binding("CurrentAppearanceSettingsViewSection")
        {
            Converter = new FuncValueConverter<AppearanceSettingsViewSection, IBrush>(section => section == AppearanceSettingsViewSection.UILayout ? theme.B_AccentColor.Multiply(0.8) : theme.B_ControlBackgroundColor)
        };
        uiLayoutButton[!Button.ForegroundProperty] = new Binding("CurrentAppearanceSettingsViewSection")
        {
            Converter = new FuncValueConverter<AppearanceSettingsViewSection, IBrush>(section => section == AppearanceSettingsViewSection.UILayout ? theme.B_AccentForeground : theme.B_TextColor)
        };

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