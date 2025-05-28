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
                _playbackEngineCoordinator.SetSong(value);

                if (value == null)
                {
                    Debug.WriteLine("[PlaybackService] CurrentSong set to null. Resetting playback state variables.");
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    UpdatePlaybackPositionAndDuration(TimeSpan.Zero, TimeSpan.Zero);
                    // Coordinator's Stop method handles monitor stop.
                }
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

    private readonly PlaybackEngineCoordinator _playbackEngineCoordinator;
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
                _playbackEngineCoordinator.UpdateRateAndPitch(value, PitchSemitones);
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
                _playbackEngineCoordinator.UpdateRateAndPitch(PlaybackRate, value);
            }
        }
    }

    public event EventHandler? PlaybackEndedNaturally;

    public PlaybackService(ScrobblingService scrobblingService)
    {
        Debug.WriteLine("[PlaybackService] Constructor called.");
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));

        var engineController = new NAudioEngineController();
        var loopHandler = new PlaybackLoopHandler(this); // LoopHandler might still need PlaybackService for complex interactions or state.
        var playbackMonitor = new PlaybackMonitor(engineController, loopHandler);

        _playbackEngineCoordinator = new PlaybackEngineCoordinator(engineController, loopHandler, playbackMonitor);
        _playbackEngineCoordinator.EnginePlaybackStopped += OnEngineCoordinatorPlaybackStopped;
        _playbackEngineCoordinator.EnginePositionUpdated += OnEngineCoordinatorPositionUpdated;

        _completionHandler = new PlaybackCompletionHandler(this, _scrobblingService);
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

        Song? interruptedSong = null;
        TimeSpan interruptedSongPosition = TimeSpan.Zero;

        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong != null && CurrentSong != song)
        {
            interruptedSong = CurrentSong;
            interruptedSongPosition = _playbackEngineCoordinator.CurrentPosition; // Get position from coordinator

            _playbackEngineCoordinator.Stop(); // This will trigger OnEngineCoordinatorPlaybackStopped
            Debug.WriteLine($"[PlaybackService] Interrupted '{interruptedSong.Title}'. Coordinator stop requested.");
        }
        else if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong == song)
        {
            Debug.WriteLine($"[PlaybackService] Restarting song '{song.Title}'. Coordinator stop requested.");
            _playbackEngineCoordinator.Stop();
        }

        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] New song is null, path invalid, or file missing. Current playback will be fully stopped.");
            if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped) _playbackEngineCoordinator.Stop();
            CurrentSong = null; // Setter handles coordinator update and state reset
            return;
        }

        CurrentSong = song; // This sets the song in the coordinator too
        _explicitStopRequested = false;

        try
        {
            if (!_playbackEngineCoordinator.Load(song.FilePath, PlaybackRate, PitchSemitones))
            {
                Debug.WriteLine($"[PlaybackService] PlaybackEngineCoordinator.Load failed for '{CurrentSong?.FilePath ?? "UNKNOWN FILE"}'.");
                _playbackEngineCoordinator.Stop();
                CurrentSong = null;
                return;
            }

            UpdatePlaybackPositionAndDuration(TimeSpan.Zero, _playbackEngineCoordinator.CurrentSongDuration);

            _playbackEngineCoordinator.Play(startMonitor: true); // Play and start monitor
            SetPlaybackStateInternal(true, PlaybackStateStatus.Playing);
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}. State: {CurrentPlaybackStatus}");
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during playback initiation for '{CurrentSong?.FilePath ?? "UNKNOWN FILE"}': {ex.ToString()}");
            _playbackEngineCoordinator.Stop();
            CurrentSong = null;
        }
    }

    public void Pause()
    {
        Debug.WriteLine($"[PlaybackService] Pause requested. Current state: {CurrentPlaybackStatus}");
        if (IsPlaying)
        {
            _playbackEngineCoordinator.Pause();
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
            Debug.WriteLine("[PlaybackService] Resume requested from Paused state. Resuming via coordinator.");
            _playbackEngineCoordinator.Resume(startMonitor: true);
            SetPlaybackStateInternal(true, PlaybackStateStatus.Playing);
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Stopped state. Re-playing current song.");
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

        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _playbackEngineCoordinator.Stop(); // Will trigger OnEngineCoordinatorPlaybackStopped
        }
        else
        {
            Debug.WriteLine("[PlaybackService Stop] Already stopped. Performing direct cleanup via completion handler if needed.");
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
        _playbackEngineCoordinator.Seek(requestedPosition);
        // Position update will come via OnEngineCoordinatorPositionUpdated if monitor isn't running
    }

    private void OnEngineCoordinatorPositionUpdated(object? sender, PositionEventArgs e)
    {
        // This is called by the coordinator when the monitor updates position,
        // or when Seek is called while not playing.
        UpdatePlaybackPositionAndDuration(e.Position, e.Duration);
    }

    private void OnEngineCoordinatorPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Debug.WriteLine($"[PlaybackService] === OnEngineCoordinatorPlaybackStopped (UI Thread) Invoked. Delegating to CompletionHandler. ===");
            Song? songThatJustStopped = CurrentSong;

            // Get final position/duration from coordinator as it's the source of truth for the stopped engine
            TimeSpan actualStoppedPosition = _playbackEngineCoordinator.CurrentPosition;
            TimeSpan actualStoppedSongDuration = _playbackEngineCoordinator.CurrentSongDuration;

            if (songThatJustStopped != null && actualStoppedSongDuration == TimeSpan.Zero)
            {
                actualStoppedSongDuration = songThatJustStopped.Duration;
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
    internal void StopUiUpdateTimerInternal() => _playbackEngineCoordinator.Stop(); // Monitor is part of coordinator
    internal void SetCurrentSongInternal(Song? song) => CurrentSong = song; // This also updates coordinator's song
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
        // Position update might be handled by coordinator's stop or completion handler setting CurrentSong to null
        UpdatePlaybackPositionAndDuration(TimeSpan.Zero, CurrentSongDuration);
    }
    internal void InvokePlaybackEndedNaturallyInternal() => PlaybackEndedNaturally?.Invoke(this, EventArgs.Empty);
    internal void ResetExplicitStopRequestInternal() => _explicitStopRequested = false;

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");

        Song? songAtDispose = CurrentSong;
        TimeSpan positionAtDispose = this.CurrentPosition;

        if (_playbackEngineCoordinator != null)
        {
            positionAtDispose = _playbackEngineCoordinator.CurrentPosition;
            _playbackEngineCoordinator.EnginePlaybackStopped -= OnEngineCoordinatorPlaybackStopped;
            _playbackEngineCoordinator.EnginePositionUpdated -= OnEngineCoordinatorPositionUpdated;
            _playbackEngineCoordinator.Dispose();
            Debug.WriteLine("[PlaybackService] Disposed playback engine coordinator during service dispose.");
        }

        if (songAtDispose != null)
        {
            TryScrobbleSong(songAtDispose, positionAtDispose);
        }

        _explicitStopRequested = false;
        CurrentSong = null; // This will also clear the song in the coordinator

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