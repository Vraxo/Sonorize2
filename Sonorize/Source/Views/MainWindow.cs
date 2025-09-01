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
    private DataGrid _songDataGrid;
    private ScrollViewer _songListScrollViewer; // Changed from ListBox
    private ListBox _artistsListBox;
    private ListBox _albumsListBox;
    private ListBox _playlistsListBox;
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

        var tabControl = _mainTabViewControls.CreateMainTabView(out _songDataGrid, out _songListScrollViewer, out _artistsListBox, out _albumsListBox, out _playlistsListBox);
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

        // --- DEFERRED BINDING FOR DATAGRID ---
        // The DataContext is now available, so we can safely apply the bindings.
        _songDataGrid.Bind(ItemsControl.ItemsSourceProperty, new Binding("Library.FilteredSongs"));
        _songDataGrid.Bind(DataGrid.SelectedItemProperty, new Binding("Library.SelectedSong", BindingMode.TwoWay));

        // Bind column visibility. Columns DO NOT inherit DataContext, so we must provide an explicit Source.
        _songDataGrid.Columns[0].Bind(DataGridColumn.IsVisibleProperty, new Binding("LibraryViewMode") { Source = vm.Library, Converter = new FuncValueConverter<SongDisplayMode, bool>(m => m == SongDisplayMode.Detailed) });
        _songDataGrid.Columns[2].Bind(DataGridColumn.IsVisibleProperty, new Binding("ShowArtist") { Source = vm.Library.ViewOptions });
        _songDataGrid.Columns[3].Bind(DataGridColumn.IsVisibleProperty, new Binding("ShowAlbum") { Source = vm.Library.ViewOptions });
        _songDataGrid.Columns[4].Bind(DataGridColumn.IsVisibleProperty, new Binding("ShowPlayCount") { Source = vm.Library.ViewOptions });
        _songDataGrid.Columns[5].Bind(DataGridColumn.IsVisibleProperty, new Binding("ShowDateAdded") { Source = vm.Library.ViewOptions });
        _songDataGrid.Columns[6].Bind(DataGridColumn.IsVisibleProperty, new Binding("ShowDuration") { Source = vm.Library.ViewOptions });
        // --- END DEFERRED BINDING ---

        _currentLibraryVM = vm.Library;
        _currentLibraryVM.PropertyChanged += LibraryViewModel_PropertyChanged;

        ApplyLibraryViewMode(_currentLibraryVM.LibraryViewMode);
        ApplyListViewDisplayMode(_artistsListBox, _currentLibraryVM.ArtistViewMode, _sharedViewTemplates.ArtistTemplates.DetailedArtistTemplate, _sharedViewTemplates.ArtistTemplates.CompactArtistTemplate, _sharedViewTemplates.ArtistTemplates.GridArtistTemplate);
        ApplyListViewDisplayMode(_albumsListBox, _currentLibraryVM.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate);
        ApplyListViewDisplayMode(_playlistsListBox, _currentLibraryVM.PlaylistViewMode, _sharedViewTemplates.DetailedPlaylistTemplate, _sharedViewTemplates.CompactPlaylistTemplate, _sharedViewTemplates.GridPlaylistTemplate);
    }

    private void LibraryViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LibraryViewModel lvm)
        {
            return;
        }

        if (e.PropertyName == nameof(LibraryViewModel.LibraryViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyLibraryViewMode(lvm.LibraryViewMode));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.ArtistViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_artistsListBox, lvm.ArtistViewMode, _sharedViewTemplates.ArtistTemplates.DetailedArtistTemplate, _sharedViewTemplates.ArtistTemplates.CompactArtistTemplate, _sharedViewTemplates.ArtistTemplates.GridArtistTemplate));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.AlbumViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_albumsListBox, lvm.AlbumViewMode, _sharedViewTemplates.DetailedAlbumTemplate, _sharedViewTemplates.CompactAlbumTemplate, _sharedViewTemplates.GridAlbumTemplate));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.PlaylistViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplyListViewDisplayMode(_playlistsListBox, lvm.PlaylistViewMode, _sharedViewTemplates.DetailedPlaylistTemplate, _sharedViewTemplates.CompactPlaylistTemplate, _sharedViewTemplates.GridPlaylistTemplate));
        }
    }

    private void ApplyLibraryViewMode(SongDisplayMode mode)
    {
        // This logic is now handled by the DataTemplateSelector on the TabItem's ContentTemplate
        // We just need to ensure the DataGrid's row height is updated when the view mode changes
        // (though it doesn't strictly depend on the mode, it's good practice to re-apply related settings)

        // This is now redundant as we're switching to a binding.
        // if (DataContext is MainWindowViewModel mvm)
        // {
        //     _songDataGrid.RowHeight = mvm.Library.ViewOptions.RowHeight;
        // }
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
        }

        if (DataContext is MainWindowViewModel vm)
        {
            vm.SetOwnerView(null!);
        }

        base.OnClosed(e);
    }
}