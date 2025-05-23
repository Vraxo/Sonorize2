using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sonorize.ViewModels;

public class PlaybackViewModel : ViewModelBase
{
    public PlaybackService PlaybackService { get; } // Keep reference to the service
    private readonly WaveformService _waveformService; // Need waveform service here

    // Properties related to playback state, directly from service or derived
    // Public getter for PlaybackService.CurrentSong property
    public Song? CurrentSong => PlaybackService.CurrentSong;
    public bool HasCurrentSong => PlaybackService.CurrentSong != null;

    public TimeSpan CurrentPosition => PlaybackService.CurrentPosition;
    public double CurrentPositionSeconds
    {
        get => PlaybackService.CurrentPositionSeconds;
        set
        {
            // Check if the value actually changed and if a song is loaded
            if (PlaybackService.CurrentSong != null &&
                Math.Abs(PlaybackService.CurrentPositionSeconds - value) > 0.01) // Use a small tolerance for double comparison
            {
                Debug.WriteLine($"[PlaybackVM] CurrentPositionSeconds setter called with: {value}. Current PlaybackService PositionSeconds: {PlaybackService.CurrentPositionSeconds}. Seeking.");
                PlaybackService.Seek(TimeSpan.FromSeconds(value));
                // After PlaybackService.Seek, it will update its CurrentPosition,
                // which will fire PropertyChanged. This ViewModel's PlaybackService_PropertyChanged
                // handler will then update its own properties (including this one's getter value)
                // and notify the UI.
            }
            // If value is effectively the same, do nothing to prevent potential feedback loops or unnecessary seeks.
            // If no song is loaded, seeking is not possible/meaningful.
        }
    }

    public TimeSpan CurrentSongDuration => PlaybackService.CurrentSongDuration;
    public double CurrentSongDurationSeconds => PlaybackService.CurrentSongDurationSeconds;

    public PlaybackStateStatus CurrentPlaybackStatus => PlaybackService.CurrentPlaybackStatus;
    public bool IsPlaying => PlaybackService.IsPlaying;

    // Properties for playback controls (Speed/Pitch)
    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed { get => _playbackSpeed; set { value = Math.Clamp(value, 0.5, 2.0); if (SetProperty(ref _playbackSpeed, value)) { PlaybackService.PlaybackRate = (float)value; OnPropertyChanged(nameof(PlaybackSpeedDisplay)); } } }
    public string PlaybackSpeedDisplay => $"{PlaybackSpeed:F2}x";

    private double _playbackPitch = 0.0;
    public double PlaybackPitch { get => _playbackPitch; set { value = Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2.0; value = Math.Clamp(value, -4.0, 4.0); if (SetProperty(ref _playbackPitch, value)) { PlaybackService.PitchSemitones = (float)_playbackPitch; OnPropertyChanged(nameof(PlaybackPitchDisplay)); } } }
    public string PlaybackPitchDisplay => $"{PlaybackPitch:+0.0;-0.0;0} st";

    // Waveform data
    public ObservableCollection<WaveformPoint> WaveformRenderData { get; } = new();
    private bool _isWaveformLoading = false;
    // Changed setter to private to enforce internal state management
    public bool IsWaveformLoading { get => _isWaveformLoading; private set => SetProperty(ref _isWaveformLoading, value); }

    // Derived properties for UI display
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

    // Commands owned by PlaybackViewModel
    public ICommand PlayPauseResumeCommand { get; } // Renamed from simple Click handler
    public ICommand SeekCommand { get; } // Command for slider/waveform seeking

    // IsAdvancedPanelVisible and ToggleAdvancedPanelCommand remain in MainWindowViewModel
    // Loop commands remain in LoopEditorViewModel


