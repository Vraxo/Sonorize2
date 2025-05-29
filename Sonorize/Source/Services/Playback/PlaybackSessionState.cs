using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackSessionState : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private Song? _currentSong;
    public Song? CurrentSong
    {
        get => _currentSong;
        set
        {
            if (SetProperty(ref _currentSong, value))
            {
                OnPropertyChanged(nameof(HasCurrentSong)); // Dependent property
            }
        }
    }

    public bool HasCurrentSong => CurrentSong != null;

    private bool _isPlaying;
    public bool IsPlaying { get => _isPlaying; set => SetProperty(ref _isPlaying, value); }

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus { get => _currentPlaybackStatus; set => SetProperty(ref _currentPlaybackStatus, value); }

    public TimeSpan CurrentPosition
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(CurrentPositionSeconds)); // Dependent property
            }
        }
    }
    public double CurrentPositionSeconds => CurrentPosition.TotalSeconds;

    public TimeSpan CurrentSongDuration
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(CurrentSongDurationSeconds)); // Dependent property
            }
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1.0;

    public float PlaybackRate { get; set => SetProperty(ref field, value); } = 1.0f;
    public float PitchSemitones { get; set => SetProperty(ref field, value); } = 0f;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void ResetToDefault()
    {
        CurrentSong = null;
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        CurrentPosition = TimeSpan.Zero;
        CurrentSongDuration = TimeSpan.Zero;
        // PlaybackRate and PitchSemitones might retain their last set values or reset too,
        // depending on desired behavior. For now, assume they persist unless explicitly changed.
        // If they should reset:
        // PlaybackRate = 1.0f;
        // PitchSemitones = 0f;
    }
}