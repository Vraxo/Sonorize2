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

        // Library View SubMenu
        var libraryViewSubMenu = new MenuItem { Header = "Library View", Foreground = theme.B_TextColor };
        var libDetailed = new MenuItem { Header = "Detailed", Foreground = theme.B_TextColor };
        libDetailed.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        libDetailed.CommandParameter = ("Library", SongDisplayMode.Detailed);
        var libCompact = new MenuItem { Header = "Compact", Foreground = theme.B_TextColor };
        libCompact.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        libCompact.CommandParameter = ("Library", SongDisplayMode.Compact);
        var libGrid = new MenuItem { Header = "Grid", Foreground = theme.B_TextColor };
        libGrid.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        libGrid.CommandParameter = ("Library", SongDisplayMode.Grid);
        libraryViewSubMenu.Items.Add(libDetailed);
        libraryViewSubMenu.Items.Add(libCompact);
        libraryViewSubMenu.Items.Add(libGrid);

        // Artists View SubMenu
        var artistsViewSubMenu = new MenuItem { Header = "Artists View", Foreground = theme.B_TextColor };
        var artDetailed = new MenuItem { Header = "Detailed", Foreground = theme.B_TextColor };
        artDetailed.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        artDetailed.CommandParameter = ("Artists", SongDisplayMode.Detailed);
        var artCompact = new MenuItem { Header = "Compact", Foreground = theme.B_TextColor };
        artCompact.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        artCompact.CommandParameter = ("Artists", SongDisplayMode.Compact);
        var artGrid = new MenuItem { Header = "Grid", Foreground = theme.B_TextColor };
        artGrid.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        artGrid.CommandParameter = ("Artists", SongDisplayMode.Grid);
        artistsViewSubMenu.Items.Add(artDetailed);
        artistsViewSubMenu.Items.Add(artCompact);
        artistsViewSubMenu.Items.Add(artGrid);

        // Albums View SubMenu
        var albumsViewSubMenu = new MenuItem { Header = "Albums View", Foreground = theme.B_TextColor };
        var albDetailed = new MenuItem { Header = "Detailed", Foreground = theme.B_TextColor };
        albDetailed.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        albDetailed.CommandParameter = ("Albums", SongDisplayMode.Detailed);
        var albCompact = new MenuItem { Header = "Compact", Foreground = theme.B_TextColor };
        albCompact.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        albCompact.CommandParameter = ("Albums", SongDisplayMode.Compact);
        var albGrid = new MenuItem { Header = "Grid", Foreground = theme.B_TextColor };
        albGrid.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        albGrid.CommandParameter = ("Albums", SongDisplayMode.Grid);
        albumsViewSubMenu.Items.Add(albDetailed);
        albumsViewSubMenu.Items.Add(albCompact);
        albumsViewSubMenu.Items.Add(albGrid);

        // Playlists View SubMenu
        var playlistsViewSubMenu = new MenuItem { Header = "Playlists View", Foreground = theme.B_TextColor };
        var playDetailed = new MenuItem { Header = "Detailed", Foreground = theme.B_TextColor };
        playDetailed.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        playDetailed.CommandParameter = ("Playlists", SongDisplayMode.Detailed);
        var playCompact = new MenuItem { Header = "Compact", Foreground = theme.B_TextColor };
        playCompact.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        playCompact.CommandParameter = ("Playlists", SongDisplayMode.Compact);
        var playGrid = new MenuItem { Header = "Grid", Foreground = theme.B_TextColor };
        playGrid.Bind(MenuItem.CommandProperty, new Binding("Library.SetDisplayModeCommand"));
        playGrid.CommandParameter = ("Playlists", SongDisplayMode.Grid);
        playlistsViewSubMenu.Items.Add(playDetailed);
        playlistsViewSubMenu.Items.Add(playCompact);
        playlistsViewSubMenu.Items.Add(playGrid);

        viewMenuItem.Items.Add(libraryViewSubMenu);
        viewMenuItem.Items.Add(artistsViewSubMenu);
        viewMenuItem.Items.Add(albumsViewSubMenu);
        viewMenuItem.Items.Add(playlistsViewSubMenu);

        menu.Items.Add(fileMenuItem);
        menu.Items.Add(viewMenuItem);
        return menu;
    }
}