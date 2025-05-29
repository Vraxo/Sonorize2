using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services; // This using directive makes PlaybackStateStatus from the Service available

namespace Sonorize.ViewModels;

public enum RepeatMode { None, PlayOnce, RepeatOne, RepeatAll }

public class PlaybackViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackService _playbackService; // Keep reference for commands if needed directly
    private readonly PlaybackStateViewModel _stateViewModel;

    public WaveformDisplayViewModel WaveformDisplay { get; }
    public PlaybackModeViewModel ModeControls { get; }
    public PlaybackEffectsViewModel EffectsControls { get; } // New child VM

    // Properties proxied from PlaybackStateViewModel
    public Song? CurrentSong => _stateViewModel.CurrentSong;
    public bool HasCurrentSong => _stateViewModel.HasCurrentSong;
    public TimeSpan CurrentPosition => _stateViewModel.CurrentPosition;
    public double CurrentPositionSeconds
    {
        get => _stateViewModel.CurrentPositionSeconds;
        set
        {
            if (CurrentSong == null || Math.Abs(_stateViewModel.CurrentPositionSeconds - value) <= 0.01)
            {
                return;
            }
            _playbackService.Seek(TimeSpan.FromSeconds(value)); // Use _playbackService for actions
        }
    }
    public TimeSpan CurrentSongDuration => _stateViewModel.CurrentSongDuration;
    public double CurrentSongDurationSeconds => _stateViewModel.CurrentSongDurationSeconds;
    public PlaybackStateStatus CurrentPlaybackStatus => _stateViewModel.CurrentPlaybackStatus;
    public bool IsPlaying => _stateViewModel.IsPlaying;
    public string CurrentTimeDisplay => _stateViewModel.CurrentTimeDisplay;
    public string TotalTimeDisplay => _stateViewModel.TotalTimeDisplay;


    public ICommand PlayPauseResumeCommand { get; }
    public ICommand SeekCommand { get; }

    public PlaybackViewModel(PlaybackService playbackService, WaveformService waveformService)
    {
        _playbackService = playbackService;
        _stateViewModel = new PlaybackStateViewModel(playbackService);

        WaveformDisplay = new WaveformDisplayViewModel(playbackService, waveformService);
        ModeControls = new PlaybackModeViewModel(this); // 'this' provides HasCurrentSong
        EffectsControls = new PlaybackEffectsViewModel(playbackService);

        PlayPauseResumeCommand = new RelayCommand(
            _ => TogglePlayPauseResume(),
            _ => CurrentSong != null && !WaveformDisplay.IsWaveformLoading);

        SeekCommand = new RelayCommand(
            positionSecondsObj =>
            {
                if (positionSecondsObj is double seconds && CurrentSongDuration.TotalSeconds > 0)
                {
                    _playbackService.Seek(TimeSpan.FromSeconds(seconds));
                }
            },
             _ => CurrentSong != null && CurrentSongDuration.TotalSeconds > 0 && !WaveformDisplay.IsWaveformLoading);

        // Subscribe to PlaybackStateViewModel for changes that affect this VM's logic (e.g., command executability)
        _stateViewModel.PropertyChanged += StateViewModel_PropertyChanged;
        PropertyChanged += PlaybackViewModel_PropertyChanged;
        WaveformDisplay.PropertyChanged += WaveformDisplay_PropertyChanged;
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
        Debug.WriteLine($"[PlaybackVM] TogglePlayPauseResume called. Current state: {CurrentPlaybackStatus}");
        if (CurrentPlaybackStatus == PlaybackStateStatus.Playing)
        {
            _playbackService.Pause();
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            _playbackService.Resume();
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped && CurrentSong != null)
        {
            _playbackService.Resume(); // Or Play if starting from scratch
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped && CurrentSong == null)
        {
            Debug.WriteLine("[PlaybackVM] TogglePlayPauseResume called in Stopped state with no song. Doing nothing.");
        }
    }

    private void StateViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Forward property changes for proxied properties
        OnPropertyChanged(e.PropertyName);

        // Specific logic based on changes from StateViewModel
        if (e.PropertyName == nameof(PlaybackStateViewModel.HasCurrentSong) ||
            e.PropertyName == nameof(PlaybackStateViewModel.CurrentSongDuration) ||
            e.PropertyName == nameof(PlaybackStateViewModel.CurrentPlaybackStatus))
        {
            RaisePlaybackCommandCanExecuteChanged();
        }
    }


    private void PlaybackViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // This handler is for properties of PlaybackViewModel itself, if any directly impact ModeControls.
        // HasCurrentSong is now proxied from StateViewModel, so StateViewModel_PropertyChanged handles its effects.
        // If PlaybackViewModel had other direct properties affecting ModeControls, they'd be handled here.
        // For now, this might not be strictly necessary if all relevant changes come via StateViewModel.
        // However, keeping it for potential future direct properties of PlaybackViewModel.
        switch (e.PropertyName)
        {
            case nameof(HasCurrentSong): // Though this comes from StateViewModel now
                ModeControls.RaiseCommandCanExecuteChanged();
                // RaisePlaybackCommandCanExecuteChanged(); // This is also called in StateViewModel_PropertyChanged
                break;
        }
    }

    public void RaisePlaybackCommandCanExecuteChanged()
    {
        (PlayPauseResumeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SeekCommand as RelayCommand)?.RaiseCanExecuteChanged();
        ModeControls.RaiseCommandCanExecuteChanged();
    }

    public void Dispose()
    {
        _stateViewModel.PropertyChanged -= StateViewModel_PropertyChanged;
        _stateViewModel.Dispose();

        if (WaveformDisplay != null)
        {
            WaveformDisplay.PropertyChanged -= WaveformDisplay_PropertyChanged;
            // If WaveformDisplay implements IDisposable, call it
        }
        ModeControls?.Dispose();
    }
}