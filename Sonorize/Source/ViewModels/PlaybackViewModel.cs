using System;
using System.ComponentModel;
using System.Diagnostics;
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
    public PlaybackModeViewModel ModeControls { get; }
    public PlaybackEffectsViewModel EffectsControls { get; } // New child VM

    public Song? CurrentSong => PlaybackService.CurrentSong;

    public bool HasCurrentSong => PlaybackService.CurrentSong != null;

    public TimeSpan CurrentPosition => PlaybackService.CurrentPosition;

    public double CurrentPositionSeconds
    {
        get => PlaybackService.CurrentPositionSeconds;
        set
        {
            if (PlaybackService.CurrentSong == null || Math.Abs(PlaybackService.CurrentPositionSeconds - value) <= 0.01)
            {
                return;
            }
            PlaybackService.Seek(TimeSpan.FromSeconds(value));
        }
    }

    public TimeSpan CurrentSongDuration => PlaybackService.CurrentSongDuration;

    public double CurrentSongDurationSeconds => PlaybackService.CurrentSongDurationSeconds;

    public PlaybackStateStatus CurrentPlaybackStatus => PlaybackService.CurrentPlaybackStatus;

    public bool IsPlaying => PlaybackService.IsPlaying;

    // PlaybackSpeed and PlaybackPitch are now managed by PlaybackEffectsViewModel
    // public double PlaybackSpeed { get; set { value = Math.Clamp(value, 0.5, 2.0); if (SetProperty(ref field, value)) { PlaybackService.PlaybackRate = (float)value; OnPropertyChanged(nameof(PlaybackSpeedDisplay)); } } } = 1.0;
    // public string PlaybackSpeedDisplay => $"{PlaybackSpeed:F2}x";
    // public double PlaybackPitch { /* ... */ } = 0.0;
    // public string PlaybackPitchDisplay => $"{PlaybackPitch:+0.0;-0.0;0} st";

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

    public ICommand PlayPauseResumeCommand { get; }
    public ICommand SeekCommand { get; }

    public PlaybackViewModel(PlaybackService playbackService, WaveformService waveformService)
    {
        PlaybackService = playbackService;
        WaveformDisplay = new WaveformDisplayViewModel(playbackService, waveformService);
        ModeControls = new PlaybackModeViewModel(this);
        EffectsControls = new PlaybackEffectsViewModel(playbackService); // Instantiate new VM

        // PlaybackSpeed = 1.0; // Now managed by EffectsControls
        // PlaybackPitch = 0.0; // Now managed by EffectsControls

        PlayPauseResumeCommand = new RelayCommand(
            _ => TogglePlayPauseResume(),
            _ => PlaybackService.CurrentSong != null && !WaveformDisplay.IsWaveformLoading);

        SeekCommand = new RelayCommand(
            positionSecondsObj =>
            {
                if (positionSecondsObj is double seconds && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
                {
                    PlaybackService.Seek(TimeSpan.FromSeconds(seconds));
                }
            },
             _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentSongDuration.TotalSeconds > 0 && !WaveformDisplay.IsWaveformLoading);

        PlaybackService.PropertyChanged += PlaybackService_PropertyChanged;
        PropertyChanged += PlaybackViewModel_PropertyChanged; // For HasCurrentSong affecting ModeControls
        WaveformDisplay.PropertyChanged += WaveformDisplay_PropertyChanged;
        // EffectsControls does not currently raise events that PlaybackViewModel needs to listen to directly.
        // If it did, we would subscribe here: EffectsControls.PropertyChanged += EffectsControls_PropertyChanged;
    }

    private void WaveformDisplay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WaveformDisplayViewModel.IsWaveformLoading))
        {
            return;
        }

        RaisePlaybackCommandCanExecuteChanged();
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
            PlaybackService.Resume();
        }
        else if (PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Stopped && PlaybackService.CurrentSong == null)
        {
            Debug.WriteLine("[PlaybackVM] TogglePlayPauseResume called in Stopped state with no song. Doing nothing.");
        }
    }

    private void PlaybackService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    OnPropertyChanged(nameof(CurrentSong));
                    OnPropertyChanged(nameof(HasCurrentSong)); // This will be picked up by PlaybackModeViewModel
                    OnPropertyChanged(nameof(CurrentTimeDisplay));
                    OnPropertyChanged(nameof(TotalTimeDisplay));
                    RaisePlaybackCommandCanExecuteChanged();
                    break;
                case nameof(PlaybackService.CurrentPosition):
                    OnPropertyChanged(nameof(CurrentPosition));
                    OnPropertyChanged(nameof(CurrentPositionSeconds));
                    OnPropertyChanged(nameof(CurrentTimeDisplay));
                    break;
                case nameof(PlaybackService.CurrentSongDuration):
                    OnPropertyChanged(nameof(CurrentSongDuration));
                    OnPropertyChanged(nameof(CurrentSongDurationSeconds));
                    OnPropertyChanged(nameof(TotalTimeDisplay));
                    RaisePlaybackCommandCanExecuteChanged();
                    break;
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    OnPropertyChanged(nameof(CurrentPlaybackStatus));
                    OnPropertyChanged(nameof(IsPlaying));
                    RaisePlaybackCommandCanExecuteChanged();
                    break;
                    // If PlaybackRate or PitchSemitones from PlaybackService were to update EffectsControls:
                    // case nameof(PlaybackService.PlaybackRate):
                    //    EffectsControls.PlaybackSpeed = PlaybackService.PlaybackRate; // Or some sync logic
                    //    break;
                    // case nameof(PlaybackService.PitchSemitones):
                    //    EffectsControls.PlaybackPitch = PlaybackService.PitchSemitones;
                    //    break;
            }
        });
    }

    private void PlaybackViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HasCurrentSong): // ModeControls depends on this
                ModeControls.RaiseCommandCanExecuteChanged();
                RaisePlaybackCommandCanExecuteChanged(); // Other commands might also depend on HasCurrentSong
                break;
        }
    }

    public void RaisePlaybackCommandCanExecuteChanged()
    {
        (PlayPauseResumeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SeekCommand as RelayCommand)?.RaiseCanExecuteChanged();
        ModeControls.RaiseCommandCanExecuteChanged(); // Delegate to the extracted VM
        // EffectsControls currently has no commands.
    }

    public void Dispose()
    {
        if (PlaybackService != null)
        {
            PlaybackService.PropertyChanged -= PlaybackService_PropertyChanged;
        }
        if (WaveformDisplay != null)
        {
            WaveformDisplay.PropertyChanged -= WaveformDisplay_PropertyChanged;
            // If WaveformDisplay implements IDisposable, call it
        }
        ModeControls?.Dispose(); // Dispose the extracted VM
        // EffectsControls does not currently implement IDisposable or hold resources.
    }
}