using System; // For EventArgs
using System.ComponentModel; // For PropertyChangedEventArgs, CancelEventArgs
using System.Diagnostics; // For Debug
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Threading; // Required for Dispatcher
using Sonorize.Models;
using Sonorize.ViewModels;
using Sonorize.Views.MainWindowControls;

namespace Sonorize.Views;
public class MainWindow : Window
{
    private readonly ThemeColors _theme;
    private ListBox _artistsListBox;
    private ListBox _albumsListBox;
    private ListBox _playlistsListBox;
    private MainWindowViewModel? _currentMainVM;
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

        var tabControl = _mainTabViewControls.CreateMainTabView(out _artistsListBox, out _albumsListBox, out _playlistsListBox);
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

        // Explicitly set the DataContext for the TabControl to ensure bindings inside resolve correctly.
        tabControl.DataContext = this.DataContext;
        this.DataContextChanged += (s, e) =>
        {
            tabControl.DataContext = this.DataContext;
            MainWindow_DataContextChanged(s, e);
        };
        this.Closing += OnMainWindowClosing; // Graceful shutdown hook
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (DataContext is IDisposable disposable)
        {
            Debug.WriteLine("[MainWindow] Window is closing. Disposing ViewModel to ensure graceful shutdown.");
            disposable.Dispose();
        }
    }

    private void MainWindow_DataContextChanged(object? sender, EventArgs e)
    {
        if (_currentMainVM?.LibraryDisplayModeService is not null)
        {
            _currentMainVM.LibraryDisplayModeService.PropertyChanged -= DisplayModeService_PropertyChanged;
        }

        if (DataContext is not MainWindowViewModel vm)
        {
            _currentMainVM = null;
            return;
        }

        vm.SetOwnerView(this);
        _currentMainVM = vm;
        _currentMainVM.LibraryDisplayModeService.PropertyChanged += DisplayModeService_PropertyChanged;

        // Apply initial view modes for non-library tabs
        ApplyListViewDisplayMode(_artistsListBox, _currentMainVM.LibraryDisplayModeService.ArtistViewMode, _sharedViewTemplates.DetailedArtistTemplate, _sharedViewTemplates.CompactArtistTemplate, _sharedViewTemplates.GridArtistTemplate);
        ApplyListViewDisplayMode(_albumsListBox, _currentMainVM.LibraryDisplayModeService.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate);
        ApplyListViewDisplayMode(_playlistsListBox, _currentMainVM.LibraryDisplayModeService.PlaylistViewMode, _sharedViewTemplates.DetailedPlaylistTemplate, _sharedViewTemplates.CompactPlaylistTemplate, _sharedViewTemplates.GridPlaylistTemplate);
    }

    private void DisplayModeService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_currentMainVM is null)
        {
            return;
        }

        if (e.PropertyName == nameof(ViewModels.LibraryManagement.LibraryDisplayModeService.ArtistViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_artistsListBox, _currentMainVM.LibraryDisplayModeService.ArtistViewMode, _sharedViewTemplates.DetailedArtistTemplate, _sharedViewTemplates.CompactArtistTemplate, _sharedViewTemplates.GridArtistTemplate));
        }
        else if (e.PropertyName == nameof(ViewModels.LibraryManagement.LibraryDisplayModeService.AlbumViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_albumsListBox, _currentMainVM.LibraryDisplayModeService.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate));
        }
        else if (e.PropertyName == nameof(ViewModels.LibraryManagement.LibraryDisplayModeService.PlaylistViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_playlistsListBox, _currentMainVM.LibraryDisplayModeService.PlaylistViewMode, _sharedViewTemplates.DetailedPlaylistTemplate, _sharedViewTemplates.CompactPlaylistTemplate, _sharedViewTemplates.GridPlaylistTemplate));
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
        if (_currentMainVM?.LibraryDisplayModeService is not null)
        {
            _currentMainVM.LibraryDisplayModeService.PropertyChanged -= DisplayModeService_PropertyChanged;
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetOwnerView(null!);
        }

        base.OnClosed(e);
    }
}