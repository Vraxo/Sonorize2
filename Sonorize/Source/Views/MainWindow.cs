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

        var tabControl = _mainTabViewControls.CreateMainTabView();
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

        _currentLibraryVM = vm.Library;
        _currentLibraryVM.PropertyChanged += LibraryViewModel_PropertyChanged;

        // Set initial view modes
        _mainTabViewControls.UpdateListViewMode("Library", _currentLibraryVM.LibraryViewMode);
        _mainTabViewControls.UpdateListViewMode("Artists", _currentLibraryVM.ArtistViewMode);
        _mainTabViewControls.UpdateListViewMode("Albums", _currentLibraryVM.AlbumViewMode);
        _mainTabViewControls.UpdateListViewMode("Playlists", _currentLibraryVM.PlaylistViewMode);
    }

    private void LibraryViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LibraryViewModel lvm)
        {
            return;
        }

        // Delegate view mode changes to the control manager
        if (e.PropertyName == nameof(LibraryViewModel.LibraryViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => _mainTabViewControls.UpdateListViewMode("Library", lvm.LibraryViewMode));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.ArtistViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => _mainTabViewControls.UpdateListViewMode("Artists", lvm.ArtistViewMode));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.AlbumViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => _mainTabViewControls.UpdateListViewMode("Albums", lvm.AlbumViewMode));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.PlaylistViewMode))
        {
            Dispatcher.UIThread.InvokeAsync(() => _mainTabViewControls.UpdateListViewMode("Playlists", lvm.PlaylistViewMode));
        }
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