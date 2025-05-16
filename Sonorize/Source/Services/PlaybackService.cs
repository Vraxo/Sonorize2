using Avalonia.Threading; // For Dispatcher
using NAudio.Wave; // Core NAudio namespace for playback
using Sonorize.Models;
using Sonorize.ViewModels; // For ViewModelBase
using System;
using System.Threading;

namespace Sonorize.Services;

public class PlaybackService : ViewModelBase, IDisposable
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
                OnPropertyChanged(nameof(CurrentPositionSeconds));
            }
        }
    }

    public double CurrentPositionSeconds
    {
        get => CurrentPosition.TotalSeconds;
        set
        {
            // Only seek if the value change is significant and we have a reader
            if (audioFileReader != null && Math.Abs(CurrentPosition.TotalSeconds - value) > 0.5)
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
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
            }
        }
    }

    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 100;

    private IWavePlayer? waveOutDevice;
    private AudioFileReader? audioFileReader;
    private Timer? uiUpdateTimer;

    public PlaybackService()
    {
        // Initialize the UI update timer but don't start it yet
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateUiCallback(object? state)
    {
        if (IsPlaying && audioFileReader != null && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            // Ensure UI updates are on the UI thread
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentPosition = audioFileReader.CurrentTime;
            });
        }
    }

    private void StartUiUpdateTimer()
    {
        uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(200)); // Update UI every 200ms
    }

    private void StopUiUpdateTimer()
    {
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void CleanUpPlaybackResources()
    {
        StopUiUpdateTimer();

        if (waveOutDevice != null)
        {
            waveOutDevice.PlaybackStopped -= OnPlaybackStopped; // Unhook event handler
            waveOutDevice.Stop(); // Stop playback
            waveOutDevice.Dispose();
            waveOutDevice = null;
        }
        if (audioFileReader != null)
        {
            audioFileReader.Dispose();
            audioFileReader = null;
        }
    }

    public void Play(Song song)
    {
        CleanUpPlaybackResources(); // Clean up any existing playback

        CurrentSong = song; // Set current song immediately

        try
        {
            audioFileReader = new AudioFileReader(song.FilePath);
            waveOutDevice = new WaveOutEvent(); // Or WasapiOut for lower latency on Windows
            waveOutDevice.PlaybackStopped += OnPlaybackStopped;
            waveOutDevice.Init(audioFileReader);

            CurrentSongDuration = audioFileReader.TotalTime;
            CurrentPosition = TimeSpan.Zero; // Reset position for new song

            waveOutDevice.Play();
            IsPlaying = true;
            StartUiUpdateTimer();
            Console.WriteLine($"NAudio Playing: {song.Title}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing playback for {song.FilePath}: {ex.Message}");
            // Potentially update UI to show an error
            IsPlaying = false;
            CurrentSong = null; // Clear current song if playback failed to start
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // This event fires when playback ends naturally or is stopped.
        // We've unhooked it in CleanUpPlaybackResources before manual stop,
        // so this handler is primarily for when the song finishes playing naturally.
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsPlaying = false;
            StopUiUpdateTimer();
            // Resources are cleaned up by the next Play() or by Dispose() or Stop()
            // Here, we just ensure the state reflects that nothing is playing.
            // If we want to automatically play the next song, this is where to trigger it.

            // Ensure position is at the end if it stopped naturally
            if (audioFileReader != null && CurrentPosition < audioFileReader.TotalTime)
            {
                CurrentPosition = audioFileReader.TotalTime;
            }

            CleanUpPlaybackResources(); // Clean up the finished song's resources

            if (e.Exception != null)
            {
                Console.WriteLine($"NAudio Playback Error: {e.Exception.Message}");
            }
            // Update UI or play next song
        });
    }

    public void Pause()
    {
        if (IsPlaying && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            waveOutDevice.Pause();
            IsPlaying = false;
            StopUiUpdateTimer(); // Stop UI updates while paused
            Console.WriteLine("NAudio Paused");
        }
    }

    public void Resume()
    {
        if (!IsPlaying && CurrentSong != null && waveOutDevice?.PlaybackState == PlaybackState.Paused)
        {
            waveOutDevice.Play();
            IsPlaying = true;
            StartUiUpdateTimer(); // Resume UI updates
            Console.WriteLine("NAudio Resumed");
        }
    }

    public void Stop()
    {
        CleanUpPlaybackResources();
        IsPlaying = false;
        // Optionally clear CurrentSong or keep it for context until a new song is played
        // CurrentSong = null;
        CurrentPosition = TimeSpan.Zero;
        // CurrentSongDuration = TimeSpan.Zero; // Keep duration of last song visible? Or reset?
        Console.WriteLine("NAudio Stopped");
    }

    public void Seek(TimeSpan position)
    {
        if (audioFileReader != null)
        {
            var newPosition = position;
            if (newPosition < TimeSpan.Zero) newPosition = TimeSpan.Zero;
            if (newPosition > audioFileReader.TotalTime) newPosition = audioFileReader.TotalTime;

            audioFileReader.CurrentTime = newPosition;
            CurrentPosition = audioFileReader.CurrentTime; // Immediately update our property
        }
    }

    public void Dispose()
    {
        CleanUpPlaybackResources();
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
        GC.SuppressFinalize(this);
    }
}