using Sonorize.Models;
using Sonorize.ViewModels; // For ViewModelBase
using System;
using System.Threading;
using Avalonia.Threading; // For Dispatcher

namespace Sonorize.Services;

public class PlaybackService : ViewModelBase
{
    private Song? _currentSong;
    public Song? CurrentSong { get => _currentSong; private set => SetProperty(ref _currentSong, value); }

    private bool _isPlaying;
    public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        set
        {
            if (SetProperty(ref _currentPosition, value))
            {
                OnPropertyChanged(nameof(CurrentPositionSeconds)); // Notify UI
            }
        }
    }

    // Used for slider binding
    public double CurrentPositionSeconds
    {
        get => CurrentPosition.TotalSeconds;
        set
        {
            if (Math.Abs(CurrentPosition.TotalSeconds - value) > 0.5) // Avoid too frequent updates from slider
            {
                Seek(TimeSpan.FromSeconds(value));
            }
        }
    }

    private TimeSpan _currentSongDuration;
    public TimeSpan CurrentSongDuration
    {
        get => _currentSongDuration;
        private set
        {
            if (SetProperty(ref _currentSongDuration, value))
            {
                OnPropertyChanged(nameof(CurrentSongDurationSeconds)); // Notify UI
            }
        }
    }

    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 100; // Default if zero

    private System.Threading.Timer? _playbackTimer;

    public void Play(Song song)
    {
        CurrentSong = song;
        CurrentSongDuration = song.Duration;
        CurrentPosition = TimeSpan.Zero;
        IsPlaying = true;

        _playbackTimer?.Dispose();
        _playbackTimer = new System.Threading.Timer(tick =>
        {
            if (IsPlaying && CurrentSong != null)
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (CurrentPosition < CurrentSongDuration)
                    {
                        CurrentPosition = CurrentPosition.Add(TimeSpan.FromSeconds(1));
                    }
                    else
                    {
                        Stop(); // Or play next song
                    }
                });
            }
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

        Console.WriteLine($"Playing: {song.Title}");
    }

    public void Pause()
    {
        if (!IsPlaying) return; // Already paused
        IsPlaying = false;
        _playbackTimer?.Change(Timeout.Infinite, Timeout.Infinite); // Stop timer ticks
        Console.WriteLine("Paused");
    }

    public void Resume()
    {
        if (IsPlaying || CurrentSong == null) return; // Already playing or no song
        IsPlaying = true;
        _playbackTimer?.Change(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)); // Resume timer
        Console.WriteLine("Resumed");
    }

    public void Stop()
    {
        IsPlaying = false;
        CurrentPosition = TimeSpan.Zero;
        _playbackTimer?.Dispose();
        _playbackTimer = null;
        // CurrentSong = null; // Optionally clear, or keep for context
        Console.WriteLine("Stopped");
    }

    public void Seek(TimeSpan position)
    {
        if (CurrentSong != null)
        {
            var newPosition = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            newPosition = newPosition > CurrentSongDuration ? CurrentSongDuration : newPosition;
            CurrentPosition = newPosition;

            Console.WriteLine($"Seeked to: {CurrentPosition}");
            if (CurrentPosition >= CurrentSongDuration && IsPlaying)
            {
                Stop(); // Stop if seeked to or past the end while playing
            }
        }
    }
}