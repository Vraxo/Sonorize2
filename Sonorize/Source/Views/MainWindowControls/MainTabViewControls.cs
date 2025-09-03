using System;
using System.Diagnostics;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Views.MainWindowControls;

public class MainTabViewControls
{
    private readonly ThemeColors _theme;
    private readonly SharedViewTemplates _sharedViewTemplates;

    public MainTabViewControls(ThemeColors theme, SharedViewTemplates sharedViewTemplates)
    {
        _theme = theme;
        _sharedViewTemplates = sharedViewTemplates;
    }

    public TabControl CreateMainTabView(
        out ListBox artistsListBox,
        out ListBox albumsListBox,
        out ListBox playlistsListBox)
    {
        var tabControl = new TabControl
        {
            Background = _theme.B_BackgroundColor,
            Margin = new Thickness(10, 5, 10, 5),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0)
        };

        tabControl.Bind(TabControl.SelectedIndexProperty, new Binding("ActiveTabIndex", BindingMode.TwoWay));

        // TabItem styling
        var tabItemStyle = new Style(s => s.Is<TabItem>());
        tabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, _theme.B_BackgroundColor));
        tabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, _theme.B_SecondaryTextColor));
        tabItemStyle.Setters.Add(new Setter(TabItem.PaddingProperty, new Thickness(12, 7)));
        tabItemStyle.Setters.Add(new Setter(TabItem.FontSizeProperty, 13.0));
        tabItemStyle.Setters.Add(new Setter(TabItem.FontWeightProperty, FontWeight.SemiBold));
        tabItemStyle.Setters.Add(new Setter(TabItem.BorderThicknessProperty, new Thickness(0)));
        tabItemStyle.Setters.Add(new Setter(TabItem.BorderBrushProperty, Brushes.Transparent));

        var selectedTabItemStyle = new Style(s => s.Is<TabItem>().Class(":selected"));
        selectedTabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, _theme.B_BackgroundColor));
        selectedTabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, _theme.B_TextColor));

        var pointerOverTabItemStyle = new Style(s => s.Is<TabItem>().Class(":pointerover").Not(x => x.Class(":selected")));
        pointerOverTabItemStyle.Setters.Add(new Setter(TabItem.BackgroundProperty, _theme.B_SlightlyLighterBackground));
        pointerOverTabItemStyle.Setters.Add(new Setter(TabItem.ForegroundProperty, _theme.B_TextColor));

        tabControl.Styles.Add(tabItemStyle);
        tabControl.Styles.Add(selectedTabItemStyle);
        tabControl.Styles.Add(pointerOverTabItemStyle);

        // --- LIBRARY tab ---
        var libraryDataGrid = LibraryDataGridFactory.Create(_theme);
        var libraryTab = new TabItem
        {
            Header = "LIBRARY",
            Content = libraryDataGrid
        };

        // --- ARTISTS tab ---
        var artistsTabContent = ArtistsTabFactory.Create(_theme, _sharedViewTemplates, out artistsListBox);
        var artistsTab = new TabItem { Header = "ARTISTS", Content = artistsTabContent };

        // --- ALBUMS tab ---
        var albumsTabContent = AlbumsTabFactory.Create(_theme, _sharedViewTemplates, out albumsListBox);
        var albumsTab = new TabItem { Header = "ALBUMS", Content = albumsTabContent };

        // --- PLAYLISTS tab ---
        var playlistsTabContent = PlaylistsTabFactory.Create(_theme, _sharedViewTemplates, out playlistsListBox);
        var playlistsTab = new TabItem { Header = "PLAYLISTS", Content = playlistsTabContent };

        // Populate tabs via Items.Add
        tabControl.Items.Add(libraryTab);
        tabControl.Items.Add(artistsTab);
        tabControl.Items.Add(albumsTab);
        tabControl.Items.Add(playlistsTab);

        tabControl.AttachedToVisualTree += (_, __) =>
        {
            Debug.WriteLine($"[MainTabViewControls] TabControl attached. Size: {tabControl.Bounds.Width}x{tabControl.Bounds.Height}");
        };

        return tabControl;
    }

    public void UpdateListViewMode(
        SongDisplayMode mode,
        ListBox listBox,
        IDataTemplate detailedTemplate,
        IDataTemplate compactTemplate,
        IDataTemplate gridTemplate)
    {
        if (listBox == null)
        {
            Debug.WriteLine("[MainTabViewControls] UpdateListViewMode called but target ListBox is null.");
            return;
        }

        Debug.WriteLine($"[MainTabViewControls] Applying display mode: {mode} to ListBox: {listBox.Name}");

        var scrollViewer = listBox.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault()
                           ?? listBox.Parent as ScrollViewer;

        switch (mode)
        {
            case SongDisplayMode.Detailed:
                listBox.ItemTemplate = detailedTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.StackPanelItemsPanelTemplate;
                if (scrollViewer is not null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;

            case SongDisplayMode.Compact:
                // Compact mode is now handled by a separate DataGrid, so the ListBox is hidden.
                // No changes are needed for the ListBox itself in this case.
                break;

            case SongDisplayMode.Grid:
                listBox.ItemTemplate = gridTemplate;
                listBox.ItemsPanel = _sharedViewTemplates.WrapPanelItemsPanelTemplate;
                if (scrollViewer is not null) scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
                break;
        }
    }
}