using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Threading;
using NAudio.Wave;
using Sonorize.Models;
using Sonorize.Services.Playback;
using Sonorize.ViewModels;

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

                if (value == null)
                {
                    Debug.WriteLine("[PlaybackService] CurrentSong set to null. Resetting playback state variables.");
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    UpdatePlaybackPositionAndDuration(TimeSpan.Zero, TimeSpan.Zero);
                    _playbackMonitor.Stop();
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
        internal set
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
        internal set
        {
            if (SetProperty(ref _currentSongDuration, value))
            {
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
            }
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1.0;

    private readonly NAudioEngineController _engineController; // Changed from NAudioPlaybackEngine
    private readonly PlaybackLoopHandler _loopHandler;
    private readonly ScrobblingService _scrobblingService;
    private readonly PlaybackCompletionHandler _completionHandler;
    private readonly PlaybackMonitor _playbackMonitor;


    private volatile bool _explicitStopRequested = false;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (SetProperty(ref _playbackRate, value))
            {
                _engineController.PlaybackRate = value;
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
                _engineController.PitchSemitones = value;
            }
        }
    }

    public event EventHandler? PlaybackEndedNaturally;

    public PlaybackService(ScrobblingService scrobblingService)
    {
        Debug.WriteLine("[PlaybackService] Constructor called.");
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        _engineController = new NAudioEngineController();
        _engineController.PlaybackStopped += OnEngineControllerPlaybackStopped;
        _loopHandler = new PlaybackLoopHandler(this);
        _completionHandler = new PlaybackCompletionHandler(this, _scrobblingService);
        _playbackMonitor = new PlaybackMonitor(this, _engineController); // Pass engine controller
    }

    internal void UpdatePlaybackPositionAndDuration(TimeSpan position, TimeSpan duration)
    {
        this.CurrentPosition = position;
        this.CurrentSongDuration = duration;
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
        _playbackMonitor.Stop();

        Song? interruptedSong = null;
        TimeSpan interruptedSongPosition = TimeSpan.Zero;

        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong != null && CurrentSong != song)
        {
            interruptedSong = CurrentSong;
            interruptedSongPosition = _engineController.CurrentPosition;

            _engineController.Stop(); // This will trigger OnEngineControllerPlaybackStopped, which calls completion handler
            Debug.WriteLine($"[PlaybackService] Interrupted '{interruptedSong.Title}'. Engine stop requested.");
            // Scrobbling for interrupted song will be handled by completion handler if criteria met.
        }
        else if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong == song)
        {
            Debug.WriteLine($"[PlaybackService] Restarting song '{song.Title}'. Engine stop requested.");
            _engineController.Stop();
        }


        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] New song is null, path invalid, or file missing. Current playback will be fully stopped.");
            if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped) _engineController.Stop(); // Ensure existing playback stops
            SetCurrentSongInternal(null);
            return;
        }

        SetCurrentSongInternal(song);
        _explicitStopRequested = false;

        try
        {
            _engineController.PlaybackRate = this.PlaybackRate;
            _engineController.PitchSemitones = this.PitchSemitones;
            _engineController.Load(song.FilePath);

            UpdatePlaybackPositionAndDuration(TimeSpan.Zero, _engineController.CurrentSongDuration);

            TimeSpan startPosition = _loopHandler.GetInitialPlaybackPosition(CurrentSongDuration);
            if (startPosition > TimeSpan.Zero && startPosition < CurrentSongDuration)
            {
                _engineController.Seek(startPosition);
                UpdatePlaybackPositionAndDuration(_engineController.CurrentPosition, CurrentSongDuration);
            }

            _engineController.Play();
            SetPlaybackStateInternal(true, PlaybackStateStatus.Playing);
            _playbackMonitor.Start(_loopHandler, CurrentSong);
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}. State: {CurrentPlaybackStatus}");
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during playback initiation for '{CurrentSong?.FilePath ?? "UNKNOWN FILE"}': {ex.ToString()}");
            _engineController.Stop(); // Ensure engine is stopped if loading failed partially
            SetCurrentSongInternal(null);
        }
    }


    public void Pause()
    {
        Debug.WriteLine($"[PlaybackService] Pause requested. Current state: {CurrentPlaybackStatus}");
        if (IsPlaying)
        {
            _playbackMonitor.Stop();
            _engineController.Pause();
            SetPlaybackStateInternal(false, PlaybackStateStatus.Paused);
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

        if (CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Paused state. Resuming engine.");
            _engineController.Play();
            SetPlaybackStateInternal(true, PlaybackStateStatus.Playing);
            _playbackMonitor.Start(_loopHandler, CurrentSong);
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Stopped state. Re-playing current song.");
            Play(CurrentSong); // Play will handle monitor start
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
        _playbackMonitor.Stop();

        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _engineController.Stop();
            // OnEngineControllerPlaybackStopped (via _completionHandler) will handle the rest
        }
        else
        {
            Debug.WriteLine("[PlaybackService Stop] Already stopped. Performing direct cleanup via completion handler if needed.");
            // If already stopped, the engine won't raise an event. Call completion handler directly.
            _completionHandler.Handle(new StoppedEventArgs(), CurrentSong, this.CurrentPosition, this.CurrentSongDuration, _explicitStopRequested);
        }
    }


    public void Seek(TimeSpan requestedPosition)
    {
        if (CurrentSong == null || CurrentSongDuration == TimeSpan.Zero)
        {
            Debug.WriteLine($"[PlaybackService] Seek ignored: No current song or duration is zero. Song: {CurrentSong != null}, Duration: {CurrentSongDuration}");
            return;
        }

        TimeSpan targetPosition = _loopHandler.GetAdjustedSeekPosition(requestedPosition, CurrentSongDuration);

        var totalMs = CurrentSongDuration.TotalMilliseconds;
        var seekMarginMs = totalMs > 200 ? 100 : (totalMs > 0 ? Math.Min(totalMs / 2, 50) : 0);
        var maxSeekablePosition = TimeSpan.FromMilliseconds(totalMs - seekMarginMs);
        if (maxSeekablePosition < TimeSpan.Zero) maxSeekablePosition = TimeSpan.Zero;

        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, maxSeekablePosition.TotalSeconds));
        double positionToleranceSeconds = 0.3;


        TimeSpan currentAudioTimeForToleranceCheck = _engineController.CurrentPosition;
        if (Math.Abs(currentAudioTimeForToleranceCheck.TotalSeconds - targetPosition.TotalSeconds) < positionToleranceSeconds)
        {
            return;
        }

        _engineController.Seek(targetPosition);
        if (CurrentPlaybackStatus != PlaybackStateStatus.Playing)
        {
            UpdatePlaybackPositionAndDuration(_engineController.CurrentPosition, CurrentSongDuration);
        }
    }

    private void OnEngineControllerPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Debug.WriteLine($"[PlaybackService] === OnEngineControllerPlaybackStopped (UI Thread) Invoked. Delegating to CompletionHandler. ===");
            _playbackMonitor.Stop();
            Song? songThatJustStopped = CurrentSong;

            // Get final position/duration from controller as it's the source of truth for the stopped engine
            TimeSpan actualStoppedPosition = _engineController.CurrentPosition;
            TimeSpan actualStoppedSongDuration = _engineController.CurrentSongDuration;

            if (songThatJustStopped != null && actualStoppedSongDuration == TimeSpan.Zero) // If engine controller returns 0 for duration (e.g. after dispose)
            {
                actualStoppedSongDuration = songThatJustStopped.Duration; // Fallback to song's metadata duration
            }


            _completionHandler.Handle(
                e,
                songThatJustStopped,
                actualStoppedPosition,
                actualStoppedSongDuration,
                _explicitStopRequested
            );
        });
    }

    // Internal methods for PlaybackCompletionHandler
    internal void StopUiUpdateTimerInternal() => _playbackMonitor.Stop();
    internal void SetCurrentSongInternal(Song? song) => CurrentSong = song;
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
        UpdatePlaybackPositionAndDuration(TimeSpan.Zero, CurrentSongDuration);
    }
    internal void InvokePlaybackEndedNaturallyInternal() => PlaybackEndedNaturally?.Invoke(this, EventArgs.Empty);
    internal void ResetExplicitStopRequestInternal() => _explicitStopRequested = false;

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");
        _playbackMonitor.Dispose();

        Song? songAtDispose = CurrentSong;
        TimeSpan positionAtDispose = this.CurrentPosition; // Get from property as controller might be gone

        if (_engineController != null)
        {
            positionAtDispose = _engineController.CurrentPosition; // Get final position before disposing controller
            _engineController.PlaybackStopped -= OnEngineControllerPlaybackStopped;
            _engineController.Dispose();
            // _engineController = null; // Not strictly necessary if service is being disposed
            Debug.WriteLine("[PlaybackService] Disposed engine controller during service dispose.");
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
        Dispose();
        Debug.WriteLine("[PlaybackService] Finalizer completed for PlaybackService.");
    }
}