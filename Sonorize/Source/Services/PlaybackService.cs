using Avalonia.Threading;
using ManagedBass;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;

namespace Sonorize.Services;

public enum PlaybackStateStatus { Stopped, Playing, Paused }

public class PlaybackService : ViewModelBase, IDisposable
{
    private Song? _currentSong;
    public Song? CurrentSong
    {
        get => _currentSong;
        private set
        {
            SetProperty(ref _currentSong, value);
            OnPropertyChanged(nameof(HasCurrentSong));
        }
    }
    public bool HasCurrentSong => CurrentSong != null;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get => _currentPlaybackStatus;
        private set => SetProperty(ref _currentPlaybackStatus, value);
    }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        set
        {
            if (SetProperty(ref _currentPosition, value))
                OnPropertyChanged(nameof(CurrentPositionSeconds));
        }
    }
    public double CurrentPositionSeconds
    {
        get => CurrentPosition.TotalSeconds;
        set
        {
            if (audioFileReader != null && Math.Abs(CurrentPosition.TotalSeconds - value) > 0.1)
                Seek(TimeSpan.FromSeconds(value));
        }
    }

    private TimeSpan _currentSongDuration;
    public TimeSpan CurrentSongDuration
    {
        get => _currentSongDuration;
        private set
        {
            if (SetProperty(ref _currentSongDuration, value))
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1;

    private int _streamHandle;
    private int _tempoEffectHandle;
    private int _pitchEffectHandle;
    private Timer? uiUpdateTimer;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (SetProperty(ref _playbackRate, value))
            {
                UpdateTempo(value);
            }
        }
    }

    private float _pitchSemitones = 0f;
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            if (SetProperty(ref _pitchSemitones, value))
            {
                UpdatePitch(value);
            }
        }
    }

    public PlaybackService()
    {
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateUiCallback(object? state)
    {
        if (IsPlaying && _streamHandle != 0 && Bass.ChannelIsPlaying(_streamHandle) == PlaybackState.Playing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (_streamHandle == 0) return;

                long position = Bass.ChannelGetPosition(_streamHandle);
                CurrentPosition = TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_streamHandle, position));

                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    var actualPlaybackTimeInFile = CurrentPosition;
                    if (actualPlaybackTimeInFile >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        Seek(activeLoop.Start);
                    }
                }
            });
        }
    }

    private void InitializeBassPipeline(string filePath)
    {
        CleanUpPlaybackResources();

        if (!Bass.Init(-1, 44100))
        {
            Console.WriteLine($"Bass.Init error: {Bass.LastError}");
            return;
        }

        _streamHandle = Bass.CreateStream(filePath, 0, 0, BassFlags.Decode | BassFlags.Float);
        if (_streamHandle == 0)
        {
            Console.WriteLine($"Bass.CreateStream error: {Bass.LastError}");
            return;
        }

        // Add tempo and pitch effects
        _tempoEffectHandle = Bass.ChannelSetFX(_streamHandle, EffectType.Tempo, 0);
        _pitchEffectHandle = Bass.ChannelSetFX(_streamHandle, EffectType.Tempo, 0); // Tempo effect is also used for pitch

        // Apply initial pitch and tempo
        UpdatePitch(_pitchSemitones);
        UpdateTempo(_playbackRate);

        // Create a playable stream from the decoded stream
        int playableStream = Bass.CreateStream(_streamHandle, 0, 0, BassFlags.Float);
         if (playableStream == 0)
        {
            Console.WriteLine($"Bass.CreateStream (playable) error: {Bass.LastError}");
            Bass.StreamFree(_streamHandle);
            _streamHandle = 0;
            return;
        }
        _streamHandle = playableStream;


        long length = Bass.ChannelGetLength(_streamHandle);
        CurrentSongDuration = TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_streamHandle, length));
        CurrentPosition = TimeSpan.Zero;
    }

    private void UpdateTempo(float rate)
    {
        if (_tempoEffectHandle != 0)
        {
            // Rate is a percentage change, so 1.0 is 0% change.
            // Bass.Net tempo is in percentage.
            Bass.FXSetParameters(_tempoEffectHandle, new TempoParameters(rate * 100f - 100f, 0, 0));
        }
    }

    private void UpdatePitch(float semitones)
    {
         if (_pitchEffectHandle != 0)
        {
            // Pitch is in semitones
            Bass.FXSetParameters(_pitchEffectHandle, new TempoParameters(0, semitones, 0));
        }
    }

    public void Play(Song song)
    {
        if (song == null) return;
        CurrentSong = song;

        try
        {
            InitializeBassPipeline(song.FilePath);

            if (_streamHandle != 0)
            {
                Bass.ChannelPlay(_streamHandle, false);
                IsPlaying = true;
                CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                StartUiUpdateTimer();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing playback for {song.FilePath}: {ex.Message}");
            IsPlaying = false;
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            CleanUpPlaybackResources();
        }
    }

    private void StartUiUpdateTimer() => uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private void StopUiUpdateTimer() => uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);

    private void CleanUpPlaybackResources()
    {
        StopUiUpdateTimer();

        if (_streamHandle != 0)
        {
            Bass.ChannelRemoveFX(_streamHandle, _tempoEffectHandle);
            Bass.ChannelRemoveFX(_streamHandle, _pitchEffectHandle);
            Bass.StreamFree(_streamHandle);
            _streamHandle = 0;
            _tempoEffectHandle = 0;
            _pitchEffectHandle = 0;
        }
        Bass.Free();
    }

    private void OnPlaybackStopped(int handle, int channel, int data, IntPtr user)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (IsPlaying || (_streamHandle != 0 && Bass.ChannelIsPlaying(_streamHandle) == PlaybackState.Stopped))
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
            StopUiUpdateTimer();
            // Bass.Net doesn't provide exception in Stopped event, check Bass.LastError after stopping if needed.
        });
    }

    public void Pause()
    {
        if (IsPlaying && _streamHandle != 0 && Bass.ChannelIsPlaying(_streamHandle) == PlaybackState.Playing)
        {
            Bass.ChannelPause(_streamHandle);
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer();
        }
    }

    public void Resume()
    {
        if (!IsPlaying && CurrentSong != null)
        {
            if (_streamHandle == 0 || Bass.ChannelIsPlaying(_streamHandle) == PlaybackState.Stopped)
            {
                TimeSpan resumePosition = CurrentPosition;

                try
                {
                    InitializeBassPipeline(CurrentSong.FilePath);
                    if (_streamHandle != 0)
                    {
                        Seek(resumePosition);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error re-initializing for resume: {ex.Message}");
                    CleanUpPlaybackResources();
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    return;
                }
            }
            if (_streamHandle != 0)
            {
                Bass.ChannelPlay(_streamHandle, false);
                IsPlaying = true;
                CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                StartUiUpdateTimer();
            }
        }
    }

    public void Stop()
    {
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        if (_streamHandle != 0)
        {
            Bass.ChannelStop(_streamHandle);
        }
        CleanUpPlaybackResources();
        CurrentPosition = TimeSpan.Zero;
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (_streamHandle != 0)
        {
            var targetPosition = positionInTrueTime;
            if (targetPosition < TimeSpan.Zero) targetPosition = TimeSpan.Zero;
            // Bass.Net handles seeking beyond total time gracefully, so no need to check against total time here.

            long positionBytes = Bass.ChannelSeconds2Bytes(_streamHandle, targetPosition.TotalSeconds);
            Bass.ChannelSetPosition(_streamHandle, positionBytes);
            CurrentPosition = TimeSpan.FromSeconds(Bass.ChannelBytes2Seconds(_streamHandle, Bass.ChannelGetPosition(_streamHandle)));
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
