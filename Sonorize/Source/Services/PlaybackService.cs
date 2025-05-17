using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Diagnostics;
using System.IO; // For Path.GetFileName
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
            {
                // Seek operates on the original audioFileReader's timeline
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
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1;

    private IWavePlayer? waveOutDevice;
    private AudioFileReader? audioFileReader;
    private VarispeedSampleProvider? varispeedProvider; // Using VarispeedSampleProvider
    // private SmbPitchShiftingSampleProvider? pitchShifter; // Still bypassed
    private ISampleProvider? finalSampleProvider;
    private Timer? uiUpdateTimer;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            if (value <= 0.01f) value = 0.01f; // Varispeed might not like 0 or too small values

            if (Math.Abs(_playbackRate - value) < 0.001f) return;

            if (SetProperty(ref _playbackRate, value))
            {
                Debug.WriteLine($"[PlaybackService] PlaybackRate property set to: {value}. Current _playbackRate field: {this._playbackRate}");
                if (CurrentSong != null)
                {
                    ReconfigurePipeline();
                }
            }
        }
    }

    private float _pitchSemitones = 0f;
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            if (float.IsNaN(value) || float.IsInfinity(value)) return;
            if (Math.Abs(_pitchSemitones - value) < 0.001f) return;

            if (SetProperty(ref _pitchSemitones, value))
            {
                Debug.WriteLine($"[PlaybackService] PitchSemitones set to: {value}. Pitch Shifter is BYPASSED for this test.");
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
                // CurrentPosition should reflect the progress in the *original* file
                CurrentPosition = audioFileReader.CurrentTime;

                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    // Compare with original file's timeline
                    if (audioFileReader.CurrentTime >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        audioFileReader.CurrentTime = activeLoop.Start;
                        CurrentPosition = activeLoop.Start; // Sync ViewModel
                    }
                }
            });
        }
    }

    private bool TryInitializeNAudioPipeline(string filePath, TimeSpan? initialPosition = null)
    {
        CleanUpPlaybackResources();

        try
        {
            audioFileReader = new AudioFileReader(filePath);
            Debug.WriteLine($"[PlaybackService] TryInit: audioFileReader.WaveFormat: SR={audioFileReader.WaveFormat.SampleRate}, Ch={audioFileReader.WaveFormat.Channels}");

            // Initialize VarispeedSampleProvider
            // The second argument (readBufferSize) can be 0 for default.
            // VarispeedSampleProvider takes an ISampleProvider. AudioFileReader is one.
            varispeedProvider = new VarispeedSampleProvider(audioFileReader, 0, _playbackRate);
            Debug.WriteLine($"[PlaybackService] TryInit: varispeedProvider created. Rate: {_playbackRate}. Input SR: {audioFileReader.WaveFormat.SampleRate}, Output SR: {varispeedProvider.WaveFormat.SampleRate}");

            finalSampleProvider = varispeedProvider;
            Debug.WriteLine($"[PlaybackService] TryInit: Pitch Shifter BYPASSED. finalSampleProvider is varispeedProvider.");

            waveOutDevice = new WaveOutEvent();
            waveOutDevice.PlaybackStopped += OnPlaybackStopped;
            waveOutDevice.Init(finalSampleProvider); // WaveOutEvent uses VarispeedSampleProvider's output

            CurrentSongDuration = audioFileReader.TotalTime; // Duration of the original file

            if (initialPosition.HasValue)
            {
                TimeSpan pos = initialPosition.Value;
                // Seek directly on audioFileReader. VarispeedSampleProvider will read from this new position.
                pos = TimeSpan.FromSeconds(Math.Clamp(pos.TotalSeconds, 0, audioFileReader.TotalTime.TotalSeconds));
                audioFileReader.CurrentTime = pos;
            }
            else
            {
                audioFileReader.CurrentTime = TimeSpan.Zero;
            }
            CurrentPosition = audioFileReader.CurrentTime; // Reflects position in original file

            Debug.WriteLine($"[PlaybackService] TryInitializeNAudioPipeline SUCCESS for {Path.GetFileName(filePath)}. Varispeed Rate: {varispeedProvider.PlaybackRate}, WaveOut SR: {finalSampleProvider.WaveFormat.SampleRate}, Pos: {CurrentPosition}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] TryInitializeNAudioPipeline FAILED for {Path.GetFileName(filePath)}: {ex.ToString()}");
            CleanUpPlaybackResources();
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            return false;
        }
    }

    private void ReconfigurePipeline()
    {
        if (CurrentSong == null || string.IsNullOrEmpty(CurrentSong.FilePath))
        {
            Debug.WriteLine("[PlaybackService] ReconfigurePipeline called with no current song or path.");
            return;
        }

        Debug.WriteLine($"[PlaybackService] Reconfiguring pipeline. Target Rate: {this._playbackRate}. Current Status: {CurrentPlaybackStatus}, IsPlaying: {IsPlaying}");

        PlaybackStateStatus oldStatus = CurrentPlaybackStatus;
        TimeSpan resumePositionInOriginalFile = audioFileReader?.CurrentTime ?? CurrentPosition;

        // VarispeedSampleProvider's PlaybackRate is typically set at construction.
        // While it has a setter, to be safe and ensure all internal buffers/states are correct,
        // a full pipeline rebuild is more robust for rate changes.

        if (waveOutDevice != null)
        {
            waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
            waveOutDevice.Stop();
        }
        StopUiUpdateTimer();
        CleanUpPlaybackResources(); // Full cleanup

        bool success = TryInitializeNAudioPipeline(CurrentSong.FilePath, resumePositionInOriginalFile);

        if (success)
        {
            Debug.WriteLine($"[PlaybackService] Reconfigure success. Restoring old status: {oldStatus}");
            if (oldStatus == PlaybackStateStatus.Playing)
            {
                waveOutDevice?.Play();
                IsPlaying = true;
                CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                StartUiUpdateTimer();
            }
            else if (oldStatus == PlaybackStateStatus.Paused)
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            }
            else
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
            Debug.WriteLine($"[PlaybackService] Reconfigure complete. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}, Pos: {CurrentPosition}, Varispeed Rate: {varispeedProvider?.PlaybackRate}");
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] ReconfigurePipeline failed. Setting to Stopped.");
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        }
    }

    public void Play(Song song)
    {
        if (song == null || string.IsNullOrEmpty(song.FilePath)) return;
        Debug.WriteLine($"[PlaybackService] Play called for: {Path.GetFileName(song.FilePath)}");

        if (waveOutDevice != null)
        {
            waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
            waveOutDevice.Stop();
        }
        StopUiUpdateTimer();
        CleanUpPlaybackResources();

        CurrentSong = song;

        if (TryInitializeNAudioPipeline(song.FilePath, TimeSpan.Zero))
        {
            waveOutDevice?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
        else
        {
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            CurrentSong = null;
        }
        Debug.WriteLine($"[PlaybackService] Play ended. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}");
    }

    private void StartUiUpdateTimer() => uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private void StopUiUpdateTimer() => uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);

    private void CleanUpPlaybackResources()
    {
        waveOutDevice?.Stop();
        if (waveOutDevice != null)
        {
            waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
            waveOutDevice.Dispose();
            waveOutDevice = null;
        }

        // VarispeedSampleProvider does not implement IDisposable.
        varispeedProvider = null;
        finalSampleProvider = null;

        if (audioFileReader != null)
        {
            audioFileReader.Dispose();
            audioFileReader = null;
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Debug.WriteLine($"[PlaybackService] OnPlaybackStopped event. Current Status Before: {CurrentPlaybackStatus}, IsPlaying Before: {IsPlaying}. Exception: {e.Exception?.Message}");

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            bool wasPlayingOrIntendingToPlay = (CurrentPlaybackStatus == PlaybackStateStatus.Playing);

            IsPlaying = false;
            StopUiUpdateTimer();

            if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] NAudio Playback Error in OnPlaybackStopped: {e.Exception.Message}");
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
            else if (wasPlayingOrIntendingToPlay || CurrentPlaybackStatus == PlaybackStateStatus.Paused)
            {
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                // Check if it was a natural end of song
                if (wasPlayingOrIntendingToPlay && audioFileReader != null && audioFileReader.Position >= audioFileReader.Length - audioFileReader.WaveFormat.BlockAlign && audioFileReader.Length > 0)
                {
                    CurrentPosition = TimeSpan.Zero;
                }
            }

            Debug.WriteLine($"[PlaybackService] OnPlaybackStopped processed. New Status: {CurrentPlaybackStatus}, IsPlaying: {IsPlaying}, CurrentPos: {CurrentPosition}");
        });
    }

    public void Pause()
    {
        if (IsPlaying && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            Debug.WriteLine("[PlaybackService] Pause called.");
            waveOutDevice.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Pause ended. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}");
        }
    }

    public void Resume()
    {
        if (!IsPlaying && CurrentSong != null)
        {
            Debug.WriteLine($"[PlaybackService] Resume called. Current Status: {CurrentPlaybackStatus}");
            TimeSpan resumePosition = CurrentPosition; // Position in original file

            if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped || waveOutDevice == null || audioFileReader == null)
            {
                Debug.WriteLine("[PlaybackService] Resume: Re-initializing pipeline because state was Stopped or pipeline null.");
                CleanUpPlaybackResources();
                if (!TryInitializeNAudioPipeline(CurrentSong.FilePath, resumePosition))
                {
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    CurrentSong = null;
                    Debug.WriteLine("[PlaybackService] Resume: Re-initialization failed.");
                    return;
                }
            }
            // If Paused, pipeline should be intact, just play.
            waveOutDevice?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Resume ended. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}");
        }
    }

    public void Stop()
    {
        Debug.WriteLine("[PlaybackService] Stop called.");
        if (waveOutDevice != null)
        {
            waveOutDevice.Stop(); // Will trigger OnPlaybackStopped
        }
        // Set state immediately, OnPlaybackStopped will confirm/refine
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        CurrentPosition = TimeSpan.Zero; // Stop implies reset to beginning
        StopUiUpdateTimer();
        Debug.WriteLine($"[PlaybackService] Stop ended. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}");
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (audioFileReader != null)
        {
            var targetPosition = TimeSpan.FromSeconds(Math.Clamp(positionInTrueTime.TotalSeconds, 0, audioFileReader.TotalTime.TotalSeconds));
            audioFileReader.CurrentTime = targetPosition; // Seek on the base reader
            CurrentPosition = targetPosition; // Update ViewModel property immediately
            Debug.WriteLine($"[PlaybackService] Seek to (original file time): {CurrentPosition}");
        }
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose called.");
        CleanUpPlaybackResources();
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
        GC.SuppressFinalize(this);
    }
}