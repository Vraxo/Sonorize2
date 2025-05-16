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

    internal IWavePlayer? waveOutDevice;
    private AudioFileReader? audioFileReader;
    private Timer? uiUpdateTimer;

    public PlaybackService()
    {
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateUiCallback(object? state)
    {
        if (IsPlaying && audioFileReader != null && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (audioFileReader == null) return;

                CurrentPosition = audioFileReader.CurrentTime;

                // A-B Loop Check using ActiveLoop
                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    if (activeLoop.End > activeLoop.Start && audioFileReader.CurrentTime >= activeLoop.End)
                    {
                        // Check again due to async nature before seeking
                        if (audioFileReader != null && CurrentSong?.ActiveLoop != null)
                        {
                            Seek(CurrentSong.ActiveLoop.Start);
                        }
                    }
                }
            });
        }
    }

    private void StartUiUpdateTimer()
    {
        uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(200));
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
            waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
            waveOutDevice.Stop();
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
        CleanUpPlaybackResources();

        CurrentSong = song;

        try
        {
            audioFileReader = new AudioFileReader(song.FilePath);
            waveOutDevice = new WaveOutEvent();
            waveOutDevice.PlaybackStopped += OnPlaybackStopped;
            waveOutDevice.Init(audioFileReader);

            CurrentSongDuration = audioFileReader.TotalTime;
            CurrentPosition = TimeSpan.Zero;

            waveOutDevice.Play();
            IsPlaying = true;
            StartUiUpdateTimer();
            Console.WriteLine($"NAudio Playing: {song.Title}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing playback for {song.FilePath}: {ex.Message}");
            IsPlaying = false;
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsPlaying = false;
            StopUiUpdateTimer();

            if (audioFileReader != null && CurrentPosition < audioFileReader.TotalTime)
            {
                CurrentPosition = audioFileReader.TotalTime;
            }
            // Don't clear CurrentSong here, so ActiveLoop can still be managed if song ends.
            // CleanUpPlaybackResources(); // Only clean up if explicitly stopped or new song plays

            if (e.Exception != null)
            {
                Console.WriteLine($"NAudio Playback Error: {e.Exception.Message}");
            }
        });
    }

    public void Pause()
    {
        if (IsPlaying && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            waveOutDevice.Pause();
            IsPlaying = false;
            StopUiUpdateTimer();
            Console.WriteLine("NAudio Paused");
        }
    }

    public void Resume()
    {
        if (!IsPlaying && CurrentSong != null && waveOutDevice?.PlaybackState == PlaybackState.Paused)
        {
            waveOutDevice.Play();
            IsPlaying = true;
            StartUiUpdateTimer();
            Console.WriteLine("NAudio Resumed");
        }
    }

    public void Stop()
    {
        // If we stop, we should probably also deactivate any active loop visually for the current song.
        // The ActiveLoop on the song object itself can remain until a new song is played or it's explicitly changed.
        CleanUpPlaybackResources();
        IsPlaying = false;
        CurrentPosition = TimeSpan.Zero;
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
            CurrentPosition = audioFileReader.CurrentTime;
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