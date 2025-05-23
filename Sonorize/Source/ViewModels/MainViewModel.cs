using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.Utils;
using System.IO; // Required for Path and Directory

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;

    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    // Expose the child ViewModels
    // Removed the explicit 'public' from 'set' as it's redundant when the property is public
    public LibraryViewModel Library { get; set; }
    public LoopEditorViewModel LoopEditor { get; }

    // HasCurrentSong and SliderPositionSeconds/CurrentTimeTotalTimeDisplay remain here
    // as they relate directly to the *currently playing* song, which is PlaybackService's concern.
    public bool HasCurrentSong => PlaybackService.CurrentSong != null;

    private string _statusBarText = "Welcome to Sonorize!";
    public string StatusBarText { get => _statusBarText; set => SetProperty(ref _statusBarText, value); }

    // IsLoadingLibrary is now tied to the Library ViewModel's state
    public bool IsLoadingLibrary { get => Library.IsLoadingLibrary; } // Removed setter and internal field

    private bool _isAdvancedPanelVisible;
    public bool IsAdvancedPanelVisible { get => _isAdvancedPanelVisible; set { if (SetProperty(ref _isAdvancedPanelVisible, value)) OnAdvancedPanelVisibleChanged(); } }

    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed { get => _playbackSpeed; set { value = Math.Clamp(value, 0.5, 2.0); if (SetProperty(ref _playbackSpeed, value)) { PlaybackService.PlaybackRate = (float)value; OnPropertyChanged(nameof(PlaybackSpeedDisplay)); } } }
    public string PlaybackSpeedDisplay => $"{PlaybackSpeed:F2}x";

    private double _playbackPitch = 0.0;
    public double PlaybackPitch { get => _playbackPitch; set { value = Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2.0; value = Math.Clamp(value, -4.0, 4.0); if (SetProperty(ref _playbackPitch, value)) { PlaybackService.PitchSemitones = (float)_playbackPitch; OnPropertyChanged(nameof(PlaybackPitchDisplay)); } } }
    public string PlaybackPitchDisplay => $"{PlaybackPitch:+0.0;-0.0;0} st";

    public ObservableCollection<WaveformPoint> WaveformRenderData { get; } = new();
    private bool _isWaveformLoading = false;
    public bool IsWaveformLoading { get => _isWaveformLoading; private set => SetProperty(ref _isWaveformLoading, value); }

    public double SliderPositionSeconds
    {
        get => PlaybackService.CurrentPosition.TotalSeconds;
        set
        {
            if (PlaybackService.HasCurrentSong && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
            {
                if (Math.Abs(PlaybackService.CurrentPosition.TotalSeconds - value) > 0.1)
                {
                    Debug.WriteLine($"[MainVM.SliderPositionSeconds.set] User seeking via slider to: {value:F2}s. Current playback pos: {PlaybackService.CurrentPosition.TotalSeconds:F2}s");
                    PlaybackService.Seek(TimeSpan.FromSeconds(value));
                }
            }
        }
    }

    public string CurrentTimeTotalTimeDisplay
    {
        get
        {
            if (PlaybackService.CurrentSong != null && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
            {
                return $"{PlaybackService.CurrentPosition:mm\\:ss} / {PlaybackService.CurrentSongDuration:mm\\:ss}";
            }
            return "--:-- / --:--";
        }
    }

    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }
    public ICommand ToggleAdvancedPanelCommand { get; }

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService,
        LoopDataService loopDataService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;

        // Initialize child ViewModels
        Library = new LibraryViewModel(settingsService, musicLibraryService, loopDataService);
        LoopEditor = new LoopEditorViewModel(PlaybackService, loopDataService);

        // Subscribe to child ViewModel property changes relevant to the parent VM
        Library.PropertyChanged += Library_PropertyChanged;
        // LoopEditor changes are handled by its internal PlaybackService listener and the PlaybackService_PropertyChanged handler here.

        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(), _ => !Library.IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !Library.IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !Library.IsLoadingLibrary);
        ToggleAdvancedPanelCommand = new RelayCommand(
            _ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible,
            // Can only toggle if there's a song selected or playing
            _ => (Library.SelectedSong != null || PlaybackService.CurrentSong != null) && !Library.IsLoadingLibrary);


        PlaybackService.PropertyChanged += OnPlaybackServicePropertyChanged;
        PlaybackSpeed = 1.0;
        PlaybackPitch = 0.0;

        // Call UpdateAllUIDependentStates to sync UI initially (including loop editor state)
        UpdateAllUIDependentStates();
    }

    private void Library_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(Library.SelectedSong):
                    Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed to: {Library.SelectedSong?.Title ?? "null"}");
                    // React to song selection by potentially starting playback
                    HandleSelectedSongChange(PlaybackService.CurrentSong, Library.SelectedSong);
                    // Update toggle button CanExecute as HasCurrentSong might change
                    (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    break;
                case nameof(Library.IsLoadingLibrary):
                    // Propagate the loading state change
                    OnPropertyChanged(nameof(IsLoadingLibrary));
                    // Update commands dependent on loading state
                    (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Depends on !IsLoadingLibrary
                    UpdateStatusBarText(); // Status text includes loading info
                    break;
                case nameof(Library.LibraryStatusText):
                    UpdateStatusBarText(); // Library status affects overall status bar
                    break;
                    // Properties like Artists, Albums, FilteredSongs changing don't need explicit handling here
                    // as the UI is bound directly to Library.*
            }
        });
    }

    // OnIsLoadingLibraryChanged removed as IsLoadingLibrary is now a proxy property

    private void OnAdvancedPanelVisibleChanged()
    {
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // Load waveform when panel becomes visible, if a song is loaded and waveform isn't already there/loading
        if (IsAdvancedPanelVisible && PlaybackService.CurrentSong != null && (!WaveformRenderData.Any() || !IsWaveformLoading))
        {
            _ = LoadWaveformForCurrentSong();
        }
    }

    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(HasCurrentSong));
        // Loop editor state is updated by LoopEditorViewModel's internal logic/PlaybackService handler
        UpdateStatusBarText();
        OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay));
        RaiseAllCommandsCanExecuteChanged(); // Check commands potentially affected by HasCurrentSong, IsLoadingLibrary etc.
    }

    private void RaiseAllCommandsCanExecuteChanged()
    {
        // Commands on MainViewModel
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();

        // Commands on child ViewModels need to be triggered via their Raise method
        LoopEditor.RaiseLoopCommandCanExecuteChanged();
        Library.RaiseLibraryCommandsCanExecuteChanged(); // Call the new method in LibraryVM
    }

    /// <summary>
    /// Handles the change in the *selected* song from the library list,
    /// potentially triggering playback in the PlaybackService.
    /// </summary>
    /// <param name="oldSong">The previously selected song (or currently playing).</param>
    /// <param name="newSong">The newly selected song from the list.</param>
    private void HandleSelectedSongChange(Song? oldSong, Song? newSong)
    {
        Debug.WriteLine($"[MainVM] HandleSelectedSongChange triggered by Library.SelectedSong change. Playing: {PlaybackService.CurrentSong?.Title ?? "null"}, New Selection: {newSong?.Title ?? "null"}");

        // If a new song is selected and it's different from the current playing song, or if playback is stopped, start playback of the new song.
        if (newSong != null && (newSong != PlaybackService.CurrentSong || PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Stopped))
        {
            PlaybackService.Play(newSong); // This updates PlaybackService.CurrentSong
            // PlaybackService_PropertyChanged handler will react to this and update other UI elements and child VMs.
        }
        else if (newSong == null)
        {
            // If selection is cleared in the list, do NOT stop playback.
            // Playback stops only via explicit Stop button or song end.
            Debug.WriteLine($"[MainVM] Library selection cleared (newSong is null). Current playing song '{PlaybackService.CurrentSong?.Title ?? "null"}' continues if it was playing.");
        }
        // If newSong is the same as PlaybackService.CurrentSong and is already playing/paused, do nothing here.
        // The LoopEditorViewModel's state should already be synced via its listener to PlaybackService.CurrentSong.

        // Update UI elements that depend on the current song
        UpdateAllUIDependentStates(); // This will sync things like HasCurrentSong, status bar, command states
    }

    private void OnPlaybackServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (args.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    // The PlaybackService.CurrentSong changing is the source of truth for *what is playing*.
                    // The LibraryViewModel.SelectedSong triggered the Play() call, which caused this.
                    // We need to update UI elements that show info about the *playing* song,
                    // not necessarily the *selected* song in the list (though they are often the same).

                    // Update MainVM-specific UI elements and trigger waveform loading.
                    UpdateAllUIDependentStates();
                    OnPropertyChanged(nameof(SliderPositionSeconds)); // Slider max changes with song
                    OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay)); // Total time changes with song

                    // Trigger waveform load for the new song if Advanced Panel is visible
                    if (PlaybackService.CurrentSong != null && IsAdvancedPanelVisible)
                    {
                        _ = LoadWaveformForCurrentSong();
                    }
                    else if (PlaybackService.CurrentSong == null)
                    {
                        // Clear waveform if no song is loaded (e.g., after calling Stop)
                        WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData)); IsWaveformLoading = false;
                    }
                    // The LoopEditorViewModel already listens to PlaybackService.CurrentSong and updates its internal state.

                    // Ensure the selected song in the list stays in sync IF the playback was triggered
                    // by selecting it in the list. This is handled by HandleSelectedSongChange now.
                    // But if playback changes *not* due to list selection (e.g., 'Next Song' button in future),
                    // we might want to update Library.SelectedSong here. For now, keep it simple.
                    // If CurrentSong changes to null due to Stop(), we should also clear Library.SelectedSong
                    if (PlaybackService.CurrentSong == null && Library.SelectedSong != null)
                    {
                        Library.SelectedSong = null;
                    }


                    break;

                case nameof(PlaybackService.IsPlaying):
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    UpdateStatusBarText(); // Playback status affects the overall status bar
                    RaiseAllCommandsCanExecuteChanged(); // Commands like Play/Pause need state update
                    // LoopEditorViewModel also listens to PlaybackService status for command CanExecute
                    break;

                case nameof(PlaybackService.CurrentPosition):
                    // Slider binding handles this directly.
                    // LoopEditorViewModel also listens to CurrentPosition for candidate updates and CanSaveLoopRegion.
                    // We still need to notify the view that CurrentTimeTotalTimeDisplay might have changed
                    OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay));
                    LoopEditor.RaiseLoopCommandCanExecuteChanged(); // Ensure loop commands update based on position
                    break;

                case nameof(PlaybackService.CurrentSongDuration):
                    // Slider binding handles this directly.
                    // LoopEditorViewModel also listens to CurrentSongDuration for CanSaveLoopRegion.
                    // We still need to notify the view that CurrentTimeTotalTimeDisplay might have changed
                    OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay));
                    LoopEditor.RaiseLoopCommandCanExecuteChanged(); // Ensure loop commands update based on duration
                    break;
            }
        });
    }


    private async Task LoadWaveformForCurrentSong()
    {
        var songToLoadWaveformFor = PlaybackService.CurrentSong;
        if (songToLoadWaveformFor == null || string.IsNullOrEmpty(songToLoadWaveformFor.FilePath))
        {
            WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData)); IsWaveformLoading = false; return;
        }
        IsWaveformLoading = true;
        try
        {
            Debug.WriteLine($"[MainVM] Requesting waveform for: {songToLoadWaveformFor.Title}");
            // Target points should probably be based on control width or a fixed resolution
            // For a fixed 80px height control, 1000 points is likely sufficient detail.
            var points = await _waveformService.GetWaveformAsync(songToLoadWaveformFor.FilePath, 1000);
            // Check if the song is still the same after the async operation before updating the UI
            if (PlaybackService.CurrentSong == songToLoadWaveformFor)
            {
                WaveformRenderData.Clear(); foreach (var p in points) WaveformRenderData.Add(p); OnPropertyChanged(nameof(WaveformRenderData));
                Debug.WriteLine($"[MainVM] Waveform loaded for: {songToLoadWaveformFor.Title}, {points.Count} points.");
            }
            else
            {
                // Song changed during waveform generation, discard the result for the old song.
                Debug.WriteLine($"[MainVM] Waveform for {songToLoadWaveformFor.Title} loaded, but current song is now {PlaybackService.CurrentSong?.Title ?? "null"}. Discarding.");
                // Note: If the new song is null or different, the PlaybackService_PropertyChanged
                // handler for the new song would have cleared the waveform already if advanced panel is visible.
                // This ensures it's cleared if it wasn't already.
                if (PlaybackService.CurrentSong != songToLoadWaveformFor)
                {
                    WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData));
                }
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[MainVM] Failed to load waveform for {songToLoadWaveformFor.Title}: {ex.Message}"); WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData)); }
        finally { IsWaveformLoading = false; }
    }


    private void UpdateStatusBarText()
    {
        // Combine status from PlaybackService and LibraryViewModel
        string playbackStatus;
        var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong != null)
        {
            string stateStr = PlaybackService.CurrentPlaybackStatus switch { PlaybackStateStatus.Playing => "Playing", PlaybackStateStatus.Paused => "Paused", PlaybackStateStatus.Stopped => "Stopped", _ => "Idle" };
            playbackStatus = $"{stateStr}: {currentServiceSong.Title}";
            // Use LoopEditor's property for loop active state displayed in status bar
            if (LoopEditor.IsCurrentLoopActiveUiBinding && currentServiceSong.SavedLoop != null)
            {
                playbackStatus += $" (Loop Active)";
            }
        }
        else
        {
            // No playback info, use library status
            playbackStatus = Library.LibraryStatusText;
        }
        StatusBarText = playbackStatus;
    }

    private async Task LoadMusicLibrary()
    {
        // Delegate the core loading logic to the LibraryViewModel
        await Library.LoadLibraryAsync();
        // The Library_PropertyChanged handler for IsLoadingLibrary will update the status bar.
        // No need to call UpdateStatusBarText directly here after the await.
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false; // Hide advanced panel when opening settings

        var currentSettingsBeforeDialog = _settingsService.LoadSettings();
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };

        await settingsDialog.ShowDialog(owner); // Show dialog modally

        // After the dialog is closed
        if (settingsVM.SettingsChanged)
        {
            Debug.WriteLine("[MainVM] Settings changed detected after dialog closed.");
            var newSettingsAfterDialog = _settingsService.LoadSettings();
            bool dirsActuallyChanged = !currentSettingsBeforeDialog.MusicDirectories.SequenceEqual(newSettingsAfterDialog.MusicDirectories);
            bool themeActuallyChanged = currentSettingsBeforeDialog.PreferredThemeFileName != newSettingsAfterDialog.PreferredThemeFileName;

            if (dirsActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Music directories changed. Reloading library.");
                await Library.LoadLibraryAsync(); // Delegate library reload
            }

            if (themeActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Theme changed. Restart recommended.");
                // Update status bar to inform user about restart
                StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect.";
            }
            // If settings changed but neither dirs nor theme changed (e.g., future settings),
            // SettingsChanged flag might be set incorrectly or indicates a different type of setting.
            // For now, only directory and theme changes trigger actions here.
        }
        else
        {
            Debug.WriteLine("[MainVM] Settings dialog closed, no changes reported by SettingsViewModel.");
        }
        // Ensure status bar reflects current state after dialog if no reload/theme change occurred
        UpdateStatusBarText();
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false; // Hide advanced panel

        // Use StorageProvider from the Window
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Music Directory", AllowMultiple = false });

        if (result != null && result.Count > 0)
        {
            // Use Path.GetFullPath to normalize the path and handle potential Uri issues robustly
            string? folderPath = null;
            try
            {
                folderPath = Path.GetFullPath(result[0].Path.LocalPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainVM] Error getting full path for selected directory: {ex.Message}");
                // Handle error or inform user
                StatusBarText = "Error getting path for selected directory.";
                return; // Exit the method if path is invalid
            }


            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath)) // Also double-check it exists
            {
                var settings = _settingsService.LoadSettings();
                // Use normalized path for checking and adding
                if (!settings.MusicDirectories.Contains(folderPath))
                {
                    settings.MusicDirectories.Add(folderPath);
                    _settingsService.SaveSettings(settings);
                    Debug.WriteLine($"[MainVM] Added new directory: {folderPath}. Reloading library.");
                    await Library.LoadLibraryAsync(); // Delegate library reload
                    // Library_PropertyChanged handler will update status
                }
                else
                {
                    Debug.WriteLine($"[MainVM] Directory already exists: {folderPath}");
                    StatusBarText = "Directory already in library.";
                }
            }
            else
            {
                Debug.WriteLine($"[MainVM] Selected directory path is invalid or does not exist: {folderPath}");
                StatusBarText = "Invalid directory selected.";
            }
        }
        else
        {
            Debug.WriteLine("[MainVM] Folder picker cancelled or returned no results.");
            // Optionally update status bar if folder picker was cancelled
            UpdateStatusBarText(); // Revert status bar to normal if not loading
        }
    }
}