    public PlaybackViewModel(PlaybackService playbackService, WaveformService waveformService)
    {
        PlaybackService = playbackService;
        _waveformService = waveformService;

        // Initialize playback controls
        PlaybackSpeed = 1.0;
        PlaybackPitch = 0.0;

        // Initialize commands
        PlayPauseResumeCommand = new RelayCommand(
            _ => TogglePlayPauseResume(),
            _ => PlaybackService.CurrentSong != null && !IsWaveformLoading); // Can't control playback while waveform is loading

        SeekCommand = new RelayCommand(
            positionSecondsObj => {
                if (positionSecondsObj is double seconds && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
                {
                    // This command is now less critical if the TwoWay binding on CurrentPositionSeconds works,
                    // but can be kept for other programmatic seek triggers if necessary.
                    PlaybackService.Seek(TimeSpan.FromSeconds(seconds));
                }
            },
             _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentSongDuration.TotalSeconds > 0 && !IsWaveformLoading);


        // Subscribe to PlaybackService property changes
        PlaybackService.PropertyChanged += PlaybackService_PropertyChanged;

        // Subscribe to PlaybackViewModel's own properties that affect command CanExecute
        PropertyChanged += PlaybackViewModel_PropertyChanged;
    }

    private void TogglePlayPauseResume()
    {
        Debug.WriteLine($"[PlaybackVM] TogglePlayPauseResume called. Current state: {PlaybackService.CurrentPlaybackStatus}");
        if (PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Playing)
        {
            PlaybackService.Pause();
        }
        else if (PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            PlaybackService.Resume();
        }
        else if (PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Stopped && PlaybackService.CurrentSong != null)
        {
            // If stopped but a song is loaded (e.g., after ending), resume playing
            PlaybackService.Resume(); // This calls Play(CurrentSong) internally
        }
        else if (PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Stopped && PlaybackService.CurrentSong == null)
        {
            // If stopped and no song is loaded, cannot play/resume.
            Debug.WriteLine("[PlaybackVM] TogglePlayPauseResume called in Stopped state with no song. Doing nothing.");
        }
    }


    private void PlaybackService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Propagate relevant PlaybackService changes to PlaybackViewModel properties
            switch (e.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    OnPropertyChanged(nameof(CurrentSong)); // Propagate the song itself
                    OnPropertyChanged(nameof(HasCurrentSong));
                    // Duration, Position, State will also change, let's handle those explicitly or rely on other cases
                    // Waveform needs to be loaded for the new song (handled outside this handler for now)

                    // If the song becomes null (e.g., after Stop), clear waveform data and loading state
                    if (PlaybackService.CurrentSong == null)
                    {
                        Debug.WriteLine("[PlaybackVM] PlaybackService.CurrentSong is null. Clearing waveform data.");
                        WaveformRenderData.Clear();
                        OnPropertyChanged(nameof(WaveformRenderData));
                        IsWaveformLoading = false; // Internal setter is fine
                    }

                    RaisePlaybackCommandCanExecuteChanged();
                    break;
                case nameof(PlaybackService.CurrentPosition):
                    OnPropertyChanged(nameof(CurrentPosition));
                    OnPropertyChanged(nameof(CurrentPositionSeconds)); // This will reflect the change from PlaybackService
                    OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay)); // Derived property
                    RaisePlaybackCommandCanExecuteChanged(); // Seek command might be affected
                    break;
                case nameof(PlaybackService.CurrentSongDuration):
                    OnPropertyChanged(nameof(CurrentSongDuration));
                    OnPropertyChanged(nameof(CurrentSongDurationSeconds));
                    OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay)); // Derived property
                    RaisePlaybackCommandCanExecuteChanged(); // Seek command might be affected
                    break;
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    OnPropertyChanged(nameof(CurrentPlaybackStatus));
                    OnPropertyChanged(nameof(IsPlaying)); // Derived from status
                    RaisePlaybackCommandCanExecuteChanged(); // Play/Pause/Resume command is affected
                    break;
                    // Speed and Pitch changes on the service are triggered by the VM setter,
                    // so no need to listen for them here.
            }
        });
    }

    private void PlaybackViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Listen to properties *on this ViewModel* that affect command CanExecute
        switch (e.PropertyName)
        {
            case nameof(IsWaveformLoading):
                RaisePlaybackCommandCanExecuteChanged(); // Play/Pause/Resume, Seek depend on this
                break;
                // Other properties like PlaybackSpeed/Pitch don't inherently affect command CanExecute
        }
    }


    /// <summary>
    /// Designed to be called by MainWindowViewModel when a new song is available (e.g., selected from library).
    /// This VM will manage playing it and loading its waveform.
    /// </summary>
    /// <param name="song">The song to play.</param>
    // Removed PlaySong method as PlaybackService.Play is called directly by MainVM now.
    // Waveform loading is also triggered by MainVM when PlaybackService.CurrentSong changes.


    /// <summary>
    /// Loads the waveform data for the currently playing song. Designed to be called by MainWindowViewModel.
    /// </summary>
    public async Task LoadWaveformForCurrentSongAsync()
    {
        var songToLoadWaveformFor = PlaybackService.CurrentSong;
        if (songToLoadWaveformFor == null || string.IsNullOrEmpty(songToLoadWaveformFor.FilePath))
        {
            Debug.WriteLine("[PlaybackVM] LoadWaveformForCurrentSongAsync skipped: No current song or invalid path.");
            // Clear existing waveform data if any (handled by PlaybackService_PropertyChanged)
            IsWaveformLoading = false;
            return;
        }

        // Clear existing waveform data immediately to show loading state
        // Note: PlaybackService_PropertyChanged(CurrentSong) handler might have already cleared this if a new song replaced null
        // but explicitly clearing here ensures state is reset before loading starts for a valid song change.
        WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData));
        IsWaveformLoading = true; // Internal setter is fine here

        try
        {
            Debug.WriteLine($"[PlaybackVM] Requesting waveform for: {songToLoadWaveformFor.Title}");
            // Target points should probably be based on control width or a fixed resolution
            // For a fixed 80px height control, 1000 points is likely sufficient detail.
            var points = await _waveformService.GetWaveformAsync(songToLoadWaveformFor.FilePath, 1000);

            // Check if the song is still the same after the async operation before updating the UI
            if (PlaybackService.CurrentSong == songToLoadWaveformFor)
            {
                Debug.WriteLine($"[PlaybackVM] Waveform loaded for: {songToLoadWaveformFor.Title}, {points.Count} points. Updating UI.");
                // Add points on the UI thread
                await Dispatcher.UIThread.InvokeAsync(() => {
                    foreach (var p in points) WaveformRenderData.Add(p);
                    OnPropertyChanged(nameof(WaveformRenderData)); // Notify UI
                });

            }
            else
            {
                // Song changed during waveform generation, discard the result for the old song.
                Debug.WriteLine($"[PlaybackVM] Waveform for {songToLoadWaveformFor.Title} loaded, but current song is now {PlaybackService.CurrentSong?.Title ?? "null"}. Discarding.");
                // No need to clear WaveformRenderData here; the handler for the new song or null song will handle it.
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackVM] CRITICAL Error loading waveform for {songToLoadWaveformFor.Title}: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() => {
                WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData));
                // Optionally set a status text indicating waveform load failed
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => {
                IsWaveformLoading = false; // Internal setter is fine here
            });
        }
    }

    /// <summary>
    /// Raises CanExecuteChanged for commands owned by this ViewModel.
    /// </summary>
    public void RaisePlaybackCommandCanExecuteChanged()
    {
        //Debug.WriteLine("[PlaybackVM] Raising playback command CanExecute changed.");
        (PlayPauseResumeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SeekCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}