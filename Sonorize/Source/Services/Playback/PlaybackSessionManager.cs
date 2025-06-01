using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using NAudio.Wave; // For StoppedEventArgs
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackSessionManager : INotifyPropertyChanged, IDisposable
{
    private readonly PlaybackInfrastructureProvider _infrastructureProvider;
    private readonly PlaybackEngineCoordinator _playbackEngineCoordinator; // Now from provider
    private readonly PlaybackCompletionHandler _completionHandler;
    private readonly ScrobblingService _scrobblingService;
    private readonly PlaybackSessionState _sessionState;
    private readonly PlaybackSessionLoader _sessionLoader;

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
            if (_sessionState.PlaybackRate != value)
            {
                _sessionState.PlaybackRate = value;
                _playbackEngineCoordinator.UpdateRateAndPitch(value, PitchSemitones);
            }
        }
    }

    public float PitchSemitones
    {
        get => _sessionState.PitchSemitones;
        set
        {
            if (_sessionState.PitchSemitones != value)
            {
                _sessionState.PitchSemitones = value;
                _playbackEngineCoordinator.UpdateRateAndPitch(PlaybackRate, value);
            }
        }
    }

    private volatile bool _explicitStopRequested = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SessionEndedNaturally;

    public PlaybackSessionManager(ScrobblingService scrobblingService, PlaybackLoopHandler loopHandler)
    {
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        _sessionState = new PlaybackSessionState();
        _sessionState.PropertyChanged += SessionState_PropertyChanged;

        _infrastructureProvider = new PlaybackInfrastructureProvider(loopHandler);
        _playbackEngineCoordinator = _infrastructureProvider.Coordinator;

        _completionHandler = new PlaybackCompletionHandler(_scrobblingService); // Pass only ScrobblingService
        _sessionLoader = new PlaybackSessionLoader(_playbackEngineCoordinator, _sessionState, _scrobblingService);


        _playbackEngineCoordinator.EnginePlaybackStopped += OnEngineCoordinatorPlaybackStopped;
        _playbackEngineCoordinator.EnginePositionUpdated += OnEngineCoordinatorPositionUpdated;
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

    private void UpdatePlaybackPositionAndDuration(TimeSpan position, TimeSpan duration)
    {
        _sessionState.CurrentPosition = position;
        _sessionState.CurrentSongDuration = duration;
    }

    public bool StartNewSession(Song song)
    {
        Debug.WriteLine($"[SessionManager] StartNewSession requested for: {(song?.Title ?? "null song")}");

        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong != null && CurrentSong != song)
        {
            _playbackEngineCoordinator.Stop();
        }
        else if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong == song)
        {
            _playbackEngineCoordinator.Stop();
        }

        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[SessionManager] New song is null or invalid. Stopping current playback if any.");
            if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped) _playbackEngineCoordinator.Stop();
            _sessionState.CurrentSong = null;
            _sessionState.IsPlaying = false;
            _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            return false;
        }

        _explicitStopRequested = false;
        return _sessionLoader.LoadNewSession(song);
    }

    public void PauseSession()
    {
        if (IsPlaying)
        {
            _playbackEngineCoordinator.Pause();
            _sessionState.IsPlaying = false;
            _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Paused;
        }
    }

    public void ResumeSession()
    {
        if (CurrentSong == null) return;

        if (CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            _playbackEngineCoordinator.Resume(startMonitor: true);
            _sessionState.IsPlaying = true;
            _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            _sessionLoader.ReloadSession(CurrentSong, CurrentPosition, true);
        }
    }

    public void StopSession(bool isExplicit)
    {
        _explicitStopRequested = isExplicit;
        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _playbackEngineCoordinator.Stop(); // This will trigger OnEngineCoordinatorPlaybackStopped
        }
        else if (isExplicit) // If already stopped, but stop is called explicitly again
        {
            // Manually trigger completion logic for an already stopped state if needed
            var result = _completionHandler.Handle(
              new StoppedEventArgs(), // Empty args as it's not from engine event
              CurrentSong,
              this.CurrentPosition,
              this.CurrentSongDuration,
              _explicitStopRequested
            );
            ApplyCompletionResult(result);
        }
    }

    public void SeekSession(TimeSpan requestedPosition)
    {
        if (CurrentSong == null || CurrentSongDuration == TimeSpan.Zero) return;
        _playbackEngineCoordinator.Seek(requestedPosition);
    }

    internal void ForceReleaseEngineForCurrentSong()
    {
        if (CurrentSong == null)
        {
            Debug.WriteLine("[SessionManager] ForceReleaseEngineForCurrentSong: No current song. Nothing to release.");
            return;
        }
        Debug.WriteLine($"[SessionManager] ForceReleaseEngineForCurrentSong for: {CurrentSong.Title}");
        _playbackEngineCoordinator.DisposeCurrentEngineInternals();
        _sessionState.IsPlaying = false;
        _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Stopped; // Position remains, state is stopped
        Debug.WriteLine($"[SessionManager] Engine resources released for {CurrentSong.Title}. State set to Stopped.");
    }

    internal bool ForceReloadAndPlayEngine(Song song, TimeSpan position, bool shouldBePlaying)
    {
        Debug.WriteLine($"[SessionManager] ForceReloadAndPlayEngine for: {song.Title}, Position: {position}, ShouldPlay: {shouldBePlaying}");
        _explicitStopRequested = false;
        return _sessionLoader.ReloadSession(song, position, shouldBePlaying);
    }


    private void OnEngineCoordinatorPositionUpdated(object? sender, PositionEventArgs e)
    {
        UpdatePlaybackPositionAndDuration(e.Position, e.Duration);
    }

    private void OnEngineCoordinatorPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Song? songThatJustStopped = CurrentSong; // Capture before it's potentially changed by Handle
        TimeSpan actualStoppedPosition = _playbackEngineCoordinator.CurrentPosition; // Capture current state from coordinator
        TimeSpan actualStoppedSongDuration = _playbackEngineCoordinator.CurrentSongDuration;

        if (songThatJustStopped != null && actualStoppedSongDuration == TimeSpan.Zero) // Ensure duration is valid
        {
            actualStoppedSongDuration = songThatJustStopped.Duration;
        }

        PlaybackCompletionResult result = _completionHandler.Handle(
            e,
            songThatJustStopped,
            actualStoppedPosition,
            actualStoppedSongDuration,
            _explicitStopRequested
        );

        ApplyCompletionResult(result);
    }

    private void ApplyCompletionResult(PlaybackCompletionResult result)
    {
        Debug.WriteLine($"[SessionManager] Applying CompletionResult: NextSong='{result.NextSong?.Title ?? "null"}', FinalStatus={result.FinalStatus}, TriggerNaturalEnd={result.TriggerSessionEndedNaturally}");

        if (result.StopUiUpdateMonitor)
        {
            // _playbackEngineCoordinator.Stop() also stops the monitor.
            // If the engine is already stopped, this might be redundant but safe.
            // Or, more precisely, call _infrastructureProvider.Monitor.Stop() if only monitor needs stopping.
            // For now, assuming _playbackEngineCoordinator.Stop() is sufficient if general stop is implied.
            // If engine might not be fully stopped by NAudio event yet, this ensures monitor is.
            _playbackEngineCoordinator.Stop(); // This stops monitor and engine if not already.
        }

        _sessionState.CurrentSong = result.NextSong;
        _sessionState.IsPlaying = result.ShouldBePlaying;
        _sessionState.CurrentPlaybackStatus = result.FinalStatus;

        if (result.NextSong == null && result.FinalStatus == PlaybackStateStatus.Stopped)
        {
            _sessionState.ResetToDefault(); // Clears position, duration etc.
        }
        else
        {
            // If there's a next song or still managing state, ensure _playbackEngineCoordinator's song context is updated.
            _playbackEngineCoordinator.SetSong(result.NextSong);
        }

        if (result.TriggerSessionEndedNaturally)
        {
            SessionEndedNaturally?.Invoke(this, EventArgs.Empty);
        }

        _explicitStopRequested = false; // Always reset this flag after handling completion.
        Debug.WriteLine($"[SessionManager] ApplyCompletionResult finishes. CurrentSong after: {_sessionState.CurrentSong?.Title ?? "null"}");
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackSessionManager] Dispose called.");
        if (_sessionState != null)
        {
            _sessionState.PropertyChanged -= SessionState_PropertyChanged;
        }
        if (_playbackEngineCoordinator != null)
        {
            _playbackEngineCoordinator.EnginePlaybackStopped -= OnEngineCoordinatorPlaybackStopped;
            _playbackEngineCoordinator.EnginePositionUpdated -= OnEngineCoordinatorPositionUpdated;
            // _playbackEngineCoordinator disposal is handled by _infrastructureProvider
        }
        _infrastructureProvider?.Dispose();
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackSessionManager] Dispose completed.");
    }

    ~PlaybackSessionManager()
    {
        Dispose();
    }
}