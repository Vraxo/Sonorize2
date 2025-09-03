using System;
using System.Collections.Generic; // Added for List<string>
using System.ComponentModel;
using System.Diagnostics;
using System.IO; // Required for Path.GetFullPath, Directory.Exists
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.LibraryManagement; // Required for LibraryDisplayModeService
using Sonorize.ViewModels.MainWindow; // Added for MainWindowInteractionCoordinator

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly MainWindowComponentsManager _componentsManager;
    private Window? _ownerView;

    // Expose Services needed by views or bindings if not through child VMs
    public PlaybackService PlaybackService => _componentsManager.PlaybackServiceProperty; // Exposed from ComponentsManager
    public ThemeColors CurrentTheme { get; }
    public LibraryDisplayModeService LibraryDisplayModeService { get; }

    // Expose the child ViewModels from ComponentsManager
    public LibraryViewModel Library => _componentsManager.Library;
    public LoopEditorViewModel LoopEditor => _componentsManager.LoopEditor;
    public PlaybackViewModel Playback => _componentsManager.Playback;
    public AdvancedPanelViewModel AdvancedPanel => _componentsManager.AdvancedPanel;

    public string StatusBarText { get => field; set => SetProperty(ref field, value); } = "Welcome to Sonorize!";

    // --- Background Properties ---
    public IBrush PlaybackAreaBackground { get; private set; }
    public Bitmap? AlbumArtForStretchBackground { get; private set; }
    public Bitmap? AlbumArtForAbstractBackground { get; private set; }
    public bool ShowAlbumArtStretchBackground { get; private set; }
    public bool ShowAlbumArtAbstractBackground { get; private set; }
    public bool UseCompactPlaybackControls { get; private set; }
    private bool _isStatusBarVisible;
    public bool IsStatusBarVisible { get => _isStatusBarVisible; private set => SetProperty(ref _isStatusBarVisible, value); }


    private int _activeTabIndex;
    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        set
        {
            if (SetProperty(ref _activeTabIndex, value))
            {
                // Library tab is index 0. Clear filters when switching to it.
                if (_activeTabIndex == 0)
                {
                    Library.FilterState.SelectedArtist = null;
                    Library.FilterState.SelectedAlbum = null;
                    Library.FilterState.SelectedPlaylist = null;
                }

                // Playlist tab is index 3
                if (_activeTabIndex == 3)
                {
                    Library.RefreshAutoPlaylists();
                }
            }
        }
    }

    public bool IsLoadingLibrary => Library.IsLoadingLibrary;

    public bool IsAdvancedPanelVisible
    {
        get => AdvancedPanel.IsVisible;
        set
        {
            if (AdvancedPanel.IsVisible != value)
            {
                AdvancedPanel.IsVisible = value;
                OnPropertyChanged(); // AdvancedPanel will also raise its own, this is for MainWindowViewModel bindings
            }
        }
    }
    public ICommand ToggleAdvancedPanelCommand => AdvancedPanel.ToggleVisibilityCommand;

    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }
    public ICommand OpenEditSongMetadataDialogCommand { get; }

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService,
        LoopDataService loopDataService,
        ScrobblingService scrobblingService,
        SongMetadataService songMetadataService,
        SongEditInteractionService songEditInteractionService,
        SongLoopService songLoopService)
    {
        CurrentTheme = theme; // Store theme directly
        PlaybackAreaBackground = CurrentTheme.B_BackgroundColor;

        LibraryDisplayModeService = new LibraryDisplayModeService(settingsService);

        _componentsManager = new MainWindowComponentsManager(
            this, // Pass self as parent
            settingsService,
            musicLibraryService,
            playbackService, // Pass the service instance
            theme,
            waveformService,
            loopDataService,
            scrobblingService,
            songMetadataService,
            songEditInteractionService,
            songLoopService,
            () => _ownerView,
            RaiseAllCommandsCanExecuteChanged,
            UpdateStatusBarText,
            (propertyName) => OnPropertyChanged(propertyName)
        );

        // Commands that interact with components managed by _componentsManager
        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(),
            _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));
        OpenSettingsCommand = new RelayCommand(async _ => await OpenSettingsDialogAsync(),
            _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));

        // Correctly handle exit by closing the window, which triggers the disposal chain.
        ExitCommand = new RelayCommand(_ => _ownerView?.Close(), _ => _ownerView is not null);

        AddDirectoryAndRefreshCommand = new RelayCommand(async _ => await AddMusicDirectoryAndRefreshAsync(),
            _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));
        OpenEditSongMetadataDialogCommand = new RelayCommand(async song => await HandleOpenEditSongMetadataDialogAsync(song), CanOpenEditSongMetadataDialog);

        Dispatcher.UIThread.InvokeAsync(UpdateAllUIDependentStates);
        UpdatePlaybackAreaBackground();

        // Load the library
        if (LoadInitialDataCommand.CanExecute(null))
        {
            LoadInitialDataCommand.Execute(null);
        }
    }

    public void SetOwnerView(Window ownerView)
    {
        _ownerView = ownerView;
        // Re-evaluate CanExecute for commands that depend on the owner view.
        (ExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(IsLoadingLibrary));
        OnPropertyChanged(nameof(IsAdvancedPanelVisible));
        OnPropertyChanged(nameof(ActiveTabIndex));

        UpdateStatusBarText();
        RaiseAllCommandsCanExecuteChanged();
    }

    public void RaiseAllCommandsCanExecuteChanged()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenEditSongMetadataDialogCommand as RelayCommand)?.RaiseCanExecuteChanged();

            Library.RaiseLibraryCommandsCanExecuteChanged();
            Playback.RaisePlaybackCommandCanExecuteChanged();
            LoopEditor.RaiseMainLoopCommandsCanExecuteChanged();
        });
    }

    private void UpdateStatusBarText()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusBarText = _componentsManager.WorkflowManager.GetCurrentStatusText(LoopEditor);
        });
    }

    public void UpdatePlaybackAreaBackground()
    {
        var settings = _componentsManager.SettingsServiceProperty.LoadSettings();

        var newCompactSetting = settings.Appearance.UseCompactPlaybackControls;
        if (UseCompactPlaybackControls != newCompactSetting)
        {
            UseCompactPlaybackControls = newCompactSetting;
            OnPropertyChanged(nameof(UseCompactPlaybackControls));
        }

        IsStatusBarVisible = settings.Appearance.ShowStatusBar;

        var style = Enum.TryParse<PlaybackAreaBackgroundStyle>(settings.Appearance.PlaybackAreaBackgroundStyle, out var s) ? s : PlaybackAreaBackgroundStyle.Solid;

        AlbumArtForAbstractBackground?.Dispose();

        ShowAlbumArtStretchBackground = false;
        ShowAlbumArtAbstractBackground = false;
        AlbumArtForStretchBackground = null;
        AlbumArtForAbstractBackground = null;
        PlaybackAreaBackground = CurrentTheme.B_BackgroundColor;

        Bitmap? defaultThumb = _componentsManager.MusicLibraryServiceProperty.GetDefaultThumbnail();
        Bitmap? currentArt = Playback.CurrentSong?.Thumbnail;

        if (currentArt != null && currentArt != defaultThumb)
        {
            PlaybackAreaBackground = Brushes.Transparent; // Make panel transparent to see image behind it

            if (style == PlaybackAreaBackgroundStyle.AlbumArtStretch)
            {
                AlbumArtForStretchBackground = currentArt;
                ShowAlbumArtStretchBackground = true;
            }
            else if (style == PlaybackAreaBackgroundStyle.AlbumArtAbstract)
            {
                var abstractBitmap = currentArt.CreateScaledBitmap(new PixelSize(8, 8), BitmapInterpolationMode.HighQuality);
                AlbumArtForAbstractBackground = abstractBitmap;
                ShowAlbumArtAbstractBackground = true;
            }
        }

        OnPropertyChanged(nameof(PlaybackAreaBackground));
        OnPropertyChanged(nameof(AlbumArtForStretchBackground));
        OnPropertyChanged(nameof(AlbumArtForAbstractBackground));
        OnPropertyChanged(nameof(ShowAlbumArtStretchBackground));
        OnPropertyChanged(nameof(ShowAlbumArtAbstractBackground));
    }


    private async Task OpenSettingsDialogAsync()
    {
        var (statusMessages, settingsChanged) = await _componentsManager.InteractionCoordinator.CoordinateAndProcessSettingsAsync();
        if (settingsChanged)
        {
            LibraryDisplayModeService.ReloadDisplayPreferences();
            UpdatePlaybackAreaBackground();
            if (statusMessages.Any())
            {
                StatusBarText = string.Join(" | ", statusMessages);
            }
            else
            {
                UpdateStatusBarText();
            }
        }
    }

    private async Task AddMusicDirectoryAndRefreshAsync()
    {
        var (refreshNeeded, statusMessage) = await _componentsManager.InteractionCoordinator.CoordinateAddMusicDirectoryAsync();
        StatusBarText = statusMessage;

        if (refreshNeeded)
        {
            await Library.LoadLibraryAsync();
        }
        else if (string.IsNullOrEmpty(statusMessage))
        {
            UpdateStatusBarText();
        }
    }

    private async Task HandleOpenEditSongMetadataDialogAsync(object? songObject)
    {
        string statusMessage = await _componentsManager.InteractionCoordinator.CoordinateEditSongMetadataAsync(songObject as Song);
        StatusBarText = statusMessage;
        if (string.IsNullOrEmpty(statusMessage))
        {
            UpdateStatusBarText();
        }
    }

    private bool CanOpenEditSongMetadataDialog(object? songObject)
    {
        return songObject is Song && !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading);
    }

    public void Dispose()
    {
        AlbumArtForAbstractBackground?.Dispose();
        _componentsManager?.Dispose();
        _ownerView = null;
    }
}