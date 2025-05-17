using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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

    private IWavePlayer? waveOutDevice;
    private AudioFileReader? audioFileReader;
    private ISampleProvider? finalSampleProvider;
    private SmbPitchShiftingSampleProvider? pitchShifter;
    private Timer? uiUpdateTimer;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            float newRate = value; // ViewModel already clamps this
            if (Math.Abs(_playbackRate - newRate) < 0.001f) return; // No significant change

            _playbackRate = newRate;
            OnPropertyChanged(); // Notify UI of the change in the underlying property

            if (CurrentSong != null && (IsPlaying || CurrentPlaybackStatus == PlaybackStateStatus.Paused))
            {
                TimeSpan currentTime = audioFileReader?.CurrentTime ?? CurrentPosition;
                bool wasPlaying = IsPlaying;
                PlaybackStateStatus previousStatus = CurrentPlaybackStatus;

                CleanUpPlaybackResources(); // Stops playback, disposes resources

                try
                {
                    InitializeNAudioPipeline(CurrentSong.FilePath); // Re-initializes with new _playbackRate

                    if (audioFileReader != null)
                    {
                        audioFileReader.CurrentTime = currentTime;
                        CurrentPosition = currentTime; // Ensure UI property is updated
                    }

                    if (wasPlaying && waveOutDevice != null)
                    {
                        waveOutDevice.Play();
                        IsPlaying = true;
                        CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                        StartUiUpdateTimer();
                    }
                    else if (previousStatus == PlaybackStateStatus.Paused && waveOutDevice != null)
                    {
                        // Pipeline is ready, but remain paused.
                        IsPlaying = false;
                        CurrentPlaybackStatus = PlaybackStateStatus.Paused;
                        // UI timer remains stopped for paused state.
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error re-initializing playback for rate change: {ex.Message}");
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    CurrentSong = null; // Critical error, stop playback
                    CleanUpPlaybackResources();
                }
            }
            // If stopped, the new _playbackRate will be picked up on the next Play() call
        }
    }

    private float _pitchSemitones = 0f;
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            if (Math.Abs(_pitchSemitones - value) < 0.001f) return;
            _pitchSemitones = value;
            OnPropertyChanged();

            if (pitchShifter != null && _playbackRate > 0) // PlaybackRate must be positive
            {
                double desiredPitchFactor = Math.Pow(2, _pitchSemitones / 12.0);
                pitchShifter.PitchFactor = (float)(desiredPitchFactor / _playbackRate);
            }
        }
    }

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

                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    if (audioFileReader.CurrentTime >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        audioFileReader.CurrentTime = activeLoop.Start;
                        CurrentPosition = activeLoop.Start; // Update UI property
                    }
                }
            });
        }
    }

    private ISampleProvider ApplySpeedChange(AudioFileReader reader, float speed)
    {
        if (Math.Abs(speed - 1.0f) < 0.001f)
        {
            return reader.ToSampleProvider(); // No speed change, return original
        }

        var outFormat = new WaveFormat((int)(reader.WaveFormat.SampleRate * speed),
                                       reader.WaveFormat.BitsPerSample,
                                       reader.WaveFormat.Channels);

        var resampler = new MediaFoundationResampler(reader, outFormat)
        {
            ResamplerQuality = 60 // Max quality
        };
        return resampler.ToSampleProvider();
    }

    private void InitializeNAudioPipeline(string filePath)
    {
        CleanUpPlaybackResources(); // Ensures previous instances are disposed

        audioFileReader = new AudioFileReader(filePath);
        ISampleProvider providerToPitchShift = ApplySpeedChange(audioFileReader, _playbackRate);

        pitchShifter = new SmbPitchShiftingSampleProvider(providerToPitchShift);
        if (_playbackRate > 0) // Avoid division by zero if _playbackRate somehow became invalid
        {
            double desiredPitchFactor = Math.Pow(2, PitchSemitones / 12.0);
            pitchShifter.PitchFactor = (float)(desiredPitchFactor / _playbackRate);
        }
        else
        {
            pitchShifter.PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0); // Fallback if rate is invalid
        }

        finalSampleProvider = pitchShifter;

        waveOutDevice = new WaveOutEvent();
        waveOutDevice.PlaybackStopped += OnPlaybackStopped;
        waveOutDevice.Init(finalSampleProvider);

        CurrentSongDuration = audioFileReader.TotalTime;
        CurrentPosition = TimeSpan.Zero;
    }

    public void Play(Song song)
    {
        if (song == null) return;

        // If it's the same song and already paused, treat as Resume
        if (CurrentSong == song && CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            Resume();
            return;
        }

        CurrentSong = song;

        try
        {
            InitializeNAudioPipeline(song.FilePath);
            waveOutDevice?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error initializing playback for {song.FilePath}: {ex.Message}");
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            CurrentSong = null;
            CleanUpPlaybackResources();
        }
    }

    private void StartUiUpdateTimer() => uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private void StopUiUpdateTimer() => uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);

    private void CleanUpPlaybackResources()
    {
        StopUiUpdateTimer();

        waveOutDevice?.Stop(); // Stop playback
        waveOutDevice?.Dispose();
        waveOutDevice = null;

        // pitchShifter is disposed implicitly if it's just a pass-through to finalSampleProvider
        // which itself might wrap disposable resources from audioFileReader via resampler.
        // However, SmbPitchShiftingSampleProvider itself is not IDisposable.
        // MediaFoundationResampler *is* IDisposable.
        // AudioFileReader *is* IDisposable.

        // The resampler (if created in ApplySpeedChange) is wrapped by ToSampleProvider()
        // and then given to SmbPitchShiftingSampleProvider. It's not directly held to be disposed.
        // The AudioFileReader is the primary disposable resource here that needs explicit handling.
        pitchShifter = null; // Allow GC
        finalSampleProvider = null; // Allow GC

        audioFileReader?.Dispose();
        audioFileReader = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // This event can fire for multiple reasons:
        // 1. Naturally reached end of stream.
        // 2. waveOutDevice.Stop() was called.
        // 3. An error occurred during playback.

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Only change state if it wasn't an intentional stop/cleanup already handled
            if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped || IsPlaying)
            {
                // If it stopped due to end of file or error, not manual stop
                if (audioFileReader != null && audioFileReader.CurrentTime >= audioFileReader.TotalTime)
                {
                    // Reached end of song
                    CurrentPosition = CurrentSongDuration; // Ensure slider goes to end
                }
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }

            StopUiUpdateTimer();

            if (e.Exception != null)
            {
                Debug.WriteLine($"NAudio Playback Error: {e.Exception.Message}");
                // Potentially set an error state in UI or log more formally
            }
        });
    }

    public void Pause()
    {
        if (IsPlaying && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            waveOutDevice.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer();
        }
    }

    public void Resume()
    {
        if (!IsPlaying && CurrentSong != null)
        {
            // If pipeline is not initialized (e.g., after Stop() or if Play() was never called for this song)
            // or if it was stopped due to an error.
            if (waveOutDevice == null || audioFileReader == null || waveOutDevice.PlaybackState == PlaybackState.Stopped)
            {
                TimeSpan resumePosition = CurrentPosition; // Use CurrentPosition as it's maintained
                try
                {
                    InitializeNAudioPipeline(CurrentSong.FilePath);
                    if (audioFileReader != null)
                    {
                        audioFileReader.CurrentTime = resumePosition;
                        CurrentPosition = resumePosition; // Update UI property
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error re-initializing for resume: {ex.Message}");
                    CleanUpPlaybackResources();
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    IsPlaying = false;
                    CurrentSong = null;
                    return;
                }
            }

            waveOutDevice?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
    }

    public void Stop()
    {
        IsPlaying = false; // Set first to influence OnPlaybackStopped logic if Stop calls it
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        // waveOutDevice.Stop() will trigger OnPlaybackStopped if device exists
        // CleanUpPlaybackResources also calls waveOutDevice.Stop()
        CleanUpPlaybackResources();
        CurrentPosition = TimeSpan.Zero; // Reset position
        // CurrentSong remains, so UI still shows it as loaded but stopped.
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (audioFileReader != null)
        {
            var targetPosition = positionInTrueTime;
            if (targetPosition < TimeSpan.Zero) targetPosition = TimeSpan.Zero;
            if (targetPosition > audioFileReader.TotalTime) targetPosition = audioFileReader.TotalTime;

            audioFileReader.CurrentTime = targetPosition;
            CurrentPosition = targetPosition; // Update UI property
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