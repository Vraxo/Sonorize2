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

                if (value == null) // CurrentSong is being cleared
                {
                    Debug.WriteLine("[PlaybackService] CurrentSong set to null. Resetting playback state variables.");
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    CurrentPosition = TimeSpan.Zero;
                    CurrentSongDuration = TimeSpan.Zero;
                    StopUiUpdateTimer();
                }

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
                    StopUiUpdateTimer();
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
                    // Debug.WriteLine($"[PlaybackService] Timer callback found engine state is not Playing ({_playbackEngine?.CurrentPlaybackStatus}). Stopping timer.");
                    StopUiUpdateTimer();
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

        // --- Start: Handle interruption of the PREVIOUS song ---
        Song? interruptedSong = null;
        TimeSpan interruptedSongPosition = TimeSpan.Zero;

        if (_playbackEngine != null && CurrentSong != null && CurrentSong != song) // An actual song is playing and is being interrupted by a DIFFERENT new song
        {
            interruptedSong = CurrentSong; // Capture the song being interrupted
            try
            {
                interruptedSongPosition = _playbackEngine.CurrentPosition; // Capture its position BEFORE stopping
            }
            catch (Exception ex) { Debug.WriteLine($"[PlaybackService] Error getting position of interrupted song '{interruptedSong.Title}': {ex.Message}"); }

            _playbackEngine.Stop(); // Request stop
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped; // Detach handler
            _playbackEngine.Dispose();      // Dispose
            _playbackEngine = null;         // Nullify reference
            Debug.WriteLine($"[PlaybackService] Interrupted '{interruptedSong.Title}'. Old engine stopped & disposed.");

            // Attempt to scrobble the interrupted song immediately
            TryScrobbleSong(interruptedSong, interruptedSongPosition);
        }
        else if (_playbackEngine != null && CurrentSong == song) // Request to play the SAME song again (restart)
        {
            Debug.WriteLine($"[PlaybackService] Restarting song '{song.Title}'. Old engine stopped & disposed.");
            _playbackEngine.Stop();
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
        }
        // --- End: Handle interruption of the PREVIOUS song ---

        // --- Start: Validate and set up the NEW song ---
        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] New song is null, path invalid, or file missing. Current playback will be fully stopped.");
            CurrentSong = null; // Triggers UI state cleanup via its setter.
            return;
        }

        CurrentSong = song;
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

            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
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
            CurrentSong = null;
        }
        // --- End: Validate and set up the NEW song ---
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
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
        }
        else if (_playbackEngine != null && CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Stopped state. Re-playing current song via engine.");
            // Re-calling Play ensures the engine is freshly initialized if it was disposed.
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

        Song? songThatWasPlaying = CurrentSong;
        TimeSpan positionOfStoppedSong = TimeSpan.Zero;

        if (_playbackEngine != null)
        {
            try
            {
                // Capture position before stopping, as it might reset.
                positionOfStoppedSong = _playbackEngine.CurrentPosition;
            }
            catch (Exception ex) { Debug.WriteLine($"[PlaybackService Stop] Error getting position before stop: {ex.Message}"); }

            _playbackEngine.Stop();
            // OnEnginePlaybackStopped will handle the rest, including scrobbling and setting CurrentSong = null.
        }
        else
        {
            Debug.WriteLine("[PlaybackService Stop] No playback engine. Performing direct cleanup.");
            if (songThatWasPlaying != null) positionOfStoppedSong = this.CurrentPosition;

            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            this.CurrentPosition = TimeSpan.Zero;
            CurrentSongDuration = TimeSpan.Zero;

            TryScrobbleSong(songThatWasPlaying, positionOfStoppedSong);
            CurrentSong = null;

            _explicitStopRequested = false;
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
                // Debug.WriteLine($"[PlaybackService] Seek target {targetPosition:mm\\:ss\\.ff} is very close to current position {currentAudioTimeForToleranceCheck:mm\\:ss\\.ff} (within {positionToleranceSeconds}s), ignoring seek.");
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error checking current position for seek tolerance: {ex.Message}. Proceeding with seek.");
        }

        // Debug.WriteLine($"[PlaybackService] Seeking engine to: {targetPosition:mm\\:ss\\.ff}");
        try
        {
            _playbackEngine.Seek(targetPosition);
            this.CurrentPosition = _playbackEngine.CurrentPosition; // Important to sync VM's position
            // Debug.WriteLine($"[PlaybackService] Seek executed. Engine Time after seek: {_playbackEngine.CurrentPosition:mm\\:ss\\.ff}. VM Position: {this.CurrentPosition:mm\\:ss\\.ff}");
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
            Song? songThatJustStopped = CurrentSong;

            TimeSpan stoppedPosition = TimeSpan.Zero;
            TimeSpan stoppedSongDuration = TimeSpan.Zero;

            // It's crucial that _playbackEngine here refers to the engine that *actually* stopped.
            // If Play() creates a new engine and this event is from an old, disposed one, sender might not be _playbackEngine.
            // However, we detach the handler from the old engine in Play(). So, this event should always be from the *current* _playbackEngine.
            if (_playbackEngine != null && sender == _playbackEngine)
            {
                try { stoppedPosition = _playbackEngine.CurrentPosition; } catch (Exception ex) { Debug.WriteLine($"[PlaybackService OnEnginePlaybackStopped] Error getting position: {ex.Message}"); }
                try { stoppedSongDuration = _playbackEngine.CurrentSongDuration; } catch (Exception ex) { Debug.WriteLine($"[PlaybackService OnEnginePlaybackStopped] Error getting duration: {ex.Message}"); }
            }
            else
            {
                Debug.WriteLine($"[PlaybackService OnEnginePlaybackStopped] Sender mismatch or _playbackEngine is null. Sender: {sender?.GetHashCode()}, _playbackEngine: {_playbackEngine?.GetHashCode()}. Using VM's last known values.");
                stoppedPosition = this.CurrentPosition;
                stoppedSongDuration = this.CurrentSongDuration;
            }

            StopUiUpdateTimer();

            // Detach from the engine that just stopped.
            if (sender is NAudioPlaybackEngine engineInstance)
            {
                engineInstance.PlaybackStopped -= OnEnginePlaybackStopped;
                Debug.WriteLine("[PlaybackService OnEnginePlaybackStopped] Detached handler from the stopping engine instance.");
            }


            if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] Playback stopped due to error: {e.Exception.Message}. Finalizing state to Stopped.");
                TryScrobbleSong(songThatJustStopped, stoppedPosition);
                CurrentSong = null; // Setting to null resets other UI state via its setter
            }
            else
            {
                bool isNearEndOfFile = (stoppedSongDuration > TimeSpan.Zero) && (stoppedPosition >= stoppedSongDuration - TimeSpan.FromMilliseconds(500));
                Debug.WriteLine($"[PlaybackService] Clean Stop. ExplicitStopReq: {_explicitStopRequested}. NearEnd: {isNearEndOfFile}. Pos: {stoppedPosition:mm\\:ss\\.ff}, Dur: {stoppedSongDuration:mm\\:ss\\.ff}");

                if (_explicitStopRequested)
                {
                    Debug.WriteLine("[PlaybackService] Playback stopped by explicit user/app command. Finalizing.");
                    TryScrobbleSong(songThatJustStopped, stoppedPosition);
                    CurrentSong = null;
                }
                else if (isNearEndOfFile)
                {
                    Debug.WriteLine("[PlaybackService] Playback stopped naturally (end of file). Raising PlaybackEndedNaturally.");
                    TryScrobbleSong(songThatJustStopped, stoppedSongDuration);
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    this.CurrentPosition = TimeSpan.Zero; // Reset position for UI, song remains "current" until next action.
                    PlaybackEndedNaturally?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    Debug.WriteLine("[PlaybackService] Playback stopped unexpectedly (not error, not explicit, not EOF). Scrobbling and stopping.");
                    TryScrobbleSong(songThatJustStopped, stoppedPosition);
                    CurrentSong = null;
                }
            }
            // Ensure IsPlaying and CurrentPlaybackStatus are also set correctly if CurrentSong becomes null
            if (CurrentSong == null)
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
            _explicitStopRequested = false;
            Debug.WriteLine($"[PlaybackService] OnEnginePlaybackStopped handler finishes. CurrentSong: {CurrentSong?.Title ?? "null"}");
        });
    }


    private void StartUiUpdateTimer()
    {
        uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        // Debug.WriteLine("[PlaybackService] UI Update Timer Started.");
    }

    private void StopUiUpdateTimer()
    {
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        // Debug.WriteLine("[PlaybackService] UI Update Timer Stopped.");
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;

        Song? songAtDispose = CurrentSong;
        TimeSpan positionAtDispose = TimeSpan.Zero;

        if (_playbackEngine != null)
        {
            try
            {
                positionAtDispose = _playbackEngine.CurrentPosition;
            }
            catch (Exception ex) { Debug.WriteLine($"[PlaybackService Dispose] Error getting position: {ex.Message}"); }

            _playbackEngine.Stop();
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
            Debug.WriteLine("[PlaybackService] Disposed playback engine during service dispose.");
        }
        else
        {
            positionAtDispose = this.CurrentPosition;
        }

        if (songAtDispose != null)
        {
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
        Dispose(); // Call the same Dispose logic
        Debug.WriteLine("[PlaybackService] Finalizer completed for PlaybackService.");
    }
}