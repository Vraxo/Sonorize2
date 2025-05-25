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
using System.IO; // Required for Path.GetFullPath, Directory.Exists
using System.Collections.Generic; // Required for List
using System.Security.Cryptography; // Required for shuffle randomization
using System.Runtime.InteropServices; // Required for Marshal.Copy

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    private readonly ScrobblingService _scrobblingService; // Added

    // Expose the Services directly for child VMs or public properties
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    // Expose the child ViewModels
    public LibraryViewModel Library { get; set; }
    public LoopEditorViewModel LoopEditor { get; }
    public PlaybackViewModel Playback { get; } // Playback ViewModel
    public string StatusBarText { get => field; set => SetProperty(ref field, value); } = "Welcome to Sonorize!";

    // Property to control the selected tab index in the main TabControl
    public int ActiveTabIndex { get => field; set => SetProperty(ref field, value); } = 0; // Default to Library tab (index 0)


    // IsLoadingLibrary is a proxy to Library's state
    public bool IsLoadingLibrary { get => Library.IsLoadingLibrary; }

    public bool IsAdvancedPanelVisible { get => field; set { if (SetProperty(ref field, value)) OnAdvancedPanelVisibleChanged(); } }

    // Top-level commands
    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }
    public ICommand ToggleAdvancedPanelCommand { get; }

    private readonly Random _shuffleRandom = new(); // Simple Random instance for shuffle randomization

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService,
        LoopDataService loopDataService,
        ScrobblingService scrobblingService) // Added ScrobblingService
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService; // PlaybackService now receives ScrobblingService from App.cs
        CurrentTheme = theme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;
        _scrobblingService = scrobblingService; // Store ScrobblingService

        // Initialize child ViewModels, passing required dependencies
        // Pass 'this' to LibraryViewModel
        Library = new LibraryViewModel(this, _settingsService, _musicLibraryService, _loopDataService);
        Playback = new PlaybackViewModel(PlaybackService, _waveformService); // Pass PlaybackService and WaveformService
        LoopEditor = new LoopEditorViewModel(PlaybackService, _loopDataService); // Pass PlaybackService and LoopDataService


        // Subscribe to child ViewModel property changes relevant to the parent VM
        Library.PropertyChanged += Library_PropertyChanged;
        Playback.PropertyChanged += Playback_PropertyChanged; // Listen to Playback VM changes

        // Subscribe to the new PlaybackEndedNaturally event
        PlaybackService.PlaybackEndedNaturally += PlaybackService_PlaybackEndedNaturally;

        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(), _ => !Library.IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !Library.IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !Library.IsLoadingLibrary);

        // ToggleAdvancedPanelCommand depends on Playback.HasCurrentSong (derived from PlaybackService)
        ToggleAdvancedPanelCommand = new RelayCommand(
            _ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible,
            _ => Playback.HasCurrentSong && !Library.IsLoadingLibrary);


        // Initial state update
        UpdateAllUIDependentStates();
    }

    // Handler for the new PlaybackEndedNaturally event
    private void PlaybackService_PlaybackEndedNaturally(object? sender, EventArgs e)
    {
        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally event received.");
        // This handler is invoked on the UI thread via Dispatcher.UIThread.InvokeAsync within PlaybackService.OnPlaybackStopped.

        var currentSong = Library.SelectedSong; // Get the song that just ended
        var currentList = Library.FilteredSongs.ToList(); // Get a snapshot of the current list

        if (currentSong == null || !currentList.Any())
        {
            Debug.WriteLine("[MainVM] Playback ended naturally, but no song selected or library list is empty. Stopping playback.");
            PlaybackService.Stop(); // Ensure state is stopped if no song is loaded
            return; // Handled
        }

        // --- Logic for determining the next action based on RepeatMode and ShuffleEnabled ---

        var repeatMode = Playback.RepeatMode;
        var shuffleEnabled = Playback.ShuffleEnabled;

        if (repeatMode == RepeatMode.RepeatOne)
        {
            Debug.WriteLine($"[MainVM] Playback ended naturally. Repeat Mode is RepeatOne. Replaying: {currentSong.Title}");
            PlaybackService.Play(currentSong); // Directly tell the service to play the *same* song instance
            Debug.WriteLine("[MainVM] PlaybackService.Play called directly for RepeatOne.");
            return; // Handled
        }

        if (repeatMode == RepeatMode.None)
        {
            Debug.WriteLine("[MainVM] Playback ended naturally. Repeat Mode is None. Stopping playback.");
            PlaybackService.Stop(); // Explicitly stop playback
            return; // Handled
        }

        // If we reach here, RepeatMode is either PlayOnce or RepeatAll
        Song? nextSong = null;

        if (shuffleEnabled)
        {
            // Shuffle logic: Pick a random song (excluding current if possible)
            Debug.WriteLine("[MainVM] Playback ended naturally. Shuffle is Enabled.");
            if (currentList.Any())
            {
                var potentialNextSongs = currentList.Where(s => s != currentSong).ToList();
                if (potentialNextSongs.Any())
                {
                    var nextIndex = _shuffleRandom.Next(potentialNextSongs.Count);
                    nextSong = potentialNextSongs[nextIndex];
                    Debug.WriteLine($"[MainVM] Shuffle pick: {nextSong?.Title ?? "null"}");
                }
                else if (currentList.Count == 1)
                {
                    Debug.WriteLine($"[MainVM] Shuffle enabled, but only one song ({currentSong.Title}) in list. Cannot pick a different one.");
                    // In shuffle mode with only one song, ending means stopping unless RepeatOne was active (handled above).
                    nextSong = null; // Will fall through to stop logic
                }
                else
                {
                    Debug.WriteLine("[MainVM] Shuffle enabled, list issues preventing next song pick.");
                    nextSong = null; // Will fall through to stop logic
                }
            }
            else
            {
                Debug.WriteLine("[MainVM] Shuffle enabled, but list is empty.");
                nextSong = null; // Will fall through to stop logic
            }
        }
        else // Shuffle Disabled - Sequential or RepeatAll
        {
            Debug.WriteLine("[MainVM] Playback ended naturally. Shuffle is Disabled.");
            var currentIndex = currentList.IndexOf(currentSong);
            if (currentIndex != -1 && currentIndex < currentList.Count - 1)
            {
                nextSong = currentList[currentIndex + 1]; // Play the next song sequentially
                Debug.WriteLine($"[MainVM] Sequential next: {nextSong?.Title ?? "null"}");
            }
            else // Reached the end of the sequential list
            {
                Debug.WriteLine("[MainVM] End of sequential list reached.");
                // Check Repeat All *only* here
                if (repeatMode == RepeatMode.RepeatAll && currentList.Any())
                {
                    nextSong = currentList.First(); // Wrap around
                    Debug.WriteLine($"[MainVM] RepeatAll active, wrapping around to first: {nextSong.Title}");
                }
                else
                {
                    Debug.WriteLine($"[MainVM] RepeatMode is {repeatMode} (not RepeatAll), end of list reached. Stopping.");
                    nextSong = null; // Fall through to stop logic
                }
            }
        }

        // --- Trigger playback of the determined next song or stop if none ---
        if (nextSong != null)
        {
            // Setting Library.SelectedSong triggers the Library_PropertyChanged handler,
            // which then calls PlaybackService.Play(nextSong). This updates the UI selection.
            Debug.WriteLine($"[MainVM] Setting Library.SelectedSong to {nextSong.Title}.");
            Library.SelectedSong = nextSong;
        }
        else
        {
            // If no next song was determined (end of PlayOnce sequential, shuffle failed in single-item list, empty list)
            Debug.WriteLine("[MainVM] No next song determined. Calling PlaybackService.Stop().");
            PlaybackService.Stop(); // Explicitly stop playback
        }

        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally handler completed.");
    }

    private void Library_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(Library.SelectedSong):
                    Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed to: {Library.SelectedSong?.Title ?? "null"}. Instance: {Library.SelectedSong?.GetHashCode() ?? 0}");

                    if (Library.SelectedSong != null && PlaybackService.CurrentSong != Library.SelectedSong)
                    {
                        Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed to a *different* song ({Library.SelectedSong.Title}) than PlaybackService.CurrentSong ({PlaybackService.CurrentSong?.Title ?? "null"}). Calling PlaybackService.Play().");
                        PlaybackService.Play(Library.SelectedSong);
                    }
                    else if (Library.SelectedSong != null && PlaybackService.CurrentSong == Library.SelectedSong)
                    {
                        Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed but is the SAME song instance as PlaybackService.CurrentSong ({Library.SelectedSong.Title}). Assuming RepeatOne handled it or user re-clicked already playing song. No Play call needed here.");
                    }
                    else if (Library.SelectedSong == null)
                    {
                        Debug.WriteLine("[MainVM_LibChanged] Library.SelectedSong is null. No Play call needed here. PlaybackService.Stop might have been called.");
                        // If selection is cleared, and playback is active, stop playback.
                        // This is more robust if clearing selection should always stop.
                        // However, PlaybackService.Stop() is preferred to manage state and scrobbling.
                        // if(PlaybackService.IsPlaying) PlaybackService.Stop(); 
                    }

                    RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(Library.IsLoadingLibrary):
                    OnPropertyChanged(nameof(IsLoadingLibrary));
                    RaiseAllCommandsCanExecuteChanged();
                    UpdateStatusBarText();
                    break;
                case nameof(Library.LibraryStatusText):
                    UpdateStatusBarText();
                    break;
            }
        });
    }

    private void Playback_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PlaybackViewModel.CurrentSong):
                    OnPropertyChanged(nameof(Playback.CurrentSong));
                    OnPropertyChanged(nameof(Playback.HasCurrentSong));
                    RaiseAllCommandsCanExecuteChanged();

                    if (!Playback.HasCurrentSong && Library.SelectedSong != null)
                    {
                        Debug.WriteLine("[MainVM_PlaybackChanged] PlaybackService has no current song. Clearing Library selection.");
                        Library.SelectedSong = null;
                    }

                    if (Playback.CurrentSong != null && IsAdvancedPanelVisible)
                    {
                        Debug.WriteLine("[MainVM_PlaybackChanged] Playback has current song, advanced panel is visible. Requesting waveform load.");
                        _ = Playback.LoadWaveformForCurrentSongAsync();
                    }

                    UpdateStatusBarText();
                    OnPropertyChanged(nameof(Playback.CurrentTimeDisplay));
                    OnPropertyChanged(nameof(Playback.TotalTimeDisplay));
                    RaiseAllCommandsCanExecuteChanged();

                    break;
                case nameof(PlaybackViewModel.CurrentPlaybackStatus):
                    OnPropertyChanged(nameof(Playback.CurrentPlaybackStatus));
                    OnPropertyChanged(nameof(Playback.IsPlaying));
                    UpdateStatusBarText();
                    RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(PlaybackViewModel.CurrentPosition):
                    OnPropertyChanged(nameof(Playback.CurrentPosition));
                    OnPropertyChanged(nameof(Playback.CurrentPositionSeconds));
                    OnPropertyChanged(nameof(Playback.CurrentTimeDisplay));
                    break;
                case nameof(PlaybackViewModel.CurrentSongDuration):
                    OnPropertyChanged(nameof(Playback.CurrentSongDuration));
                    OnPropertyChanged(nameof(Playback.CurrentSongDurationSeconds));
                    OnPropertyChanged(nameof(Playback.TotalTimeDisplay));
                    RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(PlaybackViewModel.IsWaveformLoading):
                    OnPropertyChanged(nameof(Playback.IsWaveformLoading));
                    break;
                case nameof(PlaybackViewModel.WaveformRenderData):
                    OnPropertyChanged(nameof(Playback.WaveformRenderData));
                    break;
                case nameof(PlaybackViewModel.ShuffleEnabled):
                case nameof(PlaybackViewModel.RepeatMode):
                    Playback.RaisePlaybackCommandCanExecuteChanged();
                    UpdateStatusBarText();
                    break;
            }
        });
    }

    private void OnAdvancedPanelVisibleChanged()
    {
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        if (IsAdvancedPanelVisible && Playback.HasCurrentSong && !Playback.WaveformRenderData.Any() && !Playback.IsWaveformLoading)
        {
            Debug.WriteLine("[MainVM] Advanced Panel visible, song is playing, waveform not loaded/loading. Requesting waveform load.");
            _ = Playback.LoadWaveformForCurrentSongAsync();
        }
    }

    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(IsLoadingLibrary));
        OnPropertyChanged(nameof(Playback.CurrentSong));
        OnPropertyChanged(nameof(Playback.HasCurrentSong));
        OnPropertyChanged(nameof(IsAdvancedPanelVisible));
        OnPropertyChanged(nameof(ActiveTabIndex));

        UpdateStatusBarText();
        RaiseAllCommandsCanExecuteChanged();
    }

    public void RaiseAllCommandsCanExecuteChanged()
    {
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();

        Library.RaiseLibraryCommandsCanExecuteChanged();
        Library.RaiseNavigationCommandsCanExecuteChanged();
        Playback.RaisePlaybackCommandCanExecuteChanged();
        LoopEditor.RaiseLoopCommandCanExecuteChanged();
    }


    private void UpdateStatusBarText()
    {
        string status;
        if (Playback.HasCurrentSong)
        {
            string stateStr = Playback.CurrentPlaybackStatus switch { PlaybackStateStatus.Playing => "Playing", PlaybackStateStatus.Paused => "Paused", PlaybackStateStatus.Stopped => "Stopped", _ => "Idle" };
            status = $"{stateStr}: {Playback.CurrentSong?.Title ?? "Unknown Song"}";
            if (LoopEditor.IsCurrentLoopActiveUiBinding && Playback.CurrentSong?.SavedLoop != null)
            {
                status += $" (Loop Active)";
            }

            string modeStatus = "";
            if (Playback.ShuffleEnabled)
            {
                modeStatus += " | Shuffle";
            }
            modeStatus += Playback.RepeatMode switch
            {
                RepeatMode.None => " | Do Nothing",
                RepeatMode.PlayOnce => " | Play Once",
                RepeatMode.RepeatOne => " | Repeat Song",
                RepeatMode.RepeatAll => " | Repeat All",
                _ => ""
            };

            if (!string.IsNullOrEmpty(modeStatus))
            {
                status += modeStatus;
            }
        }
        else
        {
            status = Library.LibraryStatusText;
        }
        StatusBarText = status;
    }

    private async Task LoadMusicLibrary()
    {
        await Library.LoadLibraryAsync();
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;

        var currentSettingsBeforeDialog = _settingsService.LoadSettings();
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };

        await settingsDialog.ShowDialog(owner);

        if (settingsVM.SettingsChanged)
        {
            Debug.WriteLine("[MainVM] Settings changed detected after dialog closed.");
            var newSettingsAfterDialog = _settingsService.LoadSettings();
            bool dirsActuallyChanged = !currentSettingsBeforeDialog.MusicDirectories.SequenceEqual(newSettingsAfterDialog.MusicDirectories);
            bool themeActuallyChanged = currentSettingsBeforeDialog.PreferredThemeFileName != newSettingsAfterDialog.PreferredThemeFileName;
            bool scrobbleSettingsActuallyChanged =
                currentSettingsBeforeDialog.LastfmScrobblingEnabled != newSettingsAfterDialog.LastfmScrobblingEnabled ||
                currentSettingsBeforeDialog.LastfmUsername != newSettingsAfterDialog.LastfmUsername ||
                currentSettingsBeforeDialog.LastfmPassword != newSettingsAfterDialog.LastfmPassword;


            if (dirsActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Music directories changed. Reloading library.");
                await Library.LoadLibraryAsync();
            }

            if (themeActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Theme changed. Restart recommended.");
                StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect.";
            }

            if (scrobbleSettingsActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Scrobbling settings changed. Refreshing ScrobblingService.");
                _scrobblingService.RefreshSettings();
                // Optionally, update status bar if scrobbling got enabled/disabled
                if (newSettingsAfterDialog.LastfmScrobblingEnabled && !string.IsNullOrEmpty(newSettingsAfterDialog.LastfmUsername))
                {
                    if (StatusBarText.Contains("restart Sonorize")) {/* Append if needed */} else StatusBarText = "Scrobbling enabled.";
                }
                else if (!newSettingsAfterDialog.LastfmScrobblingEnabled && currentSettingsBeforeDialog.LastfmScrobblingEnabled)
                {
                    if (StatusBarText.Contains("restart Sonorize")) {/* Append if needed */} else StatusBarText = "Scrobbling disabled.";
                }
            }

            if (!themeActuallyChanged && !dirsActuallyChanged && !scrobbleSettingsActuallyChanged)
            {
                UpdateStatusBarText(); // If only minor settings changed that don't require reload/restart/refresh
            }
        }
        else
        {
            Debug.WriteLine("[MainVM] Settings dialog closed, no changes reported by SettingsViewModel.");
            UpdateStatusBarText();
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;

        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Music Directory", AllowMultiple = false });

        if (result != null && result.Count > 0)
        {
            string? folderPath = null;
            try
            {
                folderPath = Path.GetFullPath(result[0].Path.LocalPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainVM] Error getting full path for selected directory: {ex.Message}");
                StatusBarText = "Error getting path for selected directory.";
                return;
            }

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                var settings = _settingsService.LoadSettings();
                if (!settings.MusicDirectories.Any(d => string.Equals(d, folderPath, StringComparison.OrdinalIgnoreCase)))
                {
                    settings.MusicDirectories.Add(folderPath);
                    _settingsService.SaveSettings(settings);
                    Debug.WriteLine($"[MainVM] Added new directory: {folderPath}. Reloading library.");
                    await Library.LoadLibraryAsync();
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
            UpdateStatusBarText();
        }
    }
}