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
    private ListBox _songListBox; // Field to hold the reference to the song ListBox, populated by MainTabViewControls
    private LibraryViewModel? _currentLibraryVM;
    private readonly SongListTemplates _songListTemplates;
    private readonly MainTabViewControls _mainTabViewControls;


    public MainWindow(ThemeColors theme)
    {
        _theme = theme;
        _songListTemplates = new SongListTemplates(_theme);
        _mainTabViewControls = new MainTabViewControls(_theme, _songListTemplates);

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

        // Use the new MainTabViewControls class
        var tabControl = _mainTabViewControls.CreateMainTabView(out _songListBox);
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
            _currentLibraryVM = null;
        }

        if (DataContext is MainWindowViewModel vm && vm.Library != null)
        {
            _currentLibraryVM = vm.Library;
            _currentLibraryVM.PropertyChanged += LibraryViewModel_PropertyChanged;
            // Apply initial display mode based on ViewModel's current setting
            ApplySongDisplayMode(_currentLibraryVM.CurrentSongDisplayMode);
        }
    }

    private void LibraryViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.CurrentSongDisplayMode) && sender is LibraryViewModel lvm)
        {
            Dispatcher.UIThread.InvokeAsync(() => ApplySongDisplayMode(lvm.CurrentSongDisplayMode));
        }
    }

    private void ApplySongDisplayMode(SongDisplayMode mode)
    {
        if (_songListBox == null)
        {
            Debug.WriteLine("[MainWindow] ApplySongDisplayMode called but _songListBox is null. This might happen if DataContext changes before UI is fully ready.");
            return;
        }
        _mainTabViewControls.UpdateSongListDisplayMode(mode, _songListBox);
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