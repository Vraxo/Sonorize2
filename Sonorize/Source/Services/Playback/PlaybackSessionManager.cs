using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using NAudio.Wave; // For StoppedEventArgs
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackSessionManager : INotifyPropertyChanged, IDisposable
{
    private readonly PlaybackLoopHandler _loopHandler;
    private readonly PlaybackCompletionHandler _completionHandler;
    private readonly ScrobblingService _scrobblingService;
    private readonly PlaybackSessionState _sessionState;

    private PlaybackInfrastructureProvider? _currentInfrastructure;

    // Delegated Properties to PlaybackSessionState
    public Song? CurrentSong => _sessionState.CurrentSong;
    public bool HasCurrentSong => _sessionState.HasCurrentSong;
    public bool IsPlaying => _sessionState.IsPlaying;
    public PlaybackStateStatus CurrentPlaybackStatus => _sessionState.CurrentPlaybackStatus;
    public TimeSpan CurrentPosition => _sessionState.CurrentPosition;
    public double CurrentPositionSeconds => _sessionState.CurrentPositionSeconds;
    public TimeSpan CurrentSongDuration => _sessionState.CurrentSongDuration;
    public double CurrentSongDurationSeconds => _sessionState.CurrentSongDurationSeconds;

    public float PlaybackRate
    {
        get => _sessionState.PlaybackRate;
        set
        {
            if (_sessionState.PlaybackRate == value) return;
            _sessionState.PlaybackRate = value;
            _currentInfrastructure?.Coordinator.UpdateRateAndPitch(value, PitchSemitones);
        }
    }

    public float PitchSemitones
    {
        get => _sessionState.PitchSemitones;
        set
        {
            if (_sessionState.PitchSemitones == value) return;
            _sessionState.PitchSemitones = value;
            _currentInfrastructure?.Coordinator.UpdateRateAndPitch(PlaybackRate, value);
        }
    }

    private volatile bool _explicitStopRequested = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SessionEndedNaturally;

    public PlaybackSessionManager(ScrobblingService scrobblingService, PlaybackLoopHandler loopHandler)
    {
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        _loopHandler = loopHandler ?? throw new ArgumentNullException(nameof(loopHandler));
        _sessionState = new PlaybackSessionState();
        _sessionState.PropertyChanged += SessionState_PropertyChanged;
        _completionHandler = new PlaybackCompletionHandler(this, _scrobblingService);
        Debug.WriteLine("[PlaybackSessionManager] Initialized.");
    }

    private void SessionState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName!);
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public bool StartNewSession(Song song)
    {
        Debug.WriteLine($"[SessionManager] StartNewSession requested for: {(song?.Title ?? "null song")}");

        // --- Step 1: Detach and schedule the stop for the old infrastructure ---
        var oldInfrastructure = _currentInfrastructure;
        if (oldInfrastructure != null)
        {
            // Detach main handlers so they don't fire for the old instance anymore
            oldInfrastructure.Coordinator.EnginePlaybackStopped -= OnEngineCoordinatorPlaybackStopped;
            oldInfrastructure.Coordinator.EnginePositionUpdated -= OnEngineCoordinatorPositionUpdated;

            // Capture the complete state of the outgoing song
            var songThatStopped = oldInfrastructure.Coordinator.CurrentSong;
            var positionThatStopped = oldInfrastructure.Coordinator.CurrentPosition;
            var durationThatStopped = oldInfrastructure.Coordinator.CurrentSongDuration;

            Debug.WriteLine($"[SessionManager] Scheduling stop for previous song '{songThatStopped?.Title}' at {positionThatStopped}.");

            // Attach a new, one-time handler to the old infrastructure.
            // This closure captures the song's final state and will execute when the old engine finally stops.
            oldInfrastructure.Coordinator.EnginePlaybackStopped += (s, e) =>
            {
                Debug.WriteLine($"[SessionManager] One-time stop handler fired for old song: '{songThatStopped?.Title}'.");
                _completionHandler.Handle(e, songThatStopped, positionThatStopped, durationThatStopped, wasExplicitlyStopped: false, isInternalStopForSongChange: true);
                oldInfrastructure.Dispose(); // Clean up the old infrastructure completely.
            };
            oldInfrastructure.Coordinator.Stop();
        }

        // --- Step 2: Validate new song ---
        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[SessionManager] New song is null or invalid. Session will be stopped.");
            FinalizeCurrentSong(null);
            return false;
        }

        // --- Step 3: Create and start the new infrastructure ---
        try
        {
            _currentInfrastructure = new PlaybackInfrastructureProvider(_loopHandler);
            _currentInfrastructure.Coordinator.EnginePlaybackStopped += OnEngineCoordinatorPlaybackStopped;
            _currentInfrastructure.Coordinator.EnginePositionUpdated += OnEngineCoordinatorPositionUpdated;

            var sessionLoader = new PlaybackSessionLoader(_currentInfrastructure.Coordinator, _sessionState, _scrobblingService);
            bool result = sessionLoader.LoadNewSession(song);
            if (!result)
            {
                _currentInfrastructure.Dispose();
                _currentInfrastructure = null;
                FinalizeCurrentSong(null);
            }
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SessionManager] CRITICAL: Exception creating new playback infrastructure: {ex}");
            _currentInfrastructure?.Dispose();
            _currentInfrastructure = null;
            FinalizeCurrentSong(null);
            return false;
        }
    }

    public void PauseSession()
    {
        if (IsPlaying)
        {
            _currentInfrastructure?.Coordinator.Pause();
            SetPlaybackState(false, PlaybackStateStatus.Paused);
        }
    }

    public void ResumeSession()
    {
        if (CurrentSong == null) return;
        if (CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            _currentInfrastructure?.Coordinator.Resume(startMonitor: true);
            SetPlaybackState(true, PlaybackStateStatus.Playing);
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            var sessionLoader = new PlaybackSessionLoader(_currentInfrastructure!.Coordinator, _sessionState, _scrobblingService);
            sessionLoader.ReloadSession(CurrentSong, CurrentPosition, true);
        }
    }

    public void StopSession(bool isExplicit)
    {
        _explicitStopRequested = isExplicit;
        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _currentInfrastructure?.Coordinator.Stop();
        }
        else if (isExplicit)
        {
            _completionHandler.Handle(new StoppedEventArgs(), CurrentSong, this.CurrentPosition, this.CurrentSongDuration, _explicitStopRequested, false);
        }
    }

    public void SeekSession(TimeSpan requestedPosition)
    {
        if (CurrentSong == null || CurrentSongDuration == TimeSpan.Zero) return;
        _currentInfrastructure?.Coordinator.Seek(requestedPosition);
    }

    internal void ForceReleaseEngineForCurrentSong()
    {
        if (CurrentSong == null) return;
        _currentInfrastructure?.Coordinator.DisposeCurrentEngineInternals();
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
    }

    internal bool ForceReloadAndPlayEngine(Song song, TimeSpan position, bool shouldBePlaying)
    {
        _explicitStopRequested = false;
        if (_currentInfrastructure == null)
        {
            Debug.WriteLine("[SessionManager] ForceReloadAndPlayEngine: No current infrastructure. Creating new one.");
            _currentInfrastructure = new PlaybackInfrastructureProvider(_loopHandler);
            _currentInfrastructure.Coordinator.EnginePlaybackStopped += OnEngineCoordinatorPlaybackStopped;
            _currentInfrastructure.Coordinator.EnginePositionUpdated += OnEngineCoordinatorPositionUpdated;
        }
        var sessionLoader = new PlaybackSessionLoader(_currentInfrastructure.Coordinator, _sessionState, _scrobblingService);
        return sessionLoader.ReloadSession(song, position, shouldBePlaying);
    }

    private void OnEngineCoordinatorPositionUpdated(object? sender, PositionEventArgs e)
    {
        if (sender == _currentInfrastructure?.Coordinator)
        {
            _sessionState.CurrentPosition = e.Position;
            _sessionState.CurrentSongDuration = e.Duration;
        }
    }

    private void OnEngineCoordinatorPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // This handler is now ONLY for the CURRENT session.
        // Stops from song transitions are handled by the one-time lambda in StartNewSession.
        if (sender != _currentInfrastructure?.Coordinator)
        {
            Debug.WriteLine("[SessionManager] Received a stop event from a stale/old infrastructure. Ignoring.");
            return;
        }

        _completionHandler.Handle(e, CurrentSong, this.CurrentPosition, this.CurrentSongDuration, _explicitStopRequested, isInternalStopForSongChange: false);
    }

    // Methods called by PlaybackCompletionHandler
    internal void StopUiUpdateMonitor()
    {
        _currentInfrastructure?.Monitor.Stop();
    }

    internal void UpdateStateForNaturalPlaybackEnd()
    {
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
    }

    internal void FinalizeCurrentSong(Song? song)
    {
        _sessionState.CurrentSong = song;
        if (song == null) _sessionState.ResetToDefault();
        _currentInfrastructure?.Coordinator.SetSong(song);
    }

    internal Song? GetCurrentSongForCompletion() => CurrentSong;

    internal void SetPlaybackState(bool isPlaying, PlaybackStateStatus status)
    {
        _sessionState.IsPlaying = isPlaying;
        _sessionState.CurrentPlaybackStatus = status;
    }

    internal void TriggerSessionEndedNaturally()
    {
        SessionEndedNaturally?.Invoke(this, EventArgs.Empty);
    }

    internal void ResetExplicitStopFlag()
    {
        _explicitStopRequested = false;
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackSessionManager] Dispose called.");
        if (_sessionState != null) _sessionState.PropertyChanged -= SessionState_PropertyChanged;
        _currentInfrastructure?.Dispose();
        _currentInfrastructure = null;
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackSessionManager] Dispose completed.");
    }

    ~PlaybackSessionManager()
    {
        Dispose();
    }
}