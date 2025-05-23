using Avalonia.Controls;
using Avalonia.Data;
using Sonorize.Models; // For ThemeColors
using Sonorize.ViewModels; // For SongDisplayMode enum

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

        // File Menu
        var fileMenuItem = new MenuItem { Header = "_File", Foreground = theme.B_TextColor };
        var addDirectoryMenuItem = new MenuItem { Header = "_Add Music Directory...", Foreground = theme.B_TextColor };
        addDirectoryMenuItem.Bind(MenuItem.CommandProperty, new Binding("AddDirectoryAndRefreshCommand"));
        addDirectoryMenuItem.CommandParameter = ownerWindow;

        var settingsMenuItem = new MenuItem { Header = "_Settings...", Foreground = theme.B_TextColor };
        settingsMenuItem.Bind(MenuItem.CommandProperty, new Binding("OpenSettingsCommand"));
        settingsMenuItem.CommandParameter = ownerWindow;

        var exitMenuItem = new MenuItem { Header = "E_xit", Foreground = theme.B_TextColor };
        exitMenuItem.Bind(MenuItem.CommandProperty, new Binding("ExitCommand"));

        fileMenuItem.Items.Add(addDirectoryMenuItem);
        fileMenuItem.Items.Add(settingsMenuItem);
        fileMenuItem.Items.Add(new Separator());
        fileMenuItem.Items.Add(exitMenuItem);

        // View Menu
        var viewMenuItem = new MenuItem { Header = "_View", Foreground = theme.B_TextColor };
        var displayModeDetailedItem = new MenuItem { Header = "Detailed Song List", Foreground = theme.B_TextColor };
        displayModeDetailedItem.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        displayModeDetailedItem.CommandParameter = SongDisplayMode.Detailed;

        var displayModeCompactItem = new MenuItem { Header = "Compact Song List", Foreground = theme.B_TextColor };
        displayModeCompactItem.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        displayModeCompactItem.CommandParameter = SongDisplayMode.Compact;

        var displayModeGridItem = new MenuItem { Header = "Grid Song View", Foreground = theme.B_TextColor };
        displayModeGridItem.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        displayModeGridItem.CommandParameter = SongDisplayMode.Grid;

        viewMenuItem.Items.Add(displayModeDetailedItem);
        viewMenuItem.Items.Add(displayModeCompactItem);
        viewMenuItem.Items.Add(displayModeGridItem);

        menu.Items.Add(fileMenuItem);
        menu.Items.Add(viewMenuItem);
        return menu;
    }
}