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
    private readonly PlaybackService _playbackService;
    private bool _isUserDraggingSlider;

    public WaveformDisplayViewModel WaveformDisplay { get; }
    public PlaybackModeViewModel ModeControls { get; }
    public PlaybackEffectsViewModel EffectsControls { get; }

    // Properties directly from PlaybackService or managed by this ViewModel
    public Song? CurrentSong => _playbackService.CurrentSong;
    public bool HasCurrentSong => _playbackService.CurrentSong != null;
    public TimeSpan CurrentPosition => _playbackService.CurrentPosition;
    public double CurrentPositionSeconds => _playbackService.CurrentPosition.TotalSeconds;
    public TimeSpan CurrentSongDuration => _playbackService.CurrentSongDuration;
    public double CurrentSongDurationSeconds => _playbackService.CurrentSongDurationSeconds;
    public PlaybackStateStatus CurrentPlaybackStatus => _playbackService.CurrentPlaybackStatus;
    public bool IsPlaying => _playbackService.IsPlaying;

    private double _sliderPosition;
    public double SliderPosition
    {
        get => _sliderPosition;
        set
        {
            // This is only set by the slider or the timer update.
            // The check prevents feedback loops if SetProperty is called from the timer
            // with the same value that the slider already has.
            if (Math.Abs(_sliderPosition - value) > 0.001)
            {
                SetProperty(ref _sliderPosition, value);
            }
        }
    }


    public string CurrentTimeDisplay => _playbackService.CurrentSong != null ? $"{_playbackService.CurrentPosition:mm\\:ss}" : "--:--";
    public string TotalTimeDisplay => (_playbackService.CurrentSong != null && _playbackService.CurrentSongDuration.TotalSeconds > 0)
            ? $"{_playbackService.CurrentSongDuration:mm\\:ss}"
            : "--:--";

    public ICommand PlayPauseResumeCommand { get; }
    public ICommand SeekCommand { get; }

    public PlaybackViewModel(PlaybackService playbackService, WaveformService waveformService)
    {
        _playbackService = playbackService;

        WaveformDisplay = new WaveformDisplayViewModel(playbackService, waveformService);
        ModeControls = new PlaybackModeViewModel(this);
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

        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;
        UpdateAllDisplayProperties(); // Initial sync

        PropertyChanged += PlaybackViewModel_PropertyChanged; // For ModeControls updates
        WaveformDisplay.PropertyChanged += WaveformDisplay_PropertyChanged;
    }

    private void PlaybackService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            bool commandStateMayChange = false;
            switch (e.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    OnPropertyChanged(nameof(CurrentSong));
                    OnPropertyChanged(nameof(HasCurrentSong));
                    OnPropertyChanged(nameof(CurrentTimeDisplay));
                    OnPropertyChanged(nameof(TotalTimeDisplay));
                    UpdateAllDisplayProperties();
                    commandStateMayChange = true;
                    break;
                case nameof(PlaybackService.CurrentPosition):
                    if (!_isUserDraggingSlider)
                    {
                        SliderPosition = _playbackService.CurrentPosition.TotalSeconds;
                    }
                    OnPropertyChanged(nameof(CurrentPosition));
                    OnPropertyChanged(nameof(CurrentPositionSeconds));
                    OnPropertyChanged(nameof(CurrentTimeDisplay));
                    break;
                case nameof(PlaybackService.CurrentSongDuration):
                    OnPropertyChanged(nameof(CurrentSongDuration));
                    OnPropertyChanged(nameof(CurrentSongDurationSeconds));
                    OnPropertyChanged(nameof(TotalTimeDisplay));
                    commandStateMayChange = true;
                    break;
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    OnPropertyChanged(nameof(CurrentPlaybackStatus));
                    OnPropertyChanged(nameof(IsPlaying));
                    commandStateMayChange = true;
                    break;
            }
            if (commandStateMayChange)
            {
                RaisePlaybackCommandCanExecuteChanged();
            }
        });
    }

    private void UpdateAllDisplayProperties()
    {
        OnPropertyChanged(nameof(CurrentTimeDisplay));
        OnPropertyChanged(nameof(TotalTimeDisplay));
        OnPropertyChanged(nameof(CurrentSong));
        OnPropertyChanged(nameof(HasCurrentSong));
        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(CurrentPositionSeconds));
        OnPropertyChanged(nameof(CurrentSongDuration));
        OnPropertyChanged(nameof(CurrentSongDurationSeconds));
        OnPropertyChanged(nameof(CurrentPlaybackStatus));
        OnPropertyChanged(nameof(IsPlaying));
        SliderPosition = _playbackService.CurrentPosition.TotalSeconds;
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
            _playbackService.Resume();
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped && CurrentSong == null)
        {
            Debug.WriteLine("[PlaybackVM] TogglePlayPauseResume called in Stopped state with no song. Doing nothing.");
        }
    }

    private void PlaybackViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HasCurrentSong):
                ModeControls.RaiseCommandCanExecuteChanged();
                break;
        }
    }

    public void BeginSliderDrag()
    {
        _isUserDraggingSlider = true;
        Debug.WriteLine("[PlaybackVM] Begin slider drag");
    }

    public void CompleteSliderDrag()
    {
        if (_isUserDraggingSlider)
        {
            _isUserDraggingSlider = false;
            Debug.WriteLine($"[PlaybackVM] Slider drag complete. Seeking to: {SliderPosition}");
            _playbackService.Seek(TimeSpan.FromSeconds(SliderPosition));
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
        _playbackService.PropertyChanged -= PlaybackService_PropertyChanged;
        PropertyChanged -= PlaybackViewModel_PropertyChanged;

        if (WaveformDisplay != null)
        {
            WaveformDisplay.PropertyChanged -= WaveformDisplay_PropertyChanged;
            // If WaveformDisplay implements IDisposable, call it
        }
        ModeControls?.Dispose();
    }
}