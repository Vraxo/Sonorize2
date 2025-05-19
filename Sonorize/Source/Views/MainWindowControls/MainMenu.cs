using Avalonia.Controls;
using Avalonia.Data;
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.MainWindowControls;

public static class MainMenu
{
    public static Menu Create(ThemeColors theme, Window ownerWindow)
    {
        var menu = new Menu 
        { 
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor
        };

        var fileMenuItem = new MenuItem
        {
            Header = "_File",
            Foreground = theme.B_TextColor
        };

        var addDirectoryMenuItem = new MenuItem
        {
            Header = "_Add Music Directory...",
            Foreground = theme.B_TextColor
        };

        addDirectoryMenuItem.Bind(MenuItem.CommandProperty, new Binding("AddDirectoryAndRefreshCommand"));
        addDirectoryMenuItem.CommandParameter = ownerWindow; // Use the passed owner window

        var settingsMenuItem = new MenuItem
        {
            Header = "_Settings...",
            Foreground = theme.B_TextColor
        };

        settingsMenuItem.Bind(MenuItem.CommandProperty, new Binding("OpenSettingsCommand"));
        settingsMenuItem.CommandParameter = ownerWindow; // Use the passed owner window

        var exitMenuItem = new MenuItem
        {
            Header = "E_xit",
            Foreground = theme.B_TextColor
        };

        exitMenuItem.Bind(MenuItem.CommandProperty, new Binding("ExitCommand"));

        fileMenuItem.Items.Add(addDirectoryMenuItem);
        fileMenuItem.Items.Add(settingsMenuItem);
        fileMenuItem.Items.Add(new Separator());
        fileMenuItem.Items.Add(exitMenuItem);

        menu.Items.Add(fileMenuItem);
        return menu;
    }
}