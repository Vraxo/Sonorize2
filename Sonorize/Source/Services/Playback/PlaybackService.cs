using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Threading;
using NAudio.Wave;
using Sonorize.Models;
using Sonorize.Services.Playback; // Added for PlaybackCompletionHandler
using Sonorize.ViewModels;

namespace Sonorize.Services;

public enum PlaybackStateStatus { Stopped, Playing, Paused }

public class PlaybackService : ViewModelBase, IDisposable
{
    private Song? _currentSong;
    public Song? CurrentSong
    {
        get => _currentSong;
        private set // Remains private to enforce control via Play/Stop/SetCurrentSongInternal
        {
            if (SetProperty(ref _currentSong, value))
            {
                Debug.WriteLine($"[PlaybackService] CurrentSong property set to: {value?.Title ?? "null"}");
                OnPropertyChanged(nameof(HasCurrentSong));

                if (value == null) // CurrentSong is being cleared
                {
                    Debug.WriteLine("[PlaybackService] CurrentSong set to null. Resetting playback state variables.");
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    CurrentPosition = TimeSpan.Zero;
                    CurrentSongDuration = TimeSpan.Zero;
                    StopUiUpdateTimerInternal();
                }
                // Update loop handler regardless of null or not
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
    private readonly PlaybackLoopHandler _loopHandler;
    private readonly ScrobblingService _scrobblingService;
    private readonly PlaybackCompletionHandler _completionHandler;


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

    public PlaybackService(ScrobblingService scrobblingService)
    {
        Debug.WriteLine("[PlaybackService] Constructor called.");
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
        _loopHandler = new PlaybackLoopHandler(this);
        _completionHandler = new PlaybackCompletionHandler(this, _scrobblingService);
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
                    songDuration = _playbackEngine.CurrentSongDuration;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlaybackService] Error getting Engine.CurrentPosition/Duration in timer callback: {ex.Message}. Stopping timer.");
                    StopUiUpdateTimerInternal();
                    return;
                }

                this.CurrentPosition = currentAudioTime;
                this.CurrentSongDuration = songDuration;

                _loopHandler.CheckForLoopSeek(currentAudioTime, songDuration);
            }
            else
            {
                if (_playbackEngine == null || _playbackEngine.CurrentPlaybackStatus != PlaybackStateStatus.Playing)
                {
                    StopUiUpdateTimerInternal();
                }
            }
        });
    }

    private async void TryScrobbleSong(Song? song, TimeSpan playedPosition)
    {
        if (song == null) return;
        Debug.WriteLine($"[PlaybackService] TryScrobbleSong called for '{song.Title}' at {playedPosition}.");
        if (_scrobblingService.ShouldScrobble(song, playedPosition))
        {
            await _scrobblingService.ScrobbleAsync(song, DateTime.UtcNow);
        }
    }

    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService] Play requested for: {(song?.Title ?? "null song")}");

        Song? interruptedSong = null;
        TimeSpan interruptedSongPosition = TimeSpan.Zero;

        if (_playbackEngine != null && CurrentSong != null && CurrentSong != song)
        {
            interruptedSong = CurrentSong;
            try { interruptedSongPosition = _playbackEngine.CurrentPosition; } catch (Exception ex) { Debug.WriteLine($"[PlaybackService] Error getting position of interrupted song '{interruptedSong.Title}': {ex.Message}"); }

            _playbackEngine.Stop();
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
            Debug.WriteLine($"[PlaybackService] Interrupted '{interruptedSong.Title}'. Old engine stopped & disposed.");
            TryScrobbleSong(interruptedSong, interruptedSongPosition);
        }
        else if (_playbackEngine != null && CurrentSong == song)
        {
            Debug.WriteLine($"[PlaybackService] Restarting song '{song.Title}'. Old engine stopped & disposed.");
            _playbackEngine.Stop();
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
        }

        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] New song is null, path invalid, or file missing. Current playback will be fully stopped.");
            SetCurrentSongInternal(null);
            return;
        }

        SetCurrentSongInternal(song); // Use internal setter
        _explicitStopRequested = false;

        try
        {
            _playbackEngine = new NAudioPlaybackEngine();
            _playbackEngine.PlaybackStopped += OnEnginePlaybackStopped;
            _playbackEngine.PlaybackRate = this.PlaybackRate;
            _playbackEngine.PitchSemitones = this.PitchSemitones;
            _playbackEngine.Load(song.FilePath);

            CurrentSongDuration = _playbackEngine.CurrentSongDuration;
            this.CurrentPosition = TimeSpan.Zero;

            TimeSpan startPosition = _loopHandler.GetInitialPlaybackPosition(CurrentSongDuration);
            if (startPosition > TimeSpan.Zero && startPosition < CurrentSongDuration)
            {
                _playbackEngine.Seek(startPosition);
                this.CurrentPosition = _playbackEngine.CurrentPosition;
            }

            _playbackEngine.Play();
            SetPlaybackStateInternal(true, PlaybackStateStatus.Playing);
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}. State: {CurrentPlaybackStatus}");
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during playback initiation for '{CurrentSong?.FilePath ?? "UNKNOWN FILE"}': {ex.ToString()}");
            if (_playbackEngine != null)
            {
                _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
                _playbackEngine.Dispose();
                _playbackEngine = null;
            }
            SetCurrentSongInternal(null);
        }
    }


    public void Pause()
    {
        Debug.WriteLine($"[PlaybackService] Pause requested. Current state: {CurrentPlaybackStatus}");
        if (_playbackEngine != null && IsPlaying)
        {
            _playbackEngine.Pause();
            SetPlaybackStateInternal(false, PlaybackStateStatus.Paused);
            StopUiUpdateTimerInternal();
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
            SetPlaybackStateInternal(true, PlaybackStateStatus.Playing);
            StartUiUpdateTimer();
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
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
            // OnEnginePlaybackStopped (via _completionHandler) will handle the rest
        }
        else
        {
            Debug.WriteLine("[PlaybackService Stop] No playback engine. Performing direct cleanup via completion handler.");
            // Simulate a stop event for the completion handler
            _completionHandler.Handle(null, new StoppedEventArgs(), CurrentSong, this.CurrentPosition, this.CurrentSongDuration, _explicitStopRequested);
        }
    }


    public void Seek(TimeSpan requestedPosition)
    {
        if (_playbackEngine == null || CurrentSong == null)
        {
            Debug.WriteLine($"[PlaybackService] Seek ignored: No active engine or current song. Engine: {_playbackEngine != null}, Song: {CurrentSong != null}");
            return;
        }

        TimeSpan targetPosition = _loopHandler.GetAdjustedSeekPosition(requestedPosition, _playbackEngine.CurrentSongDuration);

        var totalMs = _playbackEngine.CurrentSongDuration.TotalMilliseconds;
        var seekMarginMs = totalMs > 200 ? 100 : (totalMs > 0 ? Math.Min(totalMs / 2, 50) : 0);
        var maxSeekablePosition = TimeSpan.FromMilliseconds(totalMs - seekMarginMs);
        if (maxSeekablePosition < TimeSpan.Zero) maxSeekablePosition = TimeSpan.Zero;

        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, maxSeekablePosition.TotalSeconds));
        double positionToleranceSeconds = 0.3;

        try
        {
            TimeSpan currentAudioTimeForToleranceCheck = _playbackEngine.CurrentPosition;
            if (Math.Abs(currentAudioTimeForToleranceCheck.TotalSeconds - targetPosition.TotalSeconds) < positionToleranceSeconds)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error checking current position for seek tolerance: {ex.Message}. Proceeding with seek.");
        }

        try
        {
            _playbackEngine.Seek(targetPosition);
            this.CurrentPosition = _playbackEngine.CurrentPosition;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL Error during Engine Seek to {targetPosition:mm\\:ss\\.ff}: {ex.Message}");
            if (_playbackEngine != null) { try { this.CurrentPosition = _playbackEngine.CurrentPosition; } catch { /* ignore */ } }
            else { this.CurrentPosition = TimeSpan.Zero; }
        }
    }

    private void OnEnginePlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Debug.WriteLine($"[PlaybackService] === OnEnginePlaybackStopped (UI Thread) Invoked. Delegating to CompletionHandler. ===");
            Song? songThatJustStopped = CurrentSong;

            TimeSpan actualStoppedPosition = this.CurrentPosition;
            TimeSpan actualStoppedSongDuration = this.CurrentSongDuration;

            if (sender is NAudioPlaybackEngine engineInstance)
            {
                try { actualStoppedPosition = engineInstance.CurrentPosition; } catch (Exception ex) { Debug.WriteLine($"[PlaybackService OnEngineStopRelay] Error getting position from engine: {ex.Message}"); }
                try { actualStoppedSongDuration = engineInstance.CurrentSongDuration; } catch (Exception ex) { Debug.WriteLine($"[PlaybackService OnEngineStopRelay] Error getting duration from engine: {ex.Message}"); }
            }
            else
            {
                Debug.WriteLine($"[PlaybackService OnEngineStopRelay] Sender is not NAudioPlaybackEngine or _playbackEngine is null. Using VM's last known values for position/duration.");
            }


            _completionHandler.Handle(
                sender as NAudioPlaybackEngine,
                e,
                songThatJustStopped, // Pass the song that was playing
                actualStoppedPosition,       // Pass its actual position at stop
                actualStoppedSongDuration,   // Pass its actual duration
                _explicitStopRequested       // Pass the flag
            );
        });
    }

    // Internal methods for PlaybackCompletionHandler
    internal void StopUiUpdateTimerInternal() => StopUiUpdateTimer();
    internal EventHandler<StoppedEventArgs> GetEnginePlaybackStoppedHandler() => OnEnginePlaybackStopped;
    internal void SetCurrentSongInternal(Song? song) => CurrentSong = song; // Leverages existing private setter logic
    internal Song? GetCurrentSongInternal() => CurrentSong;
    internal void SetPlaybackStateInternal(bool isPlaying, PlaybackStateStatus status)
    {
        IsPlaying = isPlaying;
        CurrentPlaybackStatus = status;
    }
    internal void UpdateStateForNaturalPlaybackEndInternal()
    {
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        CurrentPosition = TimeSpan.Zero; // Reset position for UI, song remains "current" until next logic decides.
    }
    internal void InvokePlaybackEndedNaturallyInternal() => PlaybackEndedNaturally?.Invoke(this, EventArgs.Empty);
    internal void ResetExplicitStopRequestInternal() => _explicitStopRequested = false;


    private void StartUiUpdateTimer()
    {
        uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    private void StopUiUpdateTimer()
    {
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;

        Song? songAtDispose = CurrentSong;
        TimeSpan positionAtDispose = this.CurrentPosition; // Get from property as engine might be gone

        if (_playbackEngine != null)
        {
            try { positionAtDispose = _playbackEngine.CurrentPosition; } catch { /* Use property value */ }
            _playbackEngine.Stop();
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
            Debug.WriteLine("[PlaybackService] Disposed playback engine during service dispose.");
        }

        if (songAtDispose != null)
        {
            // Scrobble directly here as completion handler might not be involved in Dispose path
            TryScrobbleSong(songAtDispose, positionAtDispose);
        }

        _explicitStopRequested = false;
        CurrentSong = null;

        _loopHandler.Dispose();

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