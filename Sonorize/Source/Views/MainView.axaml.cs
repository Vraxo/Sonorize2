using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb, if needed
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Sonorize.Controls;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels;
using System;

namespace Sonorize.Views; // file‐scoped namespace

public partial class MainWindow : Window
{
    private readonly ThemeColors? _theme;

    // Parameterless constructor for XAML previewer and design‐time
    public MainWindow()
    {
        if (Design.IsDesignMode)
        {
            // Provide a minimal “design‐time” VM so that the designer can show bindings.
            var loopDataService = new LoopDataService();
            var settingsService = new SettingsService();
            var musicLibraryService = new MusicLibraryService(loopDataService);
            var playbackService = new PlaybackService();
            var designTimeThemeColors = new ThemeColors();
            var waveformService = new WaveformService();

            var designVm = new MainWindowViewModel(
                settingsService,
                musicLibraryService,
                playbackService,
                designTimeThemeColors,
                waveformService,
                loopDataService
            );

            // Set DataContext before loading XAML so bindings resolve in the designer.
            this.DataContext = designVm;
            _theme = designVm.CurrentTheme;
        }

        InitializeComponent();

        // Event handler for waveform seek
        var waveformDisplay = this.FindControl<WaveformDisplayControl>("WaveformDisplay");
        if (waveformDisplay != null)
        {
            waveformDisplay.SeekRequested += WaveformDisplay_SeekRequested;
        }

        // Event handler for Play/Pause button click
        var mainPlayPauseButton = this.FindControl<Button>("MainPlayPauseButton");
        if (mainPlayPauseButton != null)
        {
            mainPlayPauseButton.Click += MainPlayPauseButton_Click;
        }
    }

    // Runtime constructor (called by App.cs or equivalent)
    public MainWindow(ThemeColors theme) : this()
    {
        _theme = theme;
        // ApplyThemeStyles is no longer strictly needed for many elements
        // because theme properties are bound via resources in XAML.
        // Keep it for any properties not covered by resources or for fallback.
        ApplyThemeStyles();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void ApplyThemeStyles()
    {
        // This method is less critical now that many styles use Resources,
        // but can still apply direct properties not handled by styles/resources.
        // It also ensures design mode has some styling.
        if (_theme == null) return;

        // Direct property applications (some may now be redundant with XAML resources)
        Background = _theme.B_BackgroundColor;

        // Example: Menu background (can be done in XAML with resource too)
        var appMenu = this.FindControl<Menu>("AppMenu");
        if (appMenu != null)
        {
            // If using XAML resource for Menu Background, this line is redundant.
            // Keeping for clarity or if XAML resource is removed.
            appMenu.Background = _theme.B_SlightlyLighterBackground;
        }

        // Example: SearchBox properties (can be done in XAML with resource too)
        var searchBox = this.FindControl<TextBox>("SearchBox");
        if (searchBox != null)
        {
            // If using XAML resource for SearchBox Background/Foreground/BorderBrush, these lines are redundant.
            searchBox.Background = _theme.B_SlightlyLighterBackground;
            searchBox.Foreground = _theme.B_TextColor;
            searchBox.BorderBrush = _theme.B_ControlBackgroundColor; // Style uses accent, this is default border
        }

        // Example: ListBox backgrounds (can be done in XAML with resource too)
        var songListBox = this.FindControl<ListBox>("SongListBox");
        var artistsListBox = this.FindControl<ListBox>("ArtistsListBox");
        var albumsListBox = this.FindControl<ListBox>("AlbumsListBox");
        if (songListBox != null) songListBox.Background = _theme.B_ListBoxBackground;
        if (artistsListBox != null) artistsListBox.Background = _theme.B_ListBoxBackground;
        if (albumsListBox != null) albumsListBox.Background = _theme.B_ListBoxBackground;

        // Example: Advanced Panel Border (can be done in XAML with resource too)
        var advBorder = this.FindControl<Border>("AdvancedPlaybackPanelBorder");
        if (advBorder != null)
        {
            advBorder.Background = _theme.B_SlightlyLighterBackground;
            advBorder.BorderBrush = _theme.B_AccentColor; // This was also in XAML resource now
        }

        // Example: Status Bar Border (can be done in XAML with resource too)
        var statusBarBorder = this.FindControl<Border>("StatusBarBorder");
        if (statusBarBorder != null)
            statusBarBorder.Background = _theme.B_SlightlyLighterBackground;

        // Waveform specific brushes not handled by general resources
        var waveformDisplay = this.FindControl<WaveformDisplayControl>("WaveformDisplay");
        if (waveformDisplay != null)
        {
            // These are now handled by XAML Resources:
            // waveformDisplay.Background = _theme.B_ControlBackgroundColor;
            // waveformDisplay.WaveformBrush = _theme.B_AccentColor;
            // waveformDisplay.LoopRegionBrush is handled by ThemeLoopRegionBrush resource

            // But you might still set specific brushes like the position marker
            waveformDisplay.PositionMarkerBrush = Brushes.OrangeRed; // Example of non-theme color
        }

        // You might remove setting individual control colors here
        // if they are fully covered by XAML styles/resources now.
        // Keeping for now as they don't hurt and provide design-time fallback.
    }

    private void MainPlayPauseButton_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Playing)
                vm.PlaybackService.Pause();
            else
                vm.PlaybackService.Resume();
        }
    }

    private void WaveformDisplay_SeekRequested(object? sender, TimeSpan time)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.WaveformSeekCommand.Execute(time);
        }
    }
}

// =========================================
// Value Converters (no changes required here)
// =========================================

public class BooleanToPlayPauseTextConverter : IValueConverter
{
    public static readonly BooleanToPlayPauseTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isPlaying)
            return isPlaying ? "Pause" : "Play";
        return "Play";
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

public class NotNullToBooleanConverter : IValueConverter
{
    public static readonly NotNullToBooleanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value != null;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
