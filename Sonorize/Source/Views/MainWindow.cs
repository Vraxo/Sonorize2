using System; // For EventArgs
using System.ComponentModel; // For PropertyChangedEventArgs, CancelEventArgs
using System.Diagnostics; // For Debug
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Threading; // Required for Dispatcher
using Sonorize.Models;
using Sonorize.ViewModels;
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
    private bool _isShuttingDown = false; // Add a field to prevent re-entry


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
        this.Closing += OnMainWindowClosing; // Graceful shutdown hook
    }

    private async void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isShuttingDown) return; // Already handling shutdown, let it proceed.

        if (DataContext is MainWindowViewModel vm)
        {
            // 1. Cancel the original close event.
            e.Cancel = true;

            // 2. Set flag to prevent re-entry.
            _isShuttingDown = true;
            Debug.WriteLine("[MainWindow] Closing requested. Starting graceful shutdown procedure.");

            try
            {
                // 3. Provide immediate visual feedback BEFORE the wait.
                vm.StatusBarText = "Saving final scrobble...";

                // 4. Perform final async operations (like scrobbling).
                await vm.PerformGracefulShutdownAsync();
                Debug.WriteLine("[MainWindow] Graceful shutdown async tasks completed.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainWindow] Error during graceful shutdown tasks: {ex.Message}");
            }
            finally
            {
                // 5. Dispose the ViewModel and its hierarchy.
                vm.Dispose();
                Debug.WriteLine("[MainWindow] ViewModel disposed.");

                // 6. Actually close the window.
                // Because _isShuttingDown is true, this call will not re-trigger this handler.
                Dispatcher.UIThread.Invoke(Close);
            }
        }
    }

    private void MainWindow_DataContextChanged(object? sender, EventArgs e)
    {
        if (_currentLibraryVM is not null)
        {
            _currentLibraryVM.PropertyChanged -= LibraryViewModel_PropertyChanged;
            _currentLibraryVM = null;
        }

        if (DataContext is not MainWindowViewModel vm || vm.Library == null)
        {
            return;
        }

        vm.SetOwnerView(this);

        _currentLibraryVM = vm.Library;
        _currentLibraryVM.PropertyChanged += LibraryViewModel_PropertyChanged;
        // _sharedViewTemplates.SetLibraryViewModel(_currentLibraryVM); // No longer needed

        ApplyListViewDisplayMode(_songListBox, _currentLibraryVM.LibraryViewMode, _sharedViewTemplates.SongTemplates.DetailedSongTemplate, _sharedViewTemplates.SongTemplates.CompactSongTemplate, _sharedViewTemplates.SongTemplates.GridSongTemplate);
        ApplyListViewDisplayMode(_artistsListBox, _currentLibraryVM.ArtistViewMode, _sharedViewTemplates.ArtistTemplates.DetailedArtistTemplate, _sharedViewTemplates.ArtistTemplates.CompactArtistTemplate, _sharedViewTemplates.ArtistTemplates.GridArtistTemplate);
        ApplyListViewDisplayMode(_albumsListBox, _currentLibraryVM.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate);
    }

    private void LibraryViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LibraryViewModel lvm)
        {
            return;
        }

        if (e.PropertyName == nameof(LibraryViewModel.LibraryViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_songListBox, lvm.LibraryViewMode, _sharedViewTemplates.SongTemplates.DetailedSongTemplate, _sharedViewTemplates.SongTemplates.CompactSongTemplate, _sharedViewTemplates.SongTemplates.GridSongTemplate));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.ArtistViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_artistsListBox, lvm.ArtistViewMode, _sharedViewTemplates.ArtistTemplates.DetailedArtistTemplate, _sharedViewTemplates.ArtistTemplates.CompactArtistTemplate, _sharedViewTemplates.ArtistTemplates.GridArtistTemplate));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.AlbumViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_albumsListBox, lvm.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate));
        }
    }

    private void ApplyListViewDisplayMode(ListBox listBox, SongDisplayMode mode, IDataTemplate detailedTemplate, IDataTemplate compactTemplate, IDataTemplate gridTemplate)
    {
        if (listBox is null)
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
        if (_currentLibraryVM is not null)
        {
            _currentLibraryVM.PropertyChanged -= LibraryViewModel_PropertyChanged;
            // _sharedViewTemplates.SetLibraryViewModel(null); // No longer needed
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetOwnerView(null!);
        }

        base.OnClosed(e);
    }
}