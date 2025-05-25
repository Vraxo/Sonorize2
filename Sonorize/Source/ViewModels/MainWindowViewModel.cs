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
        LoopDataService loopDataService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;

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
                    // When a song is selected in the Library (either manually or programmatically by navigation logic),
                    // tell the PlaybackService to play it.
                    // This is handled here for explicit selection by the user or navigation commands.
                    // RepeatOne is handled by calling PlaybackService.Play directly in the PlaybackEndedNaturally handler.
                    // This handler should only initiate playback when a *new* song is selected from the list by the user or sequence/shuffle logic.
                    // If PlaybackService.CurrentSong is null, this is the first song, or a stop occurred. Play the selected song.
                    // If PlaybackService.CurrentSong is not null, and it's different from Library.SelectedSong, a new song was selected. Play it.
                    // If PlaybackService.CurrentSong is not null, and it's the SAME as Library.SelectedSong, this selection change might be due to:
                    // 1. RepeatOne completion (handled by direct Play call in handler)
                    // 2. User clicking on the already selected song (no action needed, song is already playing)
                    // 3. PlaybackEndedNaturally handler reaching end without repeat/shuffle (should stop)
                    // 4. PlaybackService.Stop() setting CurrentSong = null then Library.SelectedSong = null (handled by Playback_PropertyChanged)

                    if (Library.SelectedSong != null && PlaybackService.CurrentSong != Library.SelectedSong)
                    {
                        Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed to a *different* song ({Library.SelectedSong.Title}) than PlaybackService.CurrentSong ({PlaybackService.CurrentSong?.Title ?? "null"}). Calling PlaybackService.Play().");
                        PlaybackService.Play(Library.SelectedSong);
                    }
                    else if (Library.SelectedSong != null && PlaybackService.CurrentSong == Library.SelectedSong)
                    {
                        // Song instance is the same. This might be a user re-click or RepeatOne completing.
                        // RepeatOne case is handled by direct Play in PlaybackEndedNaturally. User re-click means it's already playing.
                        Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed but is the SAME song instance as PlaybackService.CurrentSong ({Library.SelectedSong.Title}). Assuming RepeatOne handled it or user re-clicked already playing song. No Play call needed here.");
                    }
                    else if (Library.SelectedSong == null)
                    {
                        // If selection is cleared (e.g., by search filter or explicit stop), we don't start playback here.
                        Debug.WriteLine("[MainVM_LibChanged] Library.SelectedSong is null. No Play call needed.");
                    }


                    // Update commands that might depend on a song being selected/playing
                    RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(Library.IsLoadingLibrary):
                    // Propagate the loading state change
                    OnPropertyChanged(nameof(IsLoadingLibrary));
                    // Update commands dependent on loading state
                    RaiseAllCommandsCanExecuteChanged();
                    UpdateStatusBarText(); // Status text includes loading info
                    break;
                case nameof(Library.LibraryStatusText):
                    UpdateStatusBarText(); // Library status affects overall status bar
                    break;
                    // Properties like Artists, Albums, FilteredSongs changing are handled by LibraryVM itself.
                    // When FilteredSongs changes, LibraryVM calls RaiseNavigationCommandsCanExecuteChanged internally.
            }
        });
    }

    private void Playback_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Listen for PlaybackViewModel changes that affect MainViewModel's state/UI
            switch (e.PropertyName)
            {
                case nameof(PlaybackViewModel.CurrentSong):
                    OnPropertyChanged(nameof(Playback.CurrentSong)); // Propagate the song itself
                    OnPropertyChanged(nameof(Playback.HasCurrentSong)); // Propagate derived property
                    // Commands that might depend on a song being loaded (like ToggleAdvancedPanelCommand)
                    RaiseAllCommandsCanExecuteChanged();

                    // If playback service reports no current song, ensure library selection is cleared
                    // Only clear selection if it matches the song that just became null in PlaybackService.
                    // This prevents clearing selection if the user has already selected a *new* song.
                    // However, relying on PlaybackService's CurrentSong becoming null is the most reliable way
                    // to know playback has stopped *without* a new song immediately starting (like in RepeatOne).
                    // Let's clear Library.SelectedSong if Playback.HasCurrentSong becomes false. This simplifies state.
                    if (!Playback.HasCurrentSong && Library.SelectedSong != null)
                    {
                        Debug.WriteLine("[MainVM_PlaybackChanged] PlaybackService has no current song. Clearing Library selection.");
                        Library.SelectedSong = null; // This will trigger Library_PropertyChanged (which correctly won't call Play)
                    }

                    // When a new song is set in PlaybackService (either by Play() or PlaybackEndedNaturally handler),
                    // trigger waveform loading if panel is visible.
                    // Check if the *new* CurrentSong is different from the *previous* one (handled by PS).
                    if (Playback.CurrentSong != null && IsAdvancedPanelVisible)
                    {
                        Debug.WriteLine("[MainVM_PlaybackChanged] Playback has current song, advanced panel is visible. Requesting waveform load.");
                        // Use await _ = to suppress the warning about calling async void, while ensuring the task runs.
                        _ = Playback.LoadWaveformForCurrentSongAsync(); // Delegate waveform load
                    }
                    // Note: Clearing waveform when song becomes null is handled inside PlaybackViewModel's PS handler

                    // Update status bar text whenever the current song changes
                    UpdateStatusBarText();

                    // Update time displays when song changes (delegated to PlaybackVM)
                    OnPropertyChanged(nameof(Playback.CurrentTimeDisplay)); // Propagate derived property
                    OnPropertyChanged(nameof(Playback.TotalTimeDisplay)); // Propagate derived property

                    // Commands in PlaybackVM (Play/Pause, Seek, Speed/Pitch, Mode toggles) are handled by PlaybackVM itself.
                    // Library navigation commands are handled by LibraryVM's SelectedSong/FilteredSongs change.
                    // ToggleAdvancedPanelCommand depends on Playback.HasCurrentSong, so need to raise.
                    RaiseAllCommandsCanExecuteChanged();

                    break;
                case nameof(PlaybackViewModel.CurrentPlaybackStatus):
                    OnPropertyChanged(nameof(Playback.CurrentPlaybackStatus)); // Propagate status
                    OnPropertyChanged(nameof(Playback.IsPlaying)); // Derived from status
                    UpdateStatusBarText(); // Playback status affects overall status bar
                    // Commands in PlaybackVM already listen to this. MainVM commands potentially affected? (currently none directly)
                    RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(PlaybackViewModel.CurrentPosition):
                    OnPropertyChanged(nameof(Playback.CurrentPosition)); // Propagate position
                    OnPropertyChanged(nameof(Playback.CurrentPositionSeconds)); // Propagate derived property
                    OnPropertyChanged(nameof(Playback.CurrentTimeDisplay)); // Propagate current time display
                    // Loop editor and UI slider are bound directly to PlaybackVM properties.
                    // Commands in PlaybackVM already listen to this.
                    // Raising all commands here is usually not needed for position change.
                    break;
                case nameof(PlaybackViewModel.CurrentSongDuration):
                    OnPropertyChanged(nameof(Playback.CurrentSongDuration)); // Propagate duration
                    OnPropertyChanged(nameof(Playback.CurrentSongDurationSeconds));
                    OnPropertyChanged(nameof(Playback.TotalTimeDisplay)); // Propagate total time display
                                                                          // UI slider is bound directly.
                    RaiseAllCommandsCanExecuteChanged(); // PlaybackVM Seek command CanExecute depends on duration > 0
                    break;
                case nameof(PlaybackViewModel.IsWaveformLoading):
                    OnPropertyChanged(nameof(Playback.IsWaveformLoading)); // Propagate state
                    // Commands in PlaybackVM already listen to this.
                    // Raising all commands here is usually not needed unless a MainVM command directly depends on this.
                    break;
                case nameof(PlaybackViewModel.WaveformRenderData):
                    OnPropertyChanged(nameof(Playback.WaveformRenderData)); // Propagate data
                    // Waveform display is bound directly.
                    break;
                case nameof(PlaybackViewModel.ShuffleEnabled): // Modes changed
                case nameof(PlaybackViewModel.RepeatMode): // Modes changed
                                                           // These affect the PlaybackEndedNaturally logic in MainVM,
                                                           // but they also affect PlaybackVM commands and UI bindings.
                                                           // Raise PlaybackVM commands' CanExecute state.
                    Playback.RaisePlaybackCommandCanExecuteChanged();
                    // Raising all commands here is generally redundant unless a MainVM command directly depends on these modes.
                    // Update status bar to reflect mode changes
                    UpdateStatusBarText();
                    break;

                    // PlaybackSpeed, PlaybackPitch, derived display properties are handled within PlaybackVM.
                    // LoopEditor VM listens directly to PlaybackService for position/duration.
            }
        });
    }

    private void OnAdvancedPanelVisibleChanged()
    {
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // If Advanced Panel becomes visible, and a song is playing, load its waveform.
        // Access PlaybackVM properties directly
        if (IsAdvancedPanelVisible && Playback.HasCurrentSong && !Playback.WaveformRenderData.Any() && !Playback.IsWaveformLoading)
        {
            Debug.WriteLine("[MainVM] Advanced Panel visible, song is playing, waveform not loaded/loading. Requesting waveform load.");
            // Use await _ = to suppress the warning about calling async void, while ensuring the task runs.
            _ = Playback.LoadWaveformForCurrentSongAsync(); // Delegate waveform load
        }
        // Note: If it becomes hidden, we don't clear the waveform data automatically here.
        // Clearing upon song change/stop is handled by PlaybackViewModel's PS handler.
    }

    private void UpdateAllUIDependentStates()
    {
        // Update main VM properties that depend on child VM states
        OnPropertyChanged(nameof(IsLoadingLibrary)); // Depends on Library.IsLoadingLibrary
        OnPropertyChanged(nameof(Playback.CurrentSong)); // Ensure Playback's CurrentSong is reflected
        OnPropertyChanged(nameof(Playback.HasCurrentSong)); // Ensure Playback's HasCurrentSong is reflected
        OnPropertyChanged(nameof(IsAdvancedPanelVisible)); // Ensure panel visibility is reflected
        OnPropertyChanged(nameof(ActiveTabIndex)); // Ensure tab index is reflected

        UpdateStatusBarText(); // Depends on Playback and Library status

        // Trigger commands CanExecute updates for all VMs
        RaiseAllCommandsCanExecuteChanged();
    }

    /// <summary>
    /// Raises CanExecuteChanged for commands owned by this ViewModel and tells child VMs to do the same.
    /// </summary>
    public void RaiseAllCommandsCanExecuteChanged()
    {
        // Raise CanExecuteChanged for commands owned by MainWindowViewModel
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();

        // Tell child VMs to raise their commands' CanExecuteChanged
        // This now includes the navigation commands within LibraryViewModel
        Library.RaiseLibraryCommandsCanExecuteChanged();
        Library.RaiseNavigationCommandsCanExecuteChanged(); // Explicitly call navigation commands
        Playback.RaisePlaybackCommandCanExecuteChanged();
        LoopEditor.RaiseLoopCommandCanExecuteChanged();
    }


    private void UpdateStatusBarText()
    {
        // Combine status from PlaybackViewModel and LibraryViewModel
        string status;
        if (Playback.HasCurrentSong)
        {
            string stateStr = Playback.CurrentPlaybackStatus switch { PlaybackStateStatus.Playing => "Playing", PlaybackStateStatus.Paused => "Paused", PlaybackStateStatus.Stopped => "Stopped", _ => "Idle" };
            // Access CurrentSong via Playback property
            status = $"{stateStr}: {Playback.CurrentSong?.Title ?? "Unknown Song"}";
            // Use LoopEditor's property for loop active state displayed in status bar
            // Access CurrentSong via Playback property
            if (LoopEditor.IsCurrentLoopActiveUiBinding && Playback.CurrentSong?.SavedLoop != null)
            {
                status += $" (Loop Active)";
            }

            // Add mode status
            string modeStatus = "";
            if (Playback.ShuffleEnabled)
            {
                modeStatus += " | Shuffle";
            }
            // Use RepeatMode property
            modeStatus += Playback.RepeatMode switch
            {
                RepeatMode.None => " | Do Nothing",
                RepeatMode.PlayOnce => " | Play Once",
                RepeatMode.RepeatOne => " | Repeat Song",
                RepeatMode.RepeatAll => " | Repeat All",
                _ => "" // Should not happen
            };


            if (!string.IsNullOrEmpty(modeStatus))
            {
                status += modeStatus;
            }
        }
        else
        {
            // No playback info, use library status
            status = Library.LibraryStatusText;
        }
        StatusBarText = status;
    }

    private async Task LoadMusicLibrary()
    {
        // Delegate the core loading logic to the LibraryViewModel
        await Library.LoadLibraryAsync();
        // The Library_PropertyChanged handler for IsLoadingLibrary will trigger status updates.
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
                // Status bar update triggered by Library.IsLoadingLibrary change.
            }

            if (themeActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Theme changed. Restart recommended.");
                // Update status bar to inform user about restart
                StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect.";
            }
        }
        else
        {
            Debug.WriteLine("[MainVM] Settings dialog closed, no changes reported by SettingsViewModel.");
            // Ensure status bar reflects current state after dialog if no reload/theme change occurred
            UpdateStatusBarText(); // Revert status bar to normal if not loading
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false; // Hide advanced panel

        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Music Directory", AllowMultiple = false });

        if (result != null && result.Count > 0)
        {
            string? folderPath = null;
            try
            {
                // Using Path.GetFullPath is crucial for consistent path representation
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
                // Use a case-insensitive comparison for existing directories
                if (!settings.MusicDirectories.Any(d => string.Equals(d, folderPath, StringComparison.OrdinalIgnoreCase)))
                {
                    settings.MusicDirectories.Add(folderPath);
                    _settingsService.SaveSettings(settings);
                    Debug.WriteLine($"[MainVM] Added new directory: {folderPath}. Reloading library.");
                    await Library.LoadLibraryAsync(); // Delegate library reload
                    // Status bar update triggered by Library.IsLoadingLibrary change.
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

    // Use a cryptographically secure random number generator for better shuffle randomness if needed
    // private int GetNextRandomIndex(int maxIndex)
    // {
    //     using (var rng = new RNGCryptoServiceProvider())
    //     {
    //         byte[] data = new byte[sizeof(uint)]; // Use uint size for randomness
    //         rng.GetBytes(data);
    //         uint randomValue = BitConverter.ToUInt32(data, 0);
    //         return (int)(randomValue % maxIndex);
    //     }
    // }
}