using Avalonia.Threading;
using NAudio.Wave;
using Sonorize.Models;
using Sonorize.ViewModels;
using System.Diagnostics;
using System.Threading;
using System;
using System.IO;

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
            if (SetProperty(ref _currentSong, value))
            {
                Debug.WriteLine($"[PlaybackService] CurrentSong property set to: {value?.Title ?? "null"}");
                OnPropertyChanged(nameof(HasCurrentSong));
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                IsPlaying = false;
                CurrentPosition = TimeSpan.Zero;
                CurrentSongDuration = TimeSpan.Zero;
                StopUiUpdateTimer();

                // Update the LoopHandler when the current song changes
                _loopHandler.UpdateCurrentSong(value);
            }
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
        private set
        {
            if (SetProperty(ref _currentPosition, value))
            {
                OnPropertyChanged(nameof(CurrentPositionSeconds));
            }
        }
    }
    public double CurrentPositionSeconds => CurrentPosition.TotalSeconds;

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
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1.0;

    private NAudioPlaybackEngine? _playbackEngine;
    private Timer? uiUpdateTimer;
    private readonly PlaybackLoopHandler _loopHandler; // Instance of the new loop handler

    private volatile bool _explicitStopRequested = false;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (SetProperty(ref _playbackRate, value))
            {
                if (_playbackEngine != null) _playbackEngine.PlaybackRate = value;
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
                if (_playbackEngine != null) _playbackEngine.PitchSemitones = value;
            }
        }
    }

    public event EventHandler? PlaybackEndedNaturally;

    public PlaybackService()
    {
        Debug.WriteLine("[PlaybackService] Constructor called.");
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
        _loopHandler = new PlaybackLoopHandler(this); // Initialize the loop handler, injecting this service
    }

    private void UpdateUiCallback(object? state)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_playbackEngine != null && _playbackEngine.CurrentPlaybackStatus == PlaybackStateStatus.Playing && CurrentSong != null)
            {
                TimeSpan currentAudioTime = TimeSpan.Zero;
                TimeSpan songDuration = TimeSpan.Zero;

                try
                {
                    currentAudioTime = _playbackEngine.CurrentPosition;
                    songDuration = _playbackEngine.CurrentSongDuration; // Get duration from engine
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlaybackService] Error getting Engine.CurrentPosition/Duration in timer callback: {ex.Message}. Stopping timer.");
                    StopUiUpdateTimer();
                    return;
                }

                this.CurrentPosition = currentAudioTime;
                this.CurrentSongDuration = songDuration; // Keep VM duration in sync with engine

                // Delegate loop checking to the LoopHandler
                _loopHandler.CheckForLoopSeek(currentAudioTime, songDuration);
            }
            else
            {
                if (_playbackEngine == null || _playbackEngine.CurrentPlaybackStatus != PlaybackStateStatus.Playing)
                {
                    Debug.WriteLine($"[PlaybackService] Timer callback found engine state is not Playing ({_playbackEngine?.CurrentPlaybackStatus}). Stopping timer.");
                    StopUiUpdateTimer();
                }
            }
        });
    }

    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService] Play requested for: {(song?.Title ?? "null song")}");

        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] Play called with null/invalid/missing file song. Stopping.");
            Stop();
            return;
        }

        if (_playbackEngine != null)
        {
            _playbackEngine.Stop();
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
            Debug.WriteLine("[PlaybackService] Disposed existing playback engine.");
        }

        CurrentSong = song; // Setting CurrentSong updates _loopHandler via property setter

        _explicitStopRequested = false;

        try
        {
            _playbackEngine = new NAudioPlaybackEngine();
            _playbackEngine.PlaybackStopped += OnEnginePlaybackStopped;

            _playbackEngine.PlaybackRate = PlaybackRate;
            _playbackEngine.PitchSemitones = PitchSemitones;

            _playbackEngine.Load(song.FilePath);

            // Update duration after loading
            CurrentSongDuration = _playbackEngine.CurrentSongDuration;
            this.CurrentPosition = TimeSpan.Zero; // Engine Load resets position

            // Ask LoopHandler for the initial position to start playback
            TimeSpan startPosition = _loopHandler.GetInitialPlaybackPosition(_playbackEngine.CurrentSongDuration);
            if (startPosition != TimeSpan.Zero)
            {
                Debug.WriteLine($"[PlaybackService] Starting playback at initial position determined by LoopHandler: {startPosition:mm\\:ss\\.ff}");
                _playbackEngine.Seek(startPosition);
                this.CurrentPosition = _playbackEngine.CurrentPosition; // Sync VM position
            }
            else
            {
                Debug.WriteLine("[PlaybackService] Starting playback from the beginning (0).");
            }


            _playbackEngine.Play();

            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}. State: {CurrentPlaybackStatus}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during playback initiation for {Path.GetFileName(song.FilePath)}: {ex.ToString()}");
            Stop();
        }
    }

    public void Pause()
    {
        Debug.WriteLine($"[PlaybackService] Pause requested. Current state: {CurrentPlaybackStatus}");
        if (_playbackEngine != null && IsPlaying)
        {
            _playbackEngine.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer();
        }
    }

    public void Resume()
    {
        Debug.WriteLine($"[PlaybackService] Resume requested. Current Status: {CurrentPlaybackStatus}, HasSong: {HasCurrentSong}");

        if (CurrentSong == null)
        {
            Debug.WriteLine("[PlaybackService] Resume requested but no CurrentSong is set. Cannot resume.");
            return;
        }

        if (_playbackEngine != null && CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Paused state. Resuming engine.");
            _playbackEngine.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
        else if (_playbackEngine != null && CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Stopped state. Re-playing current song via engine.");
            Play(CurrentSong);
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Resume requested but conditions not met. Current Status: {CurrentPlaybackStatus}. Doing nothing.");
        }
    }

    public void Stop()
    {
        Debug.WriteLine("[PlaybackService] Public Stop() called.");
        _explicitStopRequested = true;
        if (_playbackEngine != null)
        {
            _playbackEngine.Stop();
        }
    }

    public void Seek(TimeSpan requestedPosition)
    {
        if (_playbackEngine == null || CurrentSong == null)
        {
            Debug.WriteLine($"[PlaybackService] Seek ignored: No active engine or current song. Engine: {_playbackEngine != null}, Song: {CurrentSong != null}");
            return;
        }

        // Ask LoopHandler for the adjusted position considering active loop
        TimeSpan targetPosition = _loopHandler.GetAdjustedSeekPosition(requestedPosition, _playbackEngine.CurrentSongDuration);

        // Clamp targetPosition to a valid range within the audio file's total duration.
        var totalMs = _playbackEngine.CurrentSongDuration.TotalMilliseconds;
        var seekMarginMs = totalMs > 200 ? 100 : (totalMs > 0 ? Math.Min(totalMs / 2, 50) : 0);
        var maxSeekablePosition = TimeSpan.FromMilliseconds(totalMs - seekMarginMs);
        if (maxSeekablePosition < TimeSpan.Zero) maxSeekablePosition = TimeSpan.Zero;

        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, maxSeekablePosition.TotalSeconds));

        // Add a small tolerance check to avoid seeking if the target is very close to the current position.
        double positionToleranceSeconds = 0.3; // 300 milliseconds tolerance

        try
        {
            TimeSpan currentAudioTimeForToleranceCheck = _playbackEngine.CurrentPosition;

            if (Math.Abs(currentAudioTimeForToleranceCheck.TotalSeconds - targetPosition.TotalSeconds) < positionToleranceSeconds)
            {
                Debug.WriteLine($"[PlaybackService] Seek target {targetPosition:mm\\:ss\\.ff} is very close to current position {currentAudioTimeForToleranceCheck:mm\\:ss\\.ff} (within {positionToleranceSeconds}s), ignoring seek.");
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error checking current position for seek tolerance: {ex.Message}. Proceeding with seek.");
        }

        Debug.WriteLine($"[PlaybackService] Seeking engine to: {targetPosition:mm\\:ss\\.ff}");
        try
        {
            _playbackEngine.Seek(targetPosition);
            this.CurrentPosition = _playbackEngine.CurrentPosition;
            Debug.WriteLine($"[PlaybackService] Seek executed. Engine Time after seek: {_playbackEngine.CurrentPosition:mm\\:ss\\.ff}. VM Position: {this.CurrentPosition:mm\\:ss\\.ff}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL Error during Engine Seek to {targetPosition:mm\\:ss\\.ff}: {ex.Message}");
            if (_playbackEngine != null)
            {
                try { this.CurrentPosition = _playbackEngine.CurrentPosition; }
                catch (Exception readEx) { Debug.WriteLine($"[PlaybackService] Error reading position after failed seek: {readEx.Message}"); }
            }
            else
            {
                this.CurrentPosition = TimeSpan.Zero;
            }
        }
    }

    private void OnEnginePlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Debug.WriteLine($"[PlaybackService] === OnEnginePlaybackStopped START === (UI Thread)");
            Debug.WriteLine($"[PlaybackService] Exception: {e.Exception?.Message ?? "None"}");
            Debug.WriteLine($"[PlaybackService] _explicitStopRequested: {_explicitStopRequested}");

            StopUiUpdateTimer();

            if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] Playback stopped due to error: {e.Exception.Message}. Finalizing state to Stopped.");
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                this.CurrentPosition = TimeSpan.Zero;
                CurrentSongDuration = TimeSpan.Zero;
                CurrentSong = null;
                _explicitStopRequested = false;
                Debug.WriteLine("[PlaybackService] State set to Stopped by error handler.");
            }
            else
            {
                // Determine if it was a natural end of file based on engine position
                bool isNearEndOfFile = (_playbackEngine?.CurrentPosition ?? TimeSpan.Zero) >= (_playbackEngine?.CurrentSongDuration ?? TimeSpan.Zero) - TimeSpan.FromMilliseconds(200);

                Debug.WriteLine($"[PlaybackService] Clean Stop. Was Explicit Stop: {_explicitStopRequested}. Is Near End of File: {isNearEndOfFile}. Engine Position: {(_playbackEngine?.CurrentPosition ?? TimeSpan.Zero):mm\\:ss\\.ff}, Engine Duration: {(_playbackEngine?.CurrentSongDuration ?? TimeSpan.Zero):mm\\:ss\\.ff}");


                if (_explicitStopRequested)
                {
                    Debug.WriteLine("[PlaybackService] Playback stopped by explicit user command (event). Finalizing state to Stopped.");
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    this.CurrentPosition = TimeSpan.Zero;
                    CurrentSongDuration = TimeSpan.Zero;
                    CurrentSong = null;
                    _explicitStopRequested = false;
                    Debug.WriteLine("[PlaybackService] State set to Stopped by explicit stop handler.");
                }
                else if (isNearEndOfFile)
                {
                    Debug.WriteLine("[PlaybackService] Playback stopped naturally (event). Raising PlaybackEndedNaturally event.");
                    this.CurrentPosition = TimeSpan.Zero;
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    PlaybackEndedNaturally?.Invoke(this, EventArgs.Empty);
                    Debug.WriteLine("[PlaybackService] State set to Stopped by natural end handler, event raised.");
                }
                else
                {
                    Debug.WriteLine("Playback stopped by interruption (likely Play called). State managed by calling Play() method.");
                }
            }
            Debug.WriteLine("[PlaybackService] OnEnginePlaybackStopped handler finishes.");
        });
    }

    private void StartUiUpdateTimer()
    {
        uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        Debug.WriteLine("[PlaybackService] UI Update Timer Started.");
    }

    private void StopUiUpdateTimer()
    {
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Debug.WriteLine("[PlaybackService] UI Update Timer Stopped.");
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;

        if (_playbackEngine != null)
        {
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
            Debug.WriteLine("[PlaybackService] Disposed playback engine during service dispose.");
        }

        _explicitStopRequested = false;
        CurrentSong = null; // Setting song to null cleans up loop handler
        CurrentSongDuration = TimeSpan.Zero;
        this.CurrentPosition = TimeSpan.Zero;
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;

        // Dispose the loop handler as it holds a reference to this service
        _loopHandler.Dispose(); // Ensure loop handler is also disposable if needed (it doesn't hold unmanaged resources directly, but it's good practice if its lifecycle matches the service)

        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackService] Dispose() completed.");
    }

    ~PlaybackService()
    {
        Debug.WriteLine("[PlaybackService] Finalizer called for PlaybackService.");
        Dispose();
        Debug.WriteLine("[PlaybackService] Finalizer completed for PlaybackService.");
    }
}