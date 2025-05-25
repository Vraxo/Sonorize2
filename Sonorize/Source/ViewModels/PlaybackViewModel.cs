using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services; // This using directive makes PlaybackStateStatus from the Service available
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sonorize.ViewModels;

public enum RepeatMode { None, PlayOnce, RepeatOne, RepeatAll }

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
            // Use a small tolerance for double comparison
            if (PlaybackService.CurrentSong == null || Math.Abs(PlaybackService.CurrentPositionSeconds - value) <= 0.01)
            {
                return;
                // After PlaybackService.Seek, it will update its CurrentPosition,
                // which will fire PropertyChanged. This ViewModel's PlaybackService_PropertyChanged
                // handler will then update its own properties (including this one's getter value)
                // and notify the UI.
            }
            // Debug.WriteLine($"[PlaybackVM] CurrentPositionSeconds setter called with: {value}. Current PlaybackService PositionSeconds: {PlaybackService.CurrentPositionSeconds}. Seeking.");
            PlaybackService.Seek(TimeSpan.FromSeconds(value));
            // If value is effectively the same, do nothing to prevent potential feedback loops or unnecessary seeks.
            // If no song is loaded, seeking is not possible/meaningful.
        }
    }

    public TimeSpan CurrentSongDuration => PlaybackService.CurrentSongDuration;
    public double CurrentSongDurationSeconds => PlaybackService.CurrentSongDurationSeconds;

    // Use PlaybackStateStatus from Sonorize.Services via the using directive
    public PlaybackStateStatus CurrentPlaybackStatus => PlaybackService.CurrentPlaybackStatus;
    public bool IsPlaying => PlaybackService.IsPlaying;

    // Properties for playback controls (Speed/Pitch)
    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed { get => _playbackSpeed; set { value = Math.Clamp(value, 0.5, 2.0); if (SetProperty(ref _playbackSpeed, value)) { PlaybackService.PlaybackRate = (float)value; OnPropertyChanged(nameof(PlaybackSpeedDisplay)); } } }
    public string PlaybackSpeedDisplay => $"{PlaybackSpeed:F2}x";

    public double PlaybackPitch 
    {
        get; 
        
        set
        {
            value = double.Round(value * 2, MidpointRounding.AwayFromZero) / 2.0;
            value = double.Clamp(value, -4.0, 4.0);

            if (!SetProperty(ref field, value))
            {
                return;
            }

            PlaybackService.PitchSemitones = (float)field;
            OnPropertyChanged(nameof(PlaybackPitchDisplay));
        }
    } = 0.0;

    public string PlaybackPitchDisplay => $"{PlaybackPitch:+0.0;-0.0;0} st";

    public bool ShuffleEnabled
    {
        get;

        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
                // Saving preference could happen here
            }

            Debug.WriteLine($"[PlaybackVM] ShuffleEnabled set to: {value}");
        }
    } = false;

    public RepeatMode RepeatMode
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }
            Debug.WriteLine($"[PlaybackVM] RepeatMode set to: {value}");
            // Saving preference could happen here
            OnPropertyChanged(nameof(IsRepeatOne));
            OnPropertyChanged(nameof(IsRepeatAll));
            OnPropertyChanged(nameof(IsRepeatActive)); // Notify composite state change
        }
    } = RepeatMode.PlayOnce;

    // Helper properties for UI bindings (e.g., RadioButtons or toggling states) - Renamed
    public bool IsRepeatOne { get => RepeatMode == RepeatMode.RepeatOne; set { if (value) RepeatMode = RepeatMode.RepeatOne; } }
    public bool IsRepeatAll { get => RepeatMode == RepeatMode.RepeatAll; set { if (value) RepeatMode = RepeatMode.RepeatAll; } }
    // Helper for the ToggleButton IsChecked state (active if not None)
    public bool IsRepeatActive => RepeatMode != RepeatMode.None;


    // Commands for UI controls for modes
    public ICommand ToggleShuffleCommand { get; }
    // Renamed command to reflect cycling through repeat modes
    public ICommand CycleRepeatModeCommand { get; } // Cycles through None -> PlayOnce -> RepeatOne -> RepeatAll -> None


    // Waveform data
    public ObservableCollection<WaveformPoint> WaveformRenderData { get; } = new();
    private bool _isWaveformLoading = false;
    // Changed setter to private to enforce internal state management
    public bool IsWaveformLoading { get => _isWaveformLoading; private set => SetProperty(ref _isWaveformLoading, value); }

    // Derived properties for UI display (Split time display)
    public string CurrentTimeDisplay
    {
        get
        {
            if (PlaybackService.CurrentSong != null)
            {
                return $"{PlaybackService.CurrentPosition:mm\\:ss}";
            }
            return "--:--";
        }
    }

    public string TotalTimeDisplay
    {
        get
        {
            if (PlaybackService.CurrentSong != null && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
            {
                return $"{PlaybackService.CurrentSongDuration:mm\\:ss}";
            }
            return "--:--";
        }
    }


    // Commands owned by PlaybackViewModel
    public ICommand PlayPauseResumeCommand { get; } // Renamed from simple Click handler
    public ICommand SeekCommand { get; } // Command for slider/waveform seeking


    public PlaybackViewModel(PlaybackService playbackService, WaveformService waveformService)
    {
        PlaybackService = playbackService;
        _waveformService = waveformService;

        // Initialize playback controls (Speed/Pitch)
        PlaybackSpeed = 1.0; // Sets service value and raises property changed
        PlaybackPitch = 0.0; // Sets service value and raises property changed

        // Initialize playback modes (Defaults)
        ShuffleEnabled = false;
        RepeatMode = RepeatMode.PlayOnce; // Default repeat mode


        // Initialize commands
        PlayPauseResumeCommand = new RelayCommand(
            _ => TogglePlayPauseResume(),
            _ => PlaybackService.CurrentSong != null && !IsWaveformLoading); // Can't control playback while waveform is loading

        SeekCommand = new RelayCommand(
            positionSecondsObj => {
                if (positionSecondsObj is double seconds && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
                {
                    // This command is useful for explicit seek calls, although the slider two-way binding is primary.
                    PlaybackService.Seek(TimeSpan.FromSeconds(seconds));
                }
            },
             _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentSongDuration.TotalSeconds > 0 && !IsWaveformLoading);

        ToggleShuffleCommand = new RelayCommand(
            _ => ShuffleEnabled = !ShuffleEnabled,
             _ => PlaybackService.CurrentSong != null // Can shuffle only if a song is loaded (implies a list exists)
        );

        // Renamed command handler call
        CycleRepeatModeCommand = new RelayCommand(
             _ => CycleRepeatMode(),
             _ => PlaybackService.CurrentSong != null // Can repeat only if a song is loaded
        );


        // Subscribe to PlaybackService property changes
        PlaybackService.PropertyChanged += PlaybackService_PropertyChanged;

        // Subscribe to PlaybackViewModel's own properties that affect command CanExecute
        // ShuffleEnabled, RepeatMode, IsWaveformLoading affect command states.
        // CurrentPosition, CurrentSongDuration affect seek command CanExecute (handled by PS_PropertyChanged)
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

    // Renamed handler and updated cycle logic
    private void CycleRepeatMode()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.None => RepeatMode.PlayOnce,
            RepeatMode.PlayOnce => RepeatMode.RepeatOne,
            RepeatMode.RepeatOne => RepeatMode.RepeatAll,
            RepeatMode.RepeatAll => RepeatMode.None,
            _ => RepeatMode.None // Should not happen
        };
        RaisePlaybackCommandCanExecuteChanged(); // Repeat commands affected
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

                    // If the song becomes null (e.g., after Stop), clear waveform data and loading state
                    if (PlaybackService.CurrentSong == null)
                    {
                        Debug.WriteLine("[PlaybackVM] PlaybackService.CurrentSong is null. Clearing waveform data.");
                        WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData));
                        IsWaveformLoading = false; // Internal setter is fine
                    }

                    // Update time displays when song changes
                    OnPropertyChanged(nameof(CurrentTimeDisplay));
                    OnPropertyChanged(nameof(TotalTimeDisplay));

                    // Commands that require a song to be loaded might be affected
                    RaisePlaybackCommandCanExecuteChanged();
                    break;
                case nameof(PlaybackService.CurrentPosition):
                    OnPropertyChanged(nameof(CurrentPosition));
                    OnPropertyChanged(nameof(CurrentPositionSeconds)); // This will reflect the change from PlaybackService
                    OnPropertyChanged(nameof(CurrentTimeDisplay)); // Update current time display
                    // Seek command might be affected (CanExecute depends on duration > 0, which changes less often)
                    // RaisePlaybackCommandCanExecuteChanged(); // Usually not needed for position change
                    break;
                case nameof(PlaybackService.CurrentSongDuration):
                    OnPropertyChanged(nameof(CurrentSongDuration));
                    OnPropertyChanged(nameof(CurrentSongDurationSeconds));
                    OnPropertyChanged(nameof(TotalTimeDisplay)); // Update total time display
                    RaisePlaybackCommandCanExecuteChanged(); // Seek command's CanExecute depends on duration > 0
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
            case nameof(ShuffleEnabled): // Commands might be enabled/disabled based on modes
            case nameof(RepeatMode): // Commands might be enabled/disabled based on modes
                RaisePlaybackCommandCanExecuteChanged();
                // Also update the composite IsRepeatActive property
                OnPropertyChanged(nameof(IsRepeatActive));
                break;
            case nameof(HasCurrentSong):
                RaisePlaybackCommandCanExecuteChanged(); // ToggleShuffle and CycleRepeatMode depend on this
                break;
                // PlaybackSpeed, PlaybackPitch don't inherently affect command CanExecute
        }
    }


    /// <summary>
    /// Loads the waveform data for the currently playing song. Designed to be called by MainWindowViewModel.
    /// </summary>
    public async Task LoadWaveformForCurrentSongAsync()
    {
        var songToLoadWaveformFor = PlaybackService.CurrentSong;
        if (songToLoadWaveformFor == null || string.IsNullOrEmpty(songToLoadWaveformFor.FilePath))
        {
            Debug.WriteLine("[PlaybackVM] LoadWaveformForCurrentSongAsync skipped: No current song or invalid path.");
            // Clearing waveform data and setting loading state to false is handled by PlaybackService_PropertyChanged when CurrentSong becomes null
            return;
        }

        // Check if waveform is already loaded for this song OR currently loading
        // A simple check if there are any points is a rough indicator.
        // A more robust approach would be to store the file path associated with WaveformRenderData.
        // For simplicity, for now, if it has points OR is loading, assume it's for the current song.
        if (WaveformRenderData.Any() || IsWaveformLoading)
        {
            Debug.WriteLine($"[PlaybackVM] Waveform already loaded ({WaveformRenderData.Count} points) or loading ({IsWaveformLoading}) for {songToLoadWaveformFor.Title}. Skipping load.");
            return;
        }


        // Clear existing waveform data immediately to show loading state
        Debug.WriteLine($"[PlaybackVM] Clearing previous waveform data ({WaveformRenderData.Count} points).");
        WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData));
        IsWaveformLoading = true; // Internal setter is fine here

        try
        {
            Debug.WriteLine($"[PlaybackVM] Requesting waveform for: {songToLoadWaveformFor.Title}");
            // Target points should probably be based on control width or a fixed resolution
            // For a fixed 80px height control, 1000 points is likely sufficient detail.
            List<WaveformPoint> points = await _waveformService.GetWaveformAsync(songToLoadWaveformFor.FilePath, 1000);

            // Check if the song is still the same AFTER the async operation before updating the UI
            if (PlaybackService.CurrentSong == songToLoadWaveformFor)
            {
                Debug.WriteLine($"[PlaybackVM] Waveform loaded for: {songToLoadWaveformFor.Title}, {points.Count} points. Updating UI.");
                // Add points on the UI thread
                await Dispatcher.UIThread.InvokeAsync(() => {
                    foreach (var p in points) WaveformRenderData.Add(p);
                    OnPropertyChanged(nameof(WaveformRenderData)); // Notify UI
                });
                // _currentWaveformFilePath = songToLoadWaveformFor.FilePath; // Store the file path of the loaded waveform

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
        (ToggleShuffleCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CycleRepeatModeCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Renamed
    }
}