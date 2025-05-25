using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb, ToggleButton
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
// Removed: using Avalonia.Media.Imaging;
// Removed: using Avalonia.Styling;
// Removed: using Sonorize.Controls;
// Removed: using Sonorize.Converters;
using Sonorize.Models;
// Removed: using Sonorize.Services;
using Sonorize.ViewModels;
using Sonorize.Views.MainWindowControls;
using System; // For EventArgs
using System.ComponentModel; // For PropertyChangedEventArgs
using System.Diagnostics; // For Debug
using Avalonia.Threading; // Required for Dispatcher

namespace Sonorize.Views;
public class MainWindow : Window
{
    private readonly ThemeColors _theme;
    private ListBox _songListBox;
    private ListBox _artistsListBox;
    private ListBox _albumsListBox;
    private LibraryViewModel? _currentLibraryVM;
    private readonly SharedViewTemplates _sharedViewTemplates;
    private readonly MainTabViewControls _mainTabViewControls;


    public MainWindow(ThemeColors theme)
    {
        _theme = theme;
        _sharedViewTemplates = new SharedViewTemplates(_theme);
        _mainTabViewControls = new MainTabViewControls(_theme, _sharedViewTemplates);

        Title = "Sonorize";
        Width = 950;
        Height = 750;
        MinWidth = 700;
        MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = _theme.B_BackgroundColor;

        var mainGrid = new Grid
        {
            RowDefinitions =
            [
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Star),
                new(GridLength.Auto),
                new(GridLength.Auto),
                new(GridLength.Auto)
            ]
        };

        var menu = MainMenu.Create(_theme, this);
        Grid.SetRow(menu, 0);
        mainGrid.Children.Add(menu);

        var searchBarPanel = SearchBarPanel.Create(_theme);
        Grid.SetRow(searchBarPanel, 1);
        mainGrid.Children.Add(searchBarPanel);

        var tabControl = _mainTabViewControls.CreateMainTabView(out _songListBox, out _artistsListBox, out _albumsListBox);
        Grid.SetRow(tabControl, 2);
        mainGrid.Children.Add(tabControl);

        var advancedPlaybackPanel = AdvancedPlaybackPanelControls.Create(_theme);
        advancedPlaybackPanel.Bind(Visual.IsVisibleProperty, new Binding("IsAdvancedPanelVisible"));
        Grid.SetRow(advancedPlaybackPanel, 3);
        mainGrid.Children.Add(advancedPlaybackPanel);

        var mainPlaybackControls = MainPlaybackControlsPanel.Create(_theme);
        Grid.SetRow(mainPlaybackControls, 4);
        mainGrid.Children.Add(mainPlaybackControls);

        var statusBar = CreateStatusBar();
        Grid.SetRow(statusBar, 5);
        mainGrid.Children.Add(statusBar);

        Content = mainGrid;

        this.DataContextChanged += MainWindow_DataContextChanged;
    }

    private void MainWindow_DataContextChanged(object? sender, EventArgs e)
    {
        if (_currentLibraryVM != null)
        {
            _currentLibraryVM.PropertyChanged -= LibraryViewModel_PropertyChanged;
            // Removed: _currentLibraryVM.RequestViewModeRefresh -= LibraryViewModel_RequestViewModeRefresh; // Unsubscribe old handler
            _currentLibraryVM = null;
        }

        if (DataContext is MainWindowViewModel vm && vm.Library != null)
        {
            _currentLibraryVM = vm.Library;
            _currentLibraryVM.PropertyChanged += LibraryViewModel_PropertyChanged;
            // Removed: _currentLibraryVM.RequestViewModeRefresh += LibraryViewModel_RequestViewModeRefresh; // Subscribe new handler

            // Apply initial display modes for each list immediately when DataContext is set
            // This ensures the correct template is assigned even before loading starts.
            // The UI should update automatically as data arrives and thumbnails load via bindings.
            ApplyListViewDisplayMode(_songListBox, _currentLibraryVM.LibraryViewMode, _sharedViewTemplates.DetailedSongTemplate, _sharedViewTemplates.CompactSongTemplate, _sharedViewTemplates.GridSongTemplate);
            ApplyListViewDisplayMode(_artistsListBox, _currentLibraryVM.ArtistViewMode, _sharedViewTemplates.DetailedArtistTemplate, _sharedViewTemplates.CompactArtistTemplate, _sharedViewTemplates.GridArtistTemplate);
            ApplyListViewDisplayMode(_albumsListBox, _currentLibraryVM.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate);
        }
    }

    private void LibraryViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // This handler is primarily for changes initiated *by* the user selecting a view mode,
        // which triggers the setter in the ViewModel, which then saves settings and raises RequestViewModeRefresh.
        // The RequestViewModeRefresh handler below will actually update the list box template/panel.
        // Keeping this method allows for future specific handling of other property changes if needed.
        // Debug.WriteLine($"[MainWindow] LibraryViewModel_PropertyChanged: {e.PropertyName}");

        // With RequestViewModeRefresh removed, this handler might become less crucial unless we
        // need to react to other specific LibraryViewModel property changes here.
        // The view mode properties raising PropertyChanged in the ViewModel are enough for bindings,
        // and the template is applied in DataContextChanged.
    }

    // Removed: New handler for the RequestViewModeRefresh event
    // Removed: private void LibraryViewModel_RequestViewModeRefresh(object? sender, EventArgs e)
    // Removed: {
    // Removed:     // This handler runs on the UI thread because the event is raised on the UI thread.
    // Removed:     if (sender is LibraryViewModel lvm)
    // Removed:     {
    // Removed:         Debug.WriteLine($"[MainWindow] Received RequestViewModeRefresh event from LibraryVM. Re-applying current view modes.");
    // Removed:         // Re-apply the current view mode for the ListBox whose mode might have changed
    // Removed:         // or for the ListBox that needs a refresh (e.g., Library tab after initial load).
    // Removed:         ApplyListViewDisplayMode(_songListBox, lvm.LibraryViewMode, _sharedViewTemplates.DetailedSongTemplate, _sharedViewTemplates.CompactSongTemplate, _sharedViewTemplates.GridSongTemplate);
    // Removed:         ApplyListViewDisplayMode(_artistsListBox, lvm.ArtistViewMode, _sharedViewTemplates.DetailedArtistTemplate, _sharedViewTemplates.CompactArtistTemplate, _sharedViewTemplates.GridArtistTemplate);
    // Removed:         ApplyListViewDisplayMode(_albumsListBox, lvm.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate);
    // Removed:     }
    // Removed: }


    // Renamed for clarity
    private void ApplyListViewDisplayMode(ListBox listBox, SongDisplayMode mode, IDataTemplate detailedTemplate, IDataTemplate compactTemplate, IDataTemplate gridTemplate)
    {
        if (listBox == null)
        {
            Debug.WriteLine($"[MainWindow] ApplyListViewDisplayMode called but ListBox target is null. Mode: {mode}");
            return;
        }
        _mainTabViewControls.UpdateListViewMode(mode, listBox, detailedTemplate, compactTemplate, gridTemplate);
    }

    private Border CreateStatusBar()
    {
        var statusBar = new Border { Background = _theme.B_SlightlyLighterBackground, Padding = new Thickness(10, 4), Height = 26 };
        var statusBarText = new TextBlock { Foreground = _theme.B_SecondaryTextColor, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 };
        statusBarText.Bind(TextBlock.TextProperty, new Binding("StatusBarText"));
        statusBar.Child = statusBarText;
        return statusBar;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_currentLibraryVM != null)
        {
            _currentLibraryVM.PropertyChanged -= LibraryViewModel_PropertyChanged;
            // Removed: _currentLibraryVM.RequestViewModeRefresh -= LibraryViewModel_RequestViewModeRefresh; // Unsubscribe
        }
        base.OnClosed(e);
    }
}