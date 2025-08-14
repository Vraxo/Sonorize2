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
    private ListBox _playlistsListBox;
    private LibraryViewModel? _currentLibraryVM;
    private readonly SharedViewTemplates _sharedViewTemplates;
    private readonly MainTabViewControls _mainTabViewControls;
    private TabControl _tabControl;

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

        _tabControl = _mainTabViewControls.CreateMainTabView(out _songListBox, out _artistsListBox, out _albumsListBox, out _playlistsListBox);
        Grid.SetRow(_tabControl, 2);
        mainGrid.Children.Add(_tabControl);

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

        ApplyListViewDisplayMode((Panel)((TabItem)_tabControl.Items[0]).Content, _currentLibraryVM.LibraryViewMode, _sharedViewTemplates.SongTemplates.DetailedSongTemplate, _sharedViewTemplates.SongTemplates.CompactSongTemplate, _sharedViewTemplates.SongTemplates.GridSongTemplate);
        ApplyListViewDisplayMode((Panel)((TabItem)_tabControl.Items[1]).Content, _currentLibraryVM.ArtistViewMode, _sharedViewTemplates.ArtistTemplates.DetailedArtistTemplate, _sharedViewTemplates.ArtistTemplates.CompactArtistTemplate, _sharedViewTemplates.ArtistTemplates.GridArtistTemplate);
        ApplyListViewDisplayMode((Panel)((TabItem)_tabControl.Items[2]).Content, _currentLibraryVM.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate);
        ApplyListViewDisplayMode((Panel)((TabItem)_tabControl.Items[3]).Content, _currentLibraryVM.PlaylistViewMode, _sharedViewTemplates.DetailedPlaylistTemplate, _sharedViewTemplates.CompactPlaylistTemplate, _sharedViewTemplates.GridPlaylistTemplate);
    }

    private void LibraryViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Debug.WriteLine($"[MainWindow] LibraryViewModel_PropertyChanged fired for: {e.PropertyName}");

        if (sender is not LibraryViewModel lvm)
        {
            Debug.WriteLine("[MainWindow] Sender is not LibraryViewModel, returning.");
            return;
        }

        Action? updateAction = null;
        string? tabName = null;
        SongDisplayMode newMode = SongDisplayMode.Detailed;

        switch (e.PropertyName)
        {
            case nameof(LibraryViewModel.LibraryViewMode):
                tabName = "Library";
                newMode = lvm.LibraryViewMode;
                updateAction = () => ApplyListViewDisplayMode((Panel)((TabItem)_tabControl.Items[0]).Content, lvm.LibraryViewMode, _sharedViewTemplates.SongTemplates.DetailedSongTemplate, _sharedViewTemplates.SongTemplates.CompactSongTemplate, _sharedViewTemplates.SongTemplates.GridSongTemplate);
                break;
            case nameof(LibraryViewModel.ArtistViewMode):
                tabName = "Artist";
                newMode = lvm.ArtistViewMode;
                updateAction = () => ApplyListViewDisplayMode((Panel)((TabItem)_tabControl.Items[1]).Content, lvm.ArtistViewMode, _sharedViewTemplates.ArtistTemplates.DetailedArtistTemplate, _sharedViewTemplates.ArtistTemplates.CompactArtistTemplate, _sharedViewTemplates.ArtistTemplates.GridArtistTemplate);
                break;
            case nameof(LibraryViewModel.AlbumViewMode):
                tabName = "Album";
                newMode = lvm.AlbumViewMode;
                updateAction = () => ApplyListViewDisplayMode((Panel)((TabItem)_tabControl.Items[2]).Content, lvm.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate);
                break;
            case nameof(LibraryViewModel.PlaylistViewMode):
                tabName = "Playlist";
                newMode = lvm.PlaylistViewMode;
                updateAction = () => ApplyListViewDisplayMode((Panel)((TabItem)_tabControl.Items[3]).Content, lvm.PlaylistViewMode, _sharedViewTemplates.DetailedPlaylistTemplate, _sharedViewTemplates.CompactPlaylistTemplate, _sharedViewTemplates.GridPlaylistTemplate);
                break;
        }

        if (updateAction != null)
        {
            Debug.WriteLine($"[MainWindow] Scheduling UI update for {tabName} tab to mode {newMode}.");
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Debug.WriteLine($"[MainWindow] Executing UI update for {tabName} tab to mode {newMode} on UI thread.");
                updateAction();
            });
        }
    }

    private void ApplyListViewDisplayMode(Panel container, SongDisplayMode mode, IDataTemplate detailedTemplate, IDataTemplate compactTemplate, IDataTemplate gridTemplate)
    {
        _mainTabViewControls.UpdateListViewMode(mode, container, detailedTemplate, compactTemplate, gridTemplate);
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
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetOwnerView(null!);
        }

        base.OnClosed(e);
    }
}