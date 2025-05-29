using System;
using System.ComponentModel;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels;

public class PlaybackStateViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackService _playbackService;

    public Song? CurrentSong => _playbackService.CurrentSong;
    public bool HasCurrentSong => _playbackService.CurrentSong != null;
    public TimeSpan CurrentPosition => _playbackService.CurrentPosition;
    public double CurrentPositionSeconds => _playbackService.CurrentPositionSeconds;
    public TimeSpan CurrentSongDuration => _playbackService.CurrentSongDuration;
    public double CurrentSongDurationSeconds => _playbackService.CurrentSongDurationSeconds;
    public PlaybackStateStatus CurrentPlaybackStatus => _playbackService.CurrentPlaybackStatus;
    public bool IsPlaying => _playbackService.IsPlaying;

    private string _currentTimeDisplay = "--:--";
    public string CurrentTimeDisplay
    {
        get => _currentTimeDisplay;
        private set => SetProperty(ref _currentTimeDisplay, value);
    }

    private string _totalTimeDisplay = "--:--";
    public string TotalTimeDisplay
    {
        get => _totalTimeDisplay;
        private set => SetProperty(ref _totalTimeDisplay, value);
    }

    public PlaybackStateViewModel(PlaybackService playbackService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;
        UpdateAllDisplayProperties(); // Initial sync
    }

    private void PlaybackService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    OnPropertyChanged(nameof(CurrentSong));
                    OnPropertyChanged(nameof(HasCurrentSong));
                    UpdateCurrentTimeDisplay();
                    UpdateTotalTimeDisplay();
                    break;
                case nameof(PlaybackService.CurrentPosition):
                    OnPropertyChanged(nameof(CurrentPosition));
                    OnPropertyChanged(nameof(CurrentPositionSeconds));
                    UpdateCurrentTimeDisplay();
                    break;
                case nameof(PlaybackService.CurrentSongDuration):
                    OnPropertyChanged(nameof(CurrentSongDuration));
                    OnPropertyChanged(nameof(CurrentSongDurationSeconds));
                    UpdateTotalTimeDisplay();
                    break;
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    OnPropertyChanged(nameof(CurrentPlaybackStatus));
                    OnPropertyChanged(nameof(IsPlaying));
                    break;
            }
        });
    }

    private void UpdateAllDisplayProperties()
    {
        UpdateCurrentTimeDisplay();
        UpdateTotalTimeDisplay();
        OnPropertyChanged(nameof(CurrentSong));
        OnPropertyChanged(nameof(HasCurrentSong));
        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(CurrentPositionSeconds));
        OnPropertyChanged(nameof(CurrentSongDuration));
        OnPropertyChanged(nameof(CurrentSongDurationSeconds));
        OnPropertyChanged(nameof(CurrentPlaybackStatus));
        OnPropertyChanged(nameof(IsPlaying));
    }

    private void UpdateCurrentTimeDisplay()
    {
        CurrentTimeDisplay = _playbackService.CurrentSong != null ? $"{_playbackService.CurrentPosition:mm\\:ss}" : "--:--";
    }

    private void UpdateTotalTimeDisplay()
    {
        TotalTimeDisplay = (_playbackService.CurrentSong != null && _playbackService.CurrentSongDuration.TotalSeconds > 0)
            ? $"{_playbackService.CurrentSongDuration:mm\\:ss}"
            : "--:--";
    }

    public void Dispose()
    {
        _playbackService.PropertyChanged -= PlaybackService_PropertyChanged;
    }
}