using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services; // This using directive makes PlaybackStateStatus from the Service available

namespace Sonorize.ViewModels;

public enum RepeatMode { None, PlayOnce, RepeatOne, RepeatAll }

public class PlaybackViewModel : ViewModelBase
{
    public PlaybackService PlaybackService { get; } // Keep reference to the service

    public WaveformDisplayViewModel WaveformDisplay { get; }

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

    public PlaybackStateStatus CurrentPlaybackStatus => PlaybackService.CurrentPlaybackStatus;
    
    public bool IsPlaying => PlaybackService.IsPlaying;

    public double PlaybackSpeed { get; set { value = Math.Clamp(value, 0.5, 2.0); if (SetProperty(ref field, value)) { PlaybackService.PlaybackRate = (float)value; OnPropertyChanged(nameof(PlaybackSpeedDisplay)); } } } = 1.0;
    
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
        WaveformDisplay = new WaveformDisplayViewModel(playbackService, waveformService);


        // Initialize playback controls (Speed/Pitch)
        PlaybackSpeed = 1.0; // Sets service value and raises property changed
        PlaybackPitch = 0.0; // Sets service value and raises property changed

        // Initialize playback modes (Defaults)
        ShuffleEnabled = false;
        RepeatMode = RepeatMode.PlayOnce; // Default repeat mode


        // Initialize commands
        PlayPauseResumeCommand = new RelayCommand(
            _ => TogglePlayPauseResume(),
            _ => PlaybackService.CurrentSong != null && !WaveformDisplay.IsWaveformLoading); // Can't control playback while waveform is loading

        SeekCommand = new RelayCommand(
            positionSecondsObj =>
            {
                if (positionSecondsObj is double seconds && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
                {
                    // This command is useful for explicit seek calls, although the slider two-way binding is primary.
                    PlaybackService.Seek(TimeSpan.FromSeconds(seconds));
                }
            },
             _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentSongDuration.TotalSeconds > 0 && !WaveformDisplay.IsWaveformLoading);

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
        WaveformDisplay.PropertyChanged += WaveformDisplay_PropertyChanged;
    }

    private void WaveformDisplay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WaveformDisplayViewModel.IsWaveformLoading))
        {
            RaisePlaybackCommandCanExecuteChanged(); // PlayPauseResumeCommand and SeekCommand depend on this
        }
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

                    // Waveform loading logic is now handled by WaveformDisplayViewModel based on CurrentSong change.

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
            // IsWaveformLoading is now on WaveformDisplayViewModel, handled by WaveformDisplay_PropertyChanged
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

    public void RaisePlaybackCommandCanExecuteChanged()
    {
        //Debug.WriteLine("[PlaybackVM] Raising playback command CanExecute changed.");
        (PlayPauseResumeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SeekCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleShuffleCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CycleRepeatModeCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Renamed
    }
}