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
            if (SetProperty(ref _playbackRate, value))
            {
                // If playing, re-initialize the pipeline to apply the new rate immediately.
                if (IsPlaying && CurrentSong != null)
                {
                    var resumeTime = audioFileReader?.CurrentTime ?? TimeSpan.Zero;
                    Play(CurrentSong); // This will call InitializeNAudioPipeline
                    if (audioFileReader != null) // Check if Play re-initialized successfully
                    {
                        Seek(resumeTime);
                    }
                }
                // If not playing, the new rate is stored and will be used when Play() or Resume() is called.
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
                if (pitchShifter != null)
                {
                    // Update pitch factor considering current playback rate for decoupled pitch/speed
                    float userPitchFactor = (float)Math.Pow(2, _pitchSemitones / 12.0);
                    float rateCompensationFactor = 1.0f;
                    if (PlaybackRate != 0.0f && PlaybackRate != 1.0f)
                    {
                        rateCompensationFactor = 1.0f / PlaybackRate;
                    }
                    pitchShifter.PitchFactor = rateCompensationFactor * userPitchFactor;
                }
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
                    var actualPlaybackTimeInFile = audioFileReader.CurrentTime;
                    if (actualPlaybackTimeInFile >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        audioFileReader.CurrentTime = activeLoop.Start;
                        CurrentPosition = activeLoop.Start;
                    }
                }
            });
        }
    }

    private void InitializeNAudioPipeline(string filePath)
    {
        CleanUpPlaybackResources();

        try
        {
            audioFileReader = new AudioFileReader(filePath);

            ISampleProvider speedAdjustedProvider = AdjustSpeed(audioFileReader, PlaybackRate);

            pitchShifter = new SmbPitchShiftingSampleProvider(speedAdjustedProvider);

            // Calculate combined pitch factor for decoupled speed/pitch control
            float userPitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0);
            float rateCompensationFactor = 1.0f;
            if (PlaybackRate != 0.0f && PlaybackRate != 1.0f) // Avoid division by zero and no-op for 1.0x speed
            {
                rateCompensationFactor = 1.0f / PlaybackRate;
            }
            pitchShifter.PitchFactor = rateCompensationFactor * userPitchFactor;

            finalSampleProvider = pitchShifter;

            waveOutDevice = new WaveOutEvent();
            waveOutDevice.PlaybackStopped += OnPlaybackStopped;
            waveOutDevice.Init(finalSampleProvider);

            CurrentSongDuration = audioFileReader.TotalTime;
            CurrentPosition = TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error initializing NAudio pipeline for {filePath}: {ex.Message}");
            CleanUpPlaybackResources(); // Ensure cleanup on error
            // Optionally, propagate error or set error state
            throw; // Re-throw to allow Play method to catch and handle UI updates
        }
    }

    private ISampleProvider AdjustSpeed(AudioFileReader reader, float speed)
    {
        if (Math.Abs(speed - 1.0f) < 0.001f) // Compare float with tolerance
            return reader.ToSampleProvider();

        int newSampleRate = (int)(reader.WaveFormat.SampleRate * speed);
        // Ensure newSampleRate is valid, e.g. > 0. Some codecs might have min/max sample rates.
        if (newSampleRate <= 0) newSampleRate = reader.WaveFormat.SampleRate; // Fallback or error

        var outFormat = new WaveFormat(newSampleRate, reader.WaveFormat.Channels);

        var resampler = new MediaFoundationResampler(reader, outFormat)
        {
            ResamplerQuality = 60 // Max quality
        };

        return resampler.ToSampleProvider();
    }

    public void Play(Song song)
    {
        if (song == null || string.IsNullOrEmpty(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] Play called with null or invalid song.");
            return;
        }

        // If it's the same song and already playing/paused, this might be a rate/pitch change request
        bool isRateOrPitchChange = (CurrentSong == song && (IsPlaying || CurrentPlaybackStatus == PlaybackStateStatus.Paused));
        TimeSpan resumePosition = TimeSpan.Zero;

        if (isRateOrPitchChange && audioFileReader != null)
        {
            resumePosition = audioFileReader.CurrentTime;
        }

        CurrentSong = song; // Set current song

        try
        {
            InitializeNAudioPipeline(song.FilePath); // This will use current PlaybackRate and PitchSemitones

            if (isRateOrPitchChange)
            {
                Seek(resumePosition); // Restore position if it was a modification of a playing/paused song
            }

            waveOutDevice?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing playback for {song.FilePath}: {ex.Message}");
            IsPlaying = false;
            // CurrentSong = null; // Avoid resetting if it was just a failed modification
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

        waveOutDevice?.Stop();
        waveOutDevice?.Dispose();
        waveOutDevice = null;

        // pitchShifter is typically disposed if its source is disposable, but AudioFileReader handles its own.
        // No explicit Dispose on ISampleProvider typically.
        pitchShifter = null;
        finalSampleProvider = null;

        audioFileReader?.Dispose();
        audioFileReader = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // This event can fire when Stop() is called, or playback naturally ends, or due to an error.
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Only change state if it wasn't an intentional Stop() that already set these.
            // If IsPlaying is true here, it means playback stopped unexpectedly or finished naturally.
            if (IsPlaying || (waveOutDevice != null && waveOutDevice.PlaybackState == PlaybackState.Stopped && CurrentPlaybackStatus != PlaybackStateStatus.Stopped))
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
            // CurrentPosition = TimeSpan.Zero; // Only reset if playback naturally ended, not if it was paused.
            // If playback finished naturally, CurrentPosition would be at Duration.
            // If stopped manually via Stop(), CurrentPosition is already set to Zero.

            StopUiUpdateTimer();
            if (e.Exception != null)
            {
                Console.WriteLine($"NAudio Playback Error: {e.Exception.Message}");
                // Potentially set an error state in the UI
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
            // If paused, just play
            if (waveOutDevice != null && audioFileReader != null && waveOutDevice.PlaybackState == PlaybackState.Paused)
            {
                waveOutDevice.Play();
                IsPlaying = true;
                CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                StartUiUpdateTimer();
            }
            // If stopped, re-initialize and play (this will also pick up any rate/pitch changes)
            else if (waveOutDevice == null || audioFileReader == null || waveOutDevice.PlaybackState == PlaybackState.Stopped)
            {
                TimeSpan resumePosition = CurrentPosition; // Use the last known CurrentPosition
                Play(CurrentSong); // Re-initializes and starts playing
                if (audioFileReader != null) // Check if Play was successful
                {
                    Seek(resumePosition); // Seek to where it was
                }
            }
        }
    }

    public void Stop()
    {
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        // CleanUpPlaybackResources will stop and dispose waveOutDevice
        CleanUpPlaybackResources();
        CurrentPosition = TimeSpan.Zero; // Reset position on stop
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (audioFileReader != null)
        {
            var targetPosition = positionInTrueTime;
            if (targetPosition < TimeSpan.Zero) targetPosition = TimeSpan.Zero;
            if (targetPosition > audioFileReader.TotalTime) targetPosition = audioFileReader.TotalTime;

            audioFileReader.CurrentTime = targetPosition;
            CurrentPosition = targetPosition; // Update public property
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