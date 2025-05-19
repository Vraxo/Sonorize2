using Avalonia.Controls;
using Sonorize.Models;
using Sonorize.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System;
using System.Diagnostics;
using Avalonia.Threading;
using System.ComponentModel;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Sonorize.Utils; // For AlbumArtistTupleComparer

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    public bool HasCurrentSong => PlaybackService.CurrentSong != null;

    private readonly ObservableCollection<Song> _allSongs = new();
    public ObservableCollection<Song> FilteredSongs { get; } = new();
    public ObservableCollection<ArtistViewModel> Artists { get; } = new();
    public ObservableCollection<AlbumViewModel> Albums { get; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery { get => _searchQuery; set { if (SetProperty(ref _searchQuery, value)) ApplyFilter(); } }

    private Song? _selectedSongInternal;
    /// <summary>
    /// Represents the song currently selected in the UI list, NOT necessarily the song being played.
    /// Playback is initiated via PlaySelectedSongCommand, not setting this property directly.
    /// </summary>
    public Song? SelectedSong
    {
        get => _selectedSongInternal;
        set
        {
            // We only want to trigger HandleSelectedSongChange if the selected item *changes* in the UI list.
            // Avoid redundant updates if the same song is set again.
            if (_selectedSongInternal != value)
            {
                var previousSong = _selectedSongInternal;
                if (SetProperty(ref _selectedSongInternal, value)) // SetProperty handles the backing field and OnPropertyChanged
                {
                    // Call a separate method to handle logic triggered by selection change
                    // (like updating loop editor fields), but NOT playback.
                    HandleSelectedSongChange(previousSong, _selectedSongInternal);
                }
            }
        }
    }

    private ArtistViewModel? _selectedArtist;
    public ArtistViewModel? SelectedArtist
    {
        get => _selectedArtist;
        set
        {
            if (SetProperty(ref _selectedArtist, value))
            {
                if (value != null)
                {
                    OnArtistSelected(value);
                }
            }
        }
    }

    private AlbumViewModel? _selectedAlbum;
    public AlbumViewModel? SelectedAlbum
    {
        get => _selectedAlbum;
        set
        {
            if (SetProperty(ref _selectedAlbum, value))
            {
                if (value != null)
                {
                    OnAlbumSelected(value);
                }
            }
        }
    }

    private int _activeTabIndex = 0;
    public int ActiveTabIndex { get => _activeTabIndex; set => SetProperty(ref _activeTabIndex, value); }

    private bool _isCurrentLoopActiveUiBinding;
    public bool IsCurrentLoopActiveUiBinding
    {
        get => _isCurrentLoopActiveUiBinding;
        set
        {
            // Only update if the backing field changes and it's a different value.
            if (SetProperty(ref _isCurrentLoopActiveUiBinding, value))
            {
                // This property is bound to the UI checkbox/toggle for the *currently playing* song's loop state.
                // Ensure the change is applied to the PlaybackService.CurrentSong if available.
                if (PlaybackService.CurrentSong != null && PlaybackService.CurrentSong.SavedLoop != null)
                {
                    PlaybackService.CurrentSong.IsLoopActive = value; // Song model will notify and trigger persistence
                }
                else if (PlaybackService.CurrentSong != null && PlaybackService.CurrentSong.SavedLoop == null && value == true)
                {
                    // If trying to activate a loop when none exists, revert the UI binding and inform the user
                    _isCurrentLoopActiveUiBinding = false;
                    OnPropertyChanged(nameof(IsCurrentLoopActiveUiBinding)); // Notify UI to revert
                    Debug.WriteLine($"[MainVM] Attempted to activate loop via UI, but no loop is defined for {PlaybackService.CurrentSong.Title}.");
                    StatusBarText = $"Cannot activate loop: No loop defined for {PlaybackService.CurrentSong.Title}.";
                }
            }
        }
    }

    private string _statusBarText = "Welcome to Sonorize!";
    public string StatusBarText { get => _statusBarText; set => SetProperty(ref _statusBarText, value); }

    private bool _isLoadingLibrary = false;
    public bool IsLoadingLibrary { get => _isLoadingLibrary; set { if (SetProperty(ref _isLoadingLibrary, value)) OnIsLoadingLibraryChanged(); } }

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

    private TimeSpan? _newLoopStartCandidate;
    public TimeSpan? NewLoopStartCandidate { get => _newLoopStartCandidate; set { SetProperty(ref _newLoopStartCandidate, value); OnPropertyChanged(nameof(CanSaveLoopRegion)); OnPropertyChanged(nameof(NewLoopStartCandidateDisplay)); } }

    private TimeSpan? _newLoopEndCandidate;
    public TimeSpan? NewLoopEndCandidate { get => _newLoopEndCandidate; set { SetProperty(ref _newLoopEndCandidate, value); OnPropertyChanged(nameof(CanSaveLoopRegion)); OnPropertyChanged(nameof(NewLoopEndCandidateDisplay)); } }

    public string NewLoopStartCandidateDisplay => _newLoopStartCandidate.HasValue ? $"{_newLoopStartCandidate.Value:mm\\:ss\\.ff}" : "Not set";
    public string NewLoopEndCandidateDisplay => _newLoopEndCandidate.HasValue ? $"{_newLoopEndCandidate.Value:mm\\:ss\\.ff}" : "Not set";

    private string _activeLoopDisplayText = "No loop defined.";
    public string ActiveLoopDisplayText { get => _activeLoopDisplayText; set => SetProperty(ref _activeLoopDisplayText, value); }

    public double SliderPositionSeconds
    {
        get => PlaybackService.CurrentPosition.TotalSeconds;
        set
        {
            if (PlaybackService.HasCurrentSong && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
            {
                // Check if the new value is significantly different from the current playback position
                // to avoid seeking for tiny adjustments that might come from the binding itself during updates.
                // A threshold of 50-100ms might be reasonable.
                if (Math.Abs(PlaybackService.CurrentPosition.TotalSeconds - value) > 0.1)
                {
                    Debug.WriteLine($"[MainVM.SliderPositionSeconds.set] User seeking via slider to: {value:F2}s. Current playback pos: {PlaybackService.CurrentPosition.TotalSeconds:F2}s");
                    PlaybackService.Seek(TimeSpan.FromSeconds(value));
                }
            }
            // We don't call OnPropertyChanged(nameof(SliderPositionSeconds)) here.
            // The slider will be updated when PlaybackService.CurrentPosition changes,
            // which triggers OnPlaybackServicePropertyChanged, which then calls
            // OnPropertyChanged(nameof(SliderPositionSeconds)). This creates the correct notification loop.
        }
    }

    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }
    public ICommand ToggleAdvancedPanelCommand { get; }
    public ICommand CaptureLoopStartCandidateCommand { get; }
    public ICommand CaptureLoopEndCandidateCommand { get; }
    public ICommand SaveLoopCommand { get; }
    public ICommand ClearLoopCommand { get; }
    public ICommand ToggleLoopActiveCommand { get; }
    public ICommand WaveformSeekCommand { get; }
    public ICommand PlaySelectedSongCommand { get; } // New Command

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

        LoadInitialDataCommand = new RelayCommand(async _ => await LoadMusicLibrary(), _ => !IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !IsLoadingLibrary);
        ToggleAdvancedPanelCommand = new RelayCommand(_ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible, _ => (SelectedSong != null || PlaybackService.CurrentSong != null) && !IsLoadingLibrary);
        CaptureLoopStartCandidateCommand = new RelayCommand(_ => NewLoopStartCandidate = PlaybackService.CurrentPosition, _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentPlaybackStatus != PlaybackStateStatus.Stopped);
        CaptureLoopEndCandidateCommand = new RelayCommand(_ => NewLoopEndCandidate = PlaybackService.CurrentPosition, _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentPlaybackStatus != PlaybackStateStatus.Stopped);
        SaveLoopCommand = new RelayCommand(SaveLoopAction, _ => CanSaveLoopRegion);
        ClearLoopCommand = new RelayCommand(ClearSavedLoopAction, _ => PlaybackService.CurrentSong?.SavedLoop != null);
        ToggleLoopActiveCommand = new RelayCommand(ToggleCurrentSongLoopActive, _ => PlaybackService.CurrentSong?.SavedLoop != null);
        WaveformSeekCommand = new RelayCommand(timeSpanObj => { if (timeSpanObj is TimeSpan ts && PlaybackService.CurrentSong != null) PlaybackService.Seek(ts); }, _ => PlaybackService.CurrentSong != null);
        PlaySelectedSongCommand = new RelayCommand(songObj => { if (songObj is Song song) PlaybackService.Play(song); }, _ => SelectedSong != null && !IsLoadingLibrary); // New Command implementation

        PlaybackService.PropertyChanged += OnPlaybackServicePropertyChanged;
        PlaybackSpeed = 1.0;
        PlaybackPitch = 0.0;
        UpdateAllUIDependentStates();
    }

    private void OnIsLoadingLibraryChanged()
    {
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PlaySelectedSongCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Re-evaluate when loading changes
        if (_isLoadingLibrary) IsAdvancedPanelVisible = false;
    }

    private void OnAdvancedPanelVisibleChanged()
    {
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // Only load waveform if the panel becomes visible AND we have a song AND waveform isn't already loaded/loading
        if (IsAdvancedPanelVisible && PlaybackService.CurrentSong != null && (!WaveformRenderData.Any() || !IsWaveformLoading))
        {
            _ = LoadWaveformForCurrentSong();
        }
    }

    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(HasCurrentSong));
        UpdateLoopEditorForCurrentSong();
        UpdateActiveLoopDisplayText();
        UpdateStatusBarText();
        RaiseAllCommandsCanExecuteChanged();
    }

    private void RaiseAllCommandsCanExecuteChanged()
    {
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleLoopActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (WaveformSeekCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PlaySelectedSongCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Re-evaluate can execute
    }

    /// <summary>
    /// Handles UI state changes when the song selected in the ListBox changes.
    /// Does NOT handle playback initiation or stopping - that is done by PlaySelectedSongCommand.
    /// </summary>
    private void HandleSelectedSongChange(Song? oldSong, Song? newSong)
    {
        Debug.WriteLine($"[MainVM] HandleSelectedSongChange (UI Selection). Old: {oldSong?.Title ?? "null"}, New: {newSong?.Title ?? "null"}");

        // Unsubscribe from PropertyChanged events on the old song if it exists
        if (oldSong != null)
        {
            oldSong.PropertyChanged -= OnSelectedSongSavedLoopChanged;
            oldSong.PropertyChanged -= OnSelectedSongIsLoopActiveChanged;
        }

        // Subscribe to PropertyChanged events on the new song if it exists
        if (newSong != null)
        {
            newSong.PropertyChanged += OnSelectedSongSavedLoopChanged;
            newSong.PropertyChanged += OnSelectedSongIsLoopActiveChanged;

            // Update loop editor candidates based on the newly selected song's saved loop
            // NOTE: This assumes the loop editor always shows the *selected* song's loop info,
            //       even if a different song is currently playing.
            //       If the loop editor should always show the *playing* song's info, this logic
            //       needs to be moved to the PlaybackService.CurrentSong changed handler.
            //       Let's stick to showing the selected song's info for now, as it's being edited.
            if (newSong.SavedLoop != null)
            {
                NewLoopStartCandidate = newSong.SavedLoop.Start;
                NewLoopEndCandidate = newSong.SavedLoop.End;
                // Do NOT update IsCurrentLoopActiveUiBinding here. That binding should reflect the *playing* song's active state.
            }
            else
            {
                ClearLoopCandidateInputs();
            }

            // The Play/Pause button state and loop active toggle state should reflect the PlaybackService.CurrentSong
            // The loop editor candidates should reflect the SelectedSong
        }
        else // newSong is null (e.g. filtering removed the song)
        {
            // When selection becomes null, clear the loop editor candidates
            ClearLoopCandidateInputs();
        }

        // Update command CanExecute states that depend on SelectedSong
        (PlaySelectedSongCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Visibility might depend on selection
    }

    /// <summary>
    /// Handles PropertyChanged events from the *PlaybackService*.
    /// This is where we sync UI elements that reflect the *currently playing* song.
    /// </summary>
    private void OnPlaybackServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        // Use InvokeAsync to ensure UI updates happen on the UI thread
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (args.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    var currentServiceSong = PlaybackService.CurrentSong;
                    Debug.WriteLine($"[MainVM_PSPChanged] Service.CurrentSong is now: {currentServiceSong?.Title ?? "null"}");

                    // When the *playing* song changes (or stops), update loop active toggle state and commands
                    if (currentServiceSong != null)
                    {
                        // Subscribe to the NEW playing song's property changes if it's not the same as the selected one we already subscribe to
                        // (Although we subscribe to SelectedSong changes, PlaybackService.CurrentSong might be different or become null).
                        // Let's ensure we are ONLY subscribed to the *playing* song's loop/active state for the UI binding.
                        // Unsubscribe from *any* previous song that was being tracked for the *playing* state.
                        // This requires a field to track the previously *playing* song for subscription management.

                        // Let's simplify for now and rely on the fact that OnCurrentSongSavedLoopChanged / OnCurrentSongIsLoopActiveChanged
                        // check if sender is PlaybackService.CurrentSong.
                        // However, we need to update the UI binding state based on the NEW playing song.
                        IsCurrentLoopActiveUiBinding = currentServiceSong.IsLoopActive;

                        // Load waveform for the new playing song if advanced panel is visible
                        if (IsAdvancedPanelVisible)
                        {
                            _ = LoadWaveformForCurrentSong();
                        }
                    }
                    else
                    {
                        // Playback stopped and no new song is current
                        IsCurrentLoopActiveUiBinding = false;
                        // Also clear waveform if song stopped
                        WaveformRenderData.Clear();
                        OnPropertyChanged(nameof(WaveformRenderData));
                        IsWaveformLoading = false;
                    }

                    // Update general UI states that depend on whether a song is playing
                    UpdateAllUIDependentStates();
                    OnPropertyChanged(nameof(SliderPositionSeconds)); // Update slider when song changes (resets to 0)
                    break;

                case nameof(PlaybackService.IsPlaying):
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    // Update UI elements that reflect playback state (Play/Pause button content, status bar)
                    UpdateStatusBarText();
                    RaiseAllCommandsCanExecuteChanged(); // Commands like CaptureLoop depend on playback status
                    break;

                case nameof(PlaybackService.CurrentPosition):
                    // Update UI elements that reflect playback position (slider value)
                    OnPropertyChanged(nameof(SliderPositionSeconds)); // Update slider when position changes
                    OnPropertyChanged(nameof(NewLoopStartCandidateDisplay)); // May need re-evaluation if setting points by current time
                    OnPropertyChanged(nameof(NewLoopEndCandidateDisplay)); // May need re-evaluation
                    OnPropertyChanged(nameof(CanSaveLoopRegion)); // CanSaveLoopRegion depends on current position vs candidates
                    (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Position changed
                    (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Position changed
                    break;

                case nameof(PlaybackService.CurrentSongDuration):
                    // Update UI elements that reflect song duration (slider max value, etc.)
                    OnPropertyChanged(nameof(SliderPositionSeconds)); // Max value might have changed
                    OnPropertyChanged(nameof(CanSaveLoopRegion)); // CanSaveLoopRegion depends on duration vs candidates
                    break;
            }
        });
    }

    /// <summary>
    /// Handles PropertyChanged events from the *PlaybackService.CurrentSong*.
    /// Specifically for SavedLoop property changes.
    /// </summary>
    private void OnCurrentSongSavedLoopChanged(object? sender, PropertyChangedEventArgs e)
    {
        // This handler is now attached to PlaybackService.CurrentSong, not SelectedSong.
        if (e.PropertyName == nameof(Song.SavedLoop) && sender is Song song && song == PlaybackService.CurrentSong)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Debug.WriteLine($"[MainVM_PlayingSongPChanged] SavedLoop changed for playing song: {song.Title}. Active: {song.IsLoopActive}");
                // Update UI elements that display the *playing* song's loop state.
                UpdateActiveLoopDisplayText();
                UpdateStatusBarText();

                // If a loop was just set on the playing song, auto-activate it unless it was already inactive.
                // If a loop was cleared, explicitly set IsLoopActive to false.
                if (song.SavedLoop != null && !song.IsLoopActive)
                {
                    song.IsLoopActive = true; // This will trigger persistence via OnCurrentSongIsLoopActiveChanged
                }
                else if (song.SavedLoop == null && song.IsLoopActive)
                {
                    song.IsLoopActive = false; // This will trigger persistence
                }

                // Sync the UI binding property to the playing song's state
                IsCurrentLoopActiveUiBinding = song.IsLoopActive;

                // Re-evaluate commands that depend on the playing song having a loop (ToggleLoop, ClearLoop)
                (ToggleLoopActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
        }
    }

    /// <summary>
    /// Handles PropertyChanged events from the *PlaybackService.CurrentSong*.
    /// Specifically for IsLoopActive property changes.
    /// </summary>
    private void OnCurrentSongIsLoopActiveChanged(object? sender, PropertyChangedEventArgs e)
    {
        // This handler is now attached to PlaybackService.CurrentSong, not SelectedSong.
        if (e.PropertyName == nameof(Song.IsLoopActive) && sender is Song song && song == PlaybackService.CurrentSong)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Debug.WriteLine($"[MainVM_PlayingSongPChanged] IsLoopActive changed to {song.IsLoopActive} for playing song: {song.Title}. Persisting.");
                if (song.SavedLoop != null)
                {
                    // Persist the active state change
                    _loopDataService.UpdateLoopActiveState(song.FilePath, song.IsLoopActive);
                }
                // Sync the UI binding property to the playing song's state
                if (_isCurrentLoopActiveUiBinding != song.IsLoopActive)
                {
                    IsCurrentLoopActiveUiBinding = song.IsLoopActive;
                }
                // Update UI elements that display the *playing* song's loop state.
                UpdateActiveLoopDisplayText();
                UpdateStatusBarText();
            });
        }
    }

    /// <summary>
    /// Handles PropertyChanged events from the *SelectedSong* (in the UI list).
    /// Specifically for SavedLoop property changes.
    /// This is separate from the handler for the *playing* song.
    /// </summary>
    private void OnSelectedSongSavedLoopChanged(object? sender, PropertyChangedEventArgs e)
    {
        // This handler is attached to the Song that is *selected* in the ListBox.
        // We only care if the change is to its SavedLoop property.
        if (e.PropertyName == nameof(Song.SavedLoop) && sender is Song song && song == SelectedSong)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Debug.WriteLine($"[MainVM_SelectedSongPChanged] SavedLoop changed for selected song: {song.Title}");
                // If the selected song is also the playing song, the OnCurrentSongSavedLoopChanged handler will deal with persistence and loop active state.
                // If the selected song is NOT the playing song, we should still persist its loop definition change.
                if (song.SavedLoop != null)
                {
                    // Persist the loop definition for the selected song, preserving its *current* active state in storage.
                    // Note: This won't affect the *playing* song's active state if they are different.
                    var storedData = _loopDataService.GetLoop(song.FilePath);
                    bool isActiveInStorage = storedData?.IsActive ?? false; // Get the active state currently in storage
                    _loopDataService.SetLoop(song.FilePath, song.SavedLoop.Start, song.SavedLoop.End, isActiveInStorage);
                    Debug.WriteLine($"[MainVM_SelectedSongPChanged] Persisted loop for selected song {song.Title} (Start: {song.SavedLoop.Start}, End: {song.SavedLoop.End}, Active in storage: {isActiveInStorage})");
                }
                else // Loop was cleared on the selected song
                {
                    _loopDataService.ClearLoop(song.FilePath);
                    Debug.WriteLine($"[MainVM_SelectedSongPChanged] Cleared loop for selected song {song.Title}");
                }

                // If the selected song *is* the currently playing song, update UI elements that depend on the playing song's loop state.
                if (song == PlaybackService.CurrentSong)
                {
                    UpdateActiveLoopDisplayText(); // Updates the loop text below the slider
                    (ToggleLoopActiveCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Can only toggle if playing song has a loop
                    (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Can only clear if playing song has a loop
                }

                // Always update CanSaveLoopRegion as it depends on NewLoopStart/EndCandidates, which are tied to SelectedSong.
                OnPropertyChanged(nameof(CanSaveLoopRegion));

                // Update loop editor candidates if the change originated from the selected song
                if (song == SelectedSong) // This check might be redundant due to the handler being on SelectedSong
                {
                    if (song.SavedLoop != null)
                    {
                        NewLoopStartCandidate = song.SavedLoop.Start;
                        NewLoopEndCandidate = song.SavedLoop.End;
                    }
                    else
                    {
                        ClearLoopCandidateInputs();
                    }
                }
            });
        }
    }

    /// <summary>
    /// Handles PropertyChanged events from the *SelectedSong* (in the UI list).
    /// Specifically for IsLoopActive property changes.
    /// This handler mostly exists for completeness; the primary logic should flow from
    /// changes to PlaybackService.CurrentSong.IsLoopActive.
    /// </summary>
    private void OnSelectedSongIsLoopActiveChanged(object? sender, PropertyChangedEventArgs e)
    {
        // This handler is attached to the Song that is *selected* in the ListBox.
        // We only care if the change is to its IsLoopActive property.
        if (e.PropertyName == nameof(Song.IsLoopActive) && sender is Song song && song == SelectedSong)
        {
            // If the selected song is ALSO the playing song, the OnCurrentSongIsLoopActiveChanged handler will handle persistence and UI sync.
            // If the selected song is NOT the playing song, changing its IsLoopActive property via the model setter
            // will trigger persistence via its own setter logic. We just need to ensure the UI binding for IsLoopActiveUiBinding
            // correctly reflects the *playing* song, not the selected one.
            Debug.WriteLine($"[MainVM_SelectedSongPChanged] IsLoopActive changed to {song.IsLoopActive} for selected song: {song.Title}.");
            if (song != PlaybackService.CurrentSong)
            {
                // Persistence is handled in the Song model's IsLoopActive setter.
                // No need to update IsCurrentLoopActiveUiBinding as it's bound to PlaybackService.CurrentSong state.
                // Update status bar if needed, but mainly reflects the playing song.
            }
            else
            {
                // This case is primarily handled by OnCurrentSongIsLoopActiveChanged.
                // Ensure the UI binding for IsCurrentLoopActiveUiBinding is synced.
                if (_isCurrentLoopActiveUiBinding != song.IsLoopActive)
                {
                    IsCurrentLoopActiveUiBinding = song.IsLoopActive;
                }
            }
        }
    }


    private void ToggleCurrentSongLoopActive(object? parameter)
    {
        if (PlaybackService.CurrentSong != null && PlaybackService.CurrentSong.SavedLoop != null)
        {
            // Modifying the model property triggers its setter, which triggers OnCurrentSongIsLoopActiveChanged,
            // which then updates the IsCurrentLoopActiveUiBinding and persists the state.
            PlaybackService.CurrentSong.IsLoopActive = !PlaybackService.CurrentSong.IsLoopActive;
        }
    }

    private void OnArtistSelected(ArtistViewModel artist)
    {
        if (artist?.Name == null) return;
        Debug.WriteLine($"[MainVM] Artist selected: {artist.Name}");
        // Setting the SearchQuery triggers ApplyFilter()
        SearchQuery = artist.Name;
        // Switch to Library tab to show filtered results
        ActiveTabIndex = 0;
    }

    private void OnAlbumSelected(AlbumViewModel album)
    {
        if (album?.Title == null || album.Artist == null) return;
        Debug.WriteLine($"[MainVM] Album selected: {album.Title} by {album.Artist}");

        // Clear the search query as we are applying an album/artist filter directly
        SearchQuery = string.Empty;

        FilteredSongs.Clear();
        // Find songs matching the selected album and artist (case-insensitive)
        var songsInAlbum = _allSongs.Where(s =>
            (s.Album?.Equals(album.Title, StringComparison.OrdinalIgnoreCase) ?? false) &&
            (s.Artist?.Equals(album.Artist, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(s => s.Title); // Order songs within the album by title

        foreach (var song in songsInAlbum)
        {
            FilteredSongs.Add(song);
        }
        UpdateStatusBarText();
        ActiveTabIndex = 0; // Switch to Library tab
    }


    private void ApplyFilter()
    {
        Debug.WriteLine($"[MainVM] Applying filter: '{SearchQuery}'");
        FilteredSongs.Clear();
        var songsToFilter = _allSongs.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant().Trim();
            songsToFilter = songsToFilter.Where(s =>
                (s.Title?.ToLowerInvariant().Contains(query) ?? false) ||
                (s.Artist?.ToLowerInvariant().Contains(query) ?? false) ||
                (s.Album?.ToLowerInvariant().Contains(query) ?? false));
        }

        // Order the filtered results by artist, then album, then title for consistency
        var orderedSongs = songsToFilter.OrderBy(s => s.Artist).ThenBy(s => s.Album).ThenBy(s => s.Title);

        foreach (var song in orderedSongs)
        {
            FilteredSongs.Add(song);
        }

        // If the currently selected song is no longer in the filtered list, clear selection.
        // This is what causes SelectedSong to become null when filtering.
        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            Debug.WriteLine($"[MainVM] Selected song '{SelectedSong.Title}' filtered out. Clearing selection.");
            SelectedSong = null; // This will trigger HandleSelectedSongChange(oldSong, null)
        }

        UpdateStatusBarText();
        Debug.WriteLine($"[MainVM] Filtered songs count: {FilteredSongs.Count}");
    }

    private async Task LoadWaveformForCurrentSong()
    {
        var songToLoadWaveformFor = PlaybackService.CurrentSong;
        if (songToLoadWaveformFor == null || string.IsNullOrEmpty(songToLoadWaveformFor.FilePath))
        {
            Debug.WriteLine("[MainVM] LoadWaveformForCurrentSong: No current song or path empty. Clearing waveform.");
            WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData)); IsWaveformLoading = false; return;
        }

        // Avoid re-loading if the waveform is already loaded for this song or is currently loading
        // (This check assumes WaveformService cache is effective)
        if (_waveformService.IsWaveformCached(songToLoadWaveformFor.FilePath)) // Assuming WaveformService has a cache check method
        {
            Debug.WriteLine($"[MainVM] Waveform already cached for: {songToLoadWaveformFor.Title}. Using cached data.");
            // Update the UI from cache if needed? No, WaveformService.GetWaveformAsync handles cache lookup.
            // We still proceed to call GetWaveformAsync which is designed to be fast if cached.
        }
        else if (IsWaveformLoading)
        {
            Debug.WriteLine($"[MainVM] Waveform is already loading. Skipping request for: {songToLoadWaveformFor.Title}");
            return; // Don't start another loading task
        }


        IsWaveformLoading = true;
        WaveformRenderData.Clear(); // Clear old data immediately
        OnPropertyChanged(nameof(WaveformRenderData)); // Notify UI to clear


        try
        {
            Debug.WriteLine($"[MainVM] Requesting waveform for: {songToLoadWaveformFor.Title}");
            // Use a reasonable number of points; 1000 is often sufficient
            var points = await _waveformService.GetWaveformAsync(songToLoadWaveformFor.FilePath, 1000);

            // Important: Check if the song is *still* the current song after the async operation completes.
            // This prevents loading/displaying a waveform for a song that's no longer playing.
            if (PlaybackService.CurrentSong == songToLoadWaveformFor)
            {
                Debug.WriteLine($"[MainVM] Waveform loaded for: {songToLoadWaveformFor.Title}, {points.Count} points. Updating UI.");
                WaveformRenderData.Clear(); // Clear again just in case the UI wasn't responsive earlier
                foreach (var p in points)
                {
                    WaveformRenderData.Add(p);
                }
                OnPropertyChanged(nameof(WaveformRenderData)); // Notify UI of the new data
            }
            else
            {
                Debug.WriteLine($"[MainVM] Waveform for {songToLoadWaveformFor.Title} loaded, but current song is now {PlaybackService.CurrentSong?.Title ?? "null"}. Discarding.");
                WaveformRenderData.Clear(); // Ensure cleared if the song changed mid-load
                OnPropertyChanged(nameof(WaveformRenderData));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainVM] Failed to load waveform for {songToLoadWaveformFor.Title}: {ex.Message}");
            WaveformRenderData.Clear(); // Clear on error
            OnPropertyChanged(nameof(WaveformRenderData));
            // Optionally set status text to indicate error
            StatusBarText = $"Error loading waveform for {songToLoadWaveformFor.Title}";
        }
        finally
        {
            IsWaveformLoading = false; // Always set loading flag to false in finally
        }
    }

    /// <summary>
    /// Updates the loop editor UI elements (candidate points) to reflect the *selected* song's saved loop.
    /// Does NOT affect the *playing* song's active loop state or display text below the slider.
    /// </summary>
    private void UpdateLoopEditorForCurrentSong()
    {
        // Update candidates based on the SELECTED song
        var songForLoopEditor = SelectedSong; // Use SelectedSong here

        if (songForLoopEditor?.SavedLoop != null)
        {
            NewLoopStartCandidate = songForLoopEditor.SavedLoop.Start;
            NewLoopEndCandidate = songForLoopEditor.SavedLoop.End;
            // Note: IsCurrentLoopActiveUiBinding is NOT updated here.
            // It is ONLY updated when PlaybackService.CurrentSong changes or its IsLoopActive property changes.
        }
        else
        {
            // If no song is selected, or selected song has no loop, clear candidates
            ClearLoopCandidateInputs();
            // Note: IsCurrentLoopActiveUiBinding remains unchanged, reflecting the playing song's state.
        }
        OnPropertyChanged(nameof(CanSaveLoopRegion));
        // Commands related to loop editing might need CanExecuteChanged raised here as they depend on SelectedSong or Candidates
        (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // Clear/Toggle loop commands depend on PlaybackService.CurrentSong having a loop, handled in OnPlaybackServicePropertyChanged/OnCurrentSongSavedLoopChanged
    }


    private void ClearLoopCandidateInputs() { NewLoopStartCandidate = null; NewLoopEndCandidate = null; OnPropertyChanged(nameof(CanSaveLoopRegion)); /* CanSaveLoopRegion will update, and its command */ }

    // Can save a loop if there's a SELECTED song, candidates are set, end > start, and within song bounds.
    public bool CanSaveLoopRegion => SelectedSong != null && NewLoopStartCandidate.HasValue && NewLoopEndCandidate.HasValue && NewLoopEndCandidate.Value > NewLoopStartCandidate.Value && NewLoopEndCandidate.Value <= SelectedSong.Duration && NewLoopStartCandidate.Value >= TimeSpan.Zero;


    private void SaveLoopAction(object? param)
    {
        var selected = SelectedSong; // Use SelectedSong for the loop definition source
        if (!CanSaveLoopRegion || selected == null || !NewLoopStartCandidate.HasValue || !NewLoopEndCandidate.HasValue) return;

        // Create a new loop definition based on the candidates
        var newLoop = new LoopRegion(NewLoopStartCandidate.Value, NewLoopEndCandidate.Value, "User Loop");

        // Determine the active state for the *storage*. Default to active if new, or preserve current state in storage if updating.
        var storedData = _loopDataService.GetLoop(selected.FilePath);
        bool isActiveInStorage = storedData?.IsActive ?? false; // Get the active state currently in storage

        // Update the SavedLoop property on the SELECTED song model.
        // This will trigger OnSelectedSongSavedLoopChanged, which will persist the *new loop definition*
        // using the isActiveInStorage determined above.
        selected.SavedLoop = newLoop;

        // Now, if the selected song *is* the currently playing song, also update its active state property
        // if necessary, to match the desired state (e.g., should it become active now that a loop is defined?).
        // The UI binding IsCurrentLoopActiveUiBinding reflects the *playing* song.
        // If the selected song is playing, update its IsLoopActive to match the desired state (often active when saved).
        if (selected == PlaybackService.CurrentSong)
        {
            bool shouldBeActiveInUI = true; // Typically, saving a loop makes it active for playback.
            if (PlaybackService.CurrentSong.IsLoopActive != shouldBeActiveInUI)
            {
                // Setting IsLoopActive on the playing song model will trigger OnCurrentSongIsLoopActiveChanged,
                // which updates IsCurrentLoopActiveUiBinding and persists the *active state* change.
                PlaybackService.CurrentSong.IsLoopActive = shouldBeActiveInUI;
            }
            // If IsLoopActive didn't change (e.g., was already true), the OnCurrentSongSavedLoopChanged handler
            // already handled persisting the new definition with the existing active state.
            UpdateActiveLoopDisplayText(); // Ensure the display text below the slider updates
        }
        else
        {
            // If the selected song is NOT the playing song, we only updated its SavedLoop property.
            // Its IsLoopActive state (which is stored but not used for UI binding unless it becomes the playing song)
            // was preserved based on isActiveInStorage. No UI state change needed for playback controls.
        }

        Debug.WriteLine($"[MainVM] Loop saved for selected song {selected.Title}. Definition: {newLoop.Start} - {newLoop.End}. Active in storage: {_loopDataService.GetLoop(selected.FilePath)?.IsActive ?? false}");
        UpdateLoopEditorForCurrentSong(); // Refresh editor state (e.g. CanExecute for Clear)
        StatusBarText = $"Loop saved for {selected.Title}.";
    }

    private void ClearSavedLoopAction(object? param)
    {
        var selected = SelectedSong; // Use SelectedSong for the loop definition to clear
        if (selected != null)
        {
            var filePath = selected.FilePath;

            // Clear the SavedLoop property on the SELECTED song model.
            // This will trigger OnSelectedSongSavedLoopChanged, which will clear the loop data from storage.
            selected.SavedLoop = null;

            // If the selected song *is* the currently playing song, also ensure its IsLoopActive state is false
            // and sync the UI binding.
            if (selected == PlaybackService.CurrentSong)
            {
                if (PlaybackService.CurrentSong.IsLoopActive)
                {
                    // Setting IsLoopActive to false on the playing song model triggers its handler,
                    // which updates IsCurrentLoopActiveUiBinding and persists the active state change.
                    PlaybackService.CurrentSong.IsLoopActive = false;
                }
                UpdateActiveLoopDisplayText(); // Ensure the display text below the slider updates
                (ToggleLoopActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }

            // Clear the loop editor candidates now that the loop is gone
            ClearLoopCandidateInputs();
            UpdateLoopEditorForCurrentSong(); // Refresh CanExecute for Save/Clear
            StatusBarText = $"Loop cleared for {selected.Title}.";
        }
    }

    /// <summary>
    /// Updates the text displayed below the slider to show the *playing* song's active loop status.
    /// </summary>
    private void UpdateActiveLoopDisplayText()
    {
        var currentServiceSong = PlaybackService.CurrentSong; // Use playing song here
        if (currentServiceSong?.SavedLoop != null)
        {
            var loop = currentServiceSong.SavedLoop;
            string activeStatus = currentServiceSong.IsLoopActive ? " (Active)" : " (Inactive)";
            ActiveLoopDisplayText = $"Loop: {loop.Start:mm\\:ss\\.f} - {loop.End:mm\\:ss\\.f}{activeStatus}";
        }
        else
        {
            ActiveLoopDisplayText = "No loop defined.";
        }
    }

    /// <summary>
    /// Updates the status bar text based on the current state (loading, playing, paused, stopped, library count).
    /// Reflects the state of the PlaybackService.CurrentSong if one is playing.
    /// </summary>
    private void UpdateStatusBarText()
    {
        if (IsLoadingLibrary) return; // Don't update if loading
        string status;
        var currentServiceSong = PlaybackService.CurrentSong; // Use playing song here

        if (currentServiceSong != null)
        {
            string stateStr = PlaybackService.CurrentPlaybackStatus switch
            {
                PlaybackStateStatus.Playing => "Playing",
                PlaybackStateStatus.Paused => "Paused",
                PlaybackStateStatus.Stopped => "Stopped",
                _ => "Idle" // Should not happen if CurrentSong is not null, but fallback
            };
            status = $"{stateStr}: {currentServiceSong.Title}";
            if (currentServiceSong.SavedLoop != null && currentServiceSong.IsLoopActive)
            {
                status += $" (Loop Active)";
            }
        }
        else // No song is currently playing
        {
            // Determine status based on library state and filter
            int allCount = _allSongs.Count;
            int filteredCount = FilteredSongs.Count;

            if (_isLoadingLibrary)
            {
                status = StatusBarText; // Keep the loading status set by the loader
            }
            else if (allCount == 0)
            {
                var settings = _settingsService.LoadSettings();
                if (!settings.MusicDirectories.Any())
                {
                    status = "Sonorize - Library empty. Add directories via File menu.";
                }
                else
                {
                    status = "Sonorize - No songs found in configured directories.";
                }
            }
            else if (!string.IsNullOrWhiteSpace(SearchQuery) && filteredCount == 0)
            {
                status = $"No matches found for '{SearchQuery}'. Showing 0 of {allCount} songs.";
            }
            else if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                status = $"Showing {filteredCount} of {allCount} songs (Filtered).";
            }
            else // No search query, all songs or only initial scan results shown
            {
                status = $"Sonorize - {allCount} songs loaded."; // Or just "Ready"?
            }
        }
        // Only update if the text is different to avoid flickering/spamming
        if (StatusBarText != status)
        {
            StatusBarText = status;
        }
    }


    private async Task LoadMusicLibrary()
    {
        if (IsLoadingLibrary) return;
        // Hide advanced panel during library scan as playback isn't stable
        IsAdvancedPanelVisible = false;

        // Stop any currently playing song before clearing the library
        PlaybackService.Stop(); // This will also nullify PlaybackService.CurrentSong and trigger related UI updates

        IsLoadingLibrary = true;
        SearchQuery = string.Empty; // Clear search query when reloading library

        // Clear existing collections on the UI thread
        await Dispatcher.UIThread.InvokeAsync(() => {
            SelectedSong = null; // Clear selected song first
            _allSongs.Clear();
            Artists.Clear();
            Albums.Clear();
            FilteredSongs.Clear(); // ApplyFilter will repopulate this later
            // Clear waveform display immediately
            WaveformRenderData.Clear();
            OnPropertyChanged(nameof(WaveformRenderData));
            IsWaveformLoading = false; // Ensure loading flag is reset
            StatusBarText = "Preparing to load music...";
            UpdateAllUIDependentStates(); // Update CanExecute, status, etc.
        });

        var settings = _settingsService.LoadSettings();

        if (!settings.MusicDirectories.Any())
        {
            Debug.WriteLine("[MainVM] No music directories configured. Skipping library scan.");
            IsLoadingLibrary = false;
            UpdateStatusBarText(); // Update status to indicate no directories
            return;
        }

        try
        {
            Debug.WriteLine("[MainVM] Starting background music library scan...");
            // Load songs in the background
            await Task.Run(async () => {
                await _musicLibraryService.LoadMusicFromDirectoriesAsync(
                    settings.MusicDirectories,
                    // Callback for when a song is found - add to the master list
                    song => Dispatcher.UIThread.InvokeAsync(() => _allSongs.Add(song)),
                    // Callback for status updates during scan
                    s => Dispatcher.UIThread.InvokeAsync(() => StatusBarText = s)
                );
            });
            Debug.WriteLine($"[MainVM] Music library scan complete. Found {_allSongs.Count} songs.");

            // Update UI collections (Artists, Albums, FilteredSongs) on the UI thread after scan
            await Dispatcher.UIThread.InvokeAsync(() => {
                // Rebuild Artists collection
                Artists.Clear();
                var uniqueArtistNames = _allSongs.Select(s => s.Artist).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
                Bitmap? defaultThumb = _musicLibraryService.GetDefaultThumbnail();
                foreach (var artistName in uniqueArtistNames!) // '!' because Where(a => !string.IsNullOrWhiteSpace(a)) makes them non-null
                {
                    // Find a song by this artist with a thumbnail, otherwise use default
                    Bitmap? repThumb = _allSongs.FirstOrDefault(s => (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false) && s.Thumbnail != null)?.Thumbnail ?? defaultThumb;
                    Artists.Add(new ArtistViewModel { Name = artistName, Thumbnail = repThumb });
                }
                OnPropertyChanged(nameof(Artists));

                // Rebuild Albums collection
                Albums.Clear();
                // Use a tuple of (Album, Artist) as the key for grouping unique albums
                Func<Song, (string Album, string Artist)> keySelector = s => (s.Album?.Trim() ?? string.Empty, s.Artist?.Trim() ?? string.Empty);
                // Filter out entries with empty album/artist before grouping
                var uniqueAlbums = _allSongs.Where(s => !string.IsNullOrWhiteSpace(s.Album) && !string.IsNullOrWhiteSpace(s.Artist))
                                            .GroupBy(keySelector, AlbumArtistTupleComparer.Instance) // Use custom comparer for case-insensitivity
                                            .Select(g => new { AlbumTitle = g.Key.Album, ArtistName = g.Key.Artist, ThumbSong = g.FirstOrDefault(s => s.Thumbnail != null) }) // Select first song with thumbnail in the group
                                            .OrderBy(a => a.ArtistName, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.AlbumTitle, StringComparer.OrdinalIgnoreCase)
                                            .ToList();
                foreach (var albumData in uniqueAlbums)
                {
                    Albums.Add(new AlbumViewModel { Title = albumData.AlbumTitle, Artist = albumData.ArtistName, Thumbnail = albumData.ThumbSong?.Thumbnail ?? defaultThumb });
                }
                OnPropertyChanged(nameof(Albums));

                // Apply the current filter (which is empty after reset) to populate FilteredSongs
                ApplyFilter();

                // Clear waveform cache as audio files might have changed
                _waveformService.ClearCache();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainVM] CRITICAL Error loading library: {ex}");
            await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = $"Error loading music library: {ex.Message}");
            // Clear collections even on error to reflect empty state
            await Dispatcher.UIThread.InvokeAsync(() => {
                _allSongs.Clear(); Artists.Clear(); Albums.Clear(); FilteredSongs.Clear();
                OnPropertyChanged(nameof(Artists)); OnPropertyChanged(nameof(Albums));
                ApplyFilter(); // Re-apply filter to ensure FilteredSongs is empty
            });
        }
        finally
        {
            // Ensure loading state is reset and status bar is updated appropriately
            IsLoadingLibrary = false;
            UpdateStatusBarText(); // Update status after loading finishes, reflecting final counts.
        }
    }


    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        // Ensure the owner is a Window and we aren't already busy loading
        if (ownerWindow is not Window owner || IsLoadingLibrary) return;

        // Hide advanced panel when dialog opens
        IsAdvancedPanelVisible = false;

        // Stop playback while settings are changed (especially directories)
        PlaybackService.Stop();

        // Load current settings to compare after dialog closes
        var currentSettingsBeforeDialog = _settingsService.LoadSettings();

        // Create and show the settings dialog
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };

        // Show the dialog modally
        await settingsDialog.ShowDialog(owner);

        // Check if settings were marked as changed by the dialog ViewModel
        if (settingsVM.SettingsChanged)
        {
            // Re-load settings from the file to get the state that was saved by the dialog
            var newSettingsAfterDialog = _settingsService.LoadSettings();

            // Check if directories actually changed
            bool dirsActuallyChanged = !currentSettingsBeforeDialog.MusicDirectories.SequenceEqual(newSettingsAfterDialog.MusicDirectories);

            // Check if the preferred theme actually changed
            bool themeActuallyChanged = currentSettingsBeforeDialog.PreferredThemeFileName != newSettingsAfterDialog.PreferredThemeFileName;

            // If directories changed, reload the music library
            if (dirsActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Music directories changed in settings. Reloading library.");
                await LoadMusicLibrary();
            }

            // If theme changed, update status bar and indicate restart needed
            if (themeActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Preferred theme changed in settings.");
                // We don't apply the theme change immediately here as it affects Avalonia's core styles.
                // A restart is genuinely required for theme changes applied via the main App.cs logic.
                StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect.";
            }
            else if (!dirsActuallyChanged)
            {
                // If settingsChanged was true but neither dirs nor theme changed,
                // maybe some other setting changed, or it was marked true unnecessarily.
                // If no reload/restart action is taken, reset the status bar.
                UpdateStatusBarText();
            }
        }
        else
        {
            Debug.WriteLine("[MainVM] Settings dialog closed, no changes flagged by ViewModel.");
            // If no changes were flagged, restore previous status or default.
            UpdateStatusBarText();
        }
    }


    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false; // Hide panel

        // Use Avalonia's StorageProvider for folder picking
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Music Directory",
            AllowMultiple = false // Allow selecting only one folder at a time
        });

        // Process the result
        if (result != null && result.Count > 0)
        {
            // Get the path from the first selected folder
            // Try LocalPath first, then fall back to parsing the URI if needed
            string? folderPath = result[0].Path.LocalPath;

            // Fallback for systems/providers where LocalPath might be null or incorrect
            if (string.IsNullOrEmpty(folderPath) && result[0].Path.IsAbsoluteUri)
            {
                try
                {
                    // Attempt to convert the URI to a local path string
                    folderPath = new Uri(result[0].Path.ToString()).LocalPath;
                }
                catch (Exception ex)
                {
                    // Log any errors during URI conversion
                    Debug.WriteLine($"[MainVM] Error converting folder URI to local path: {result[0].Path}. Exception: {ex.Message}");
                    folderPath = null; // Ensure folderPath is null if conversion fails
                }
            }

            // If we have a valid folder path
            if (!string.IsNullOrEmpty(folderPath))
            {
                Debug.WriteLine($"[MainVM] Selected music directory: {folderPath}");
                // Load current settings
                var settings = _settingsService.LoadSettings();

                // Check if the directory is already in the list
                if (!settings.MusicDirectories.Contains(folderPath))
                {
                    // Add the new directory, save settings, and reload the library
                    settings.MusicDirectories.Add(folderPath);
                    _settingsService.SaveSettings(settings);
                    Debug.WriteLine($"[MainVM] Directory added to settings. Reloading library.");
                    await LoadMusicLibrary();
                }
                else
                {
                    // Directory already exists, maybe just inform the user via status bar?
                    Debug.WriteLine($"[MainVM] Directory '{folderPath}' is already in the list. Not adding.");
                    StatusBarText = $"Directory '{System.IO.Path.GetFileName(folderPath)}' already in list.";
                }
            }
            else
            {
                // If folderPath is still null/empty, inform the user
                Debug.WriteLine($"[MainVM] Could not get a valid path from the selected folder.");
                StatusBarText = "Could not get valid path for selected folder.";
            }
        }
        else
        {
            Debug.WriteLine("[MainVM] Folder picker dialog cancelled or returned no result.");
            // User cancelled the dialog, restore status bar
            UpdateStatusBarText();
        }
    }
}
