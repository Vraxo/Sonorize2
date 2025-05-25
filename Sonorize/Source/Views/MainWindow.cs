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
using Sonorize.Views.MainWindowControls;

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
    // Keep a reference to the created playback panel to potentially interact later
    // private Control _mainPlaybackPanel;


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
                new(GridLength.Auto), // Menu Bar
                new(GridLength.Auto), // Search Bar
                new(GridLength.Star), // Tab Control (Library/Artists/Albums)
                new(GridLength.Auto), // Advanced Playback Panel (Waveform, Speed/Pitch, Loop)
                new(GridLength.Auto), // Main Playback Controls (Song Info, Buttons, Slider)
                new(GridLength.Auto)  // Status Bar
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

        // Use the new composite MainPlaybackControlsPanel
        var mainPlaybackPanel = MainPlaybackControlsPanel.Create(_theme);
        Grid.SetRow(mainPlaybackPanel, 4);
        mainGrid.Children.Add(mainPlaybackPanel);


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
            _currentLibraryVM = null;
        }

        if (DataContext is MainWindowViewModel vm && vm.Library != null)
        {
            _currentLibraryVM = vm.Library;
            _currentLibraryVM.PropertyChanged += LibraryViewModel_PropertyChanged;

            // Apply initial display modes for each list
            ApplyListViewDisplayMode(_songListBox, _currentLibraryVM.LibraryViewMode, _sharedViewTemplates.DetailedSongTemplate, _sharedViewTemplates.CompactSongTemplate, _sharedViewTemplates.GridSongTemplate);
            ApplyListViewDisplayMode(_artistsListBox, _currentLibraryVM.ArtistViewMode, _sharedViewTemplates.DetailedArtistTemplate, _sharedViewTemplates.CompactArtistTemplate, _sharedViewTemplates.GridArtistTemplate);
            ApplyListViewDisplayMode(_albumsListBox, _currentLibraryVM.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate);
        }
    }

    private void LibraryViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is LibraryViewModel lvm)
        {
            if (e.PropertyName == nameof(LibraryViewModel.LibraryViewMode))
            {
                Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_songListBox, lvm.LibraryViewMode, _sharedViewTemplates.DetailedSongTemplate, _sharedViewTemplates.CompactSongTemplate, _sharedViewTemplates.GridSongTemplate));
            }
            else if (e.PropertyName == nameof(LibraryViewModel.ArtistViewMode))
            {
                Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_artistsListBox, lvm.ArtistViewMode, _sharedViewTemplates.DetailedArtistTemplate, _sharedViewTemplates.CompactArtistTemplate, _sharedViewTemplates.GridArtistTemplate));
            }
            else if (e.PropertyName == nameof(LibraryViewModel.AlbumViewMode))
            {
                Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_albumsListBox, lvm.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate));
            }
        }
    }

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
        }
        base.OnClosed(e);
    }
}