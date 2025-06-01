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
            if (_sessionState.PlaybackRate == value)
            {
                return;
            }

            _sessionState.PlaybackRate = value;
            _playbackEngineCoordinator.UpdateRateAndPitch(value, PitchSemitones);
        }
    }

    public float PitchSemitones
    {
        get => _sessionState.PitchSemitones;
        set
        {
            if (_sessionState.PitchSemitones == value)
            {
                return;
            }

            _sessionState.PitchSemitones = value;
            _playbackEngineCoordinator.UpdateRateAndPitch(PlaybackRate, value);
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

        _completionHandler = new PlaybackCompletionHandler(this, _scrobblingService);
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

        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[SessionManager] New song is null or invalid. Stopping current playback if any.");
            if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
            {
                // This stop is an internal mechanism due to invalid new song.
                _explicitStopRequested = false;
                _playbackEngineCoordinator.Stop();
            }
            else // Already stopped, just ensure state reflects no song.
            {
                _sessionState.CurrentSong = null;
                SetPlaybackState(false, PlaybackStateStatus.Stopped);
            }
            return false;
        }

        // If a song is currently playing or paused, and it's different from the new song,
        // or if it's the same song (re-click), it needs to be stopped first.
        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong != null)
        {
            Debug.WriteLine($"[SessionManager] Current song '{CurrentSong.Title}' ({CurrentPlaybackStatus}) will be stopped for new/re-clicked song '{song.Title}'.");
            // This stop is internal to starting/restarting a song, not an explicit user "stop all playback" request.
            // So, _explicitStopRequested must be false to ensure PlaybackCompletionHandler treats it as an internal stop.
            _explicitStopRequested = false;
            _playbackEngineCoordinator.Stop();
            // The PlaybackStopped event for the old song will be handled asynchronously.
            // The PlaybackCompletionHandler logic for internal stops should prevent interference
            // with the new song's session that is about to be loaded.
        }

        // _explicitStopRequested is now definitely false (either set above, or was already false if nothing was playing).
        // This flag primarily informs the PlaybackCompletionHandler.
        // For the new session being loaded by _sessionLoader, the concept of an "explicit stop" doesn't apply yet.
        return _sessionLoader.LoadNewSession(song);
    }

    public void PauseSession()
    {
        if (IsPlaying)
        {
            _playbackEngineCoordinator.Pause();
            SetPlaybackState(false, PlaybackStateStatus.Paused);
        }
    }

    public void ResumeSession()
    {
        if (CurrentSong == null) return;

        if (CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            _playbackEngineCoordinator.Resume(startMonitor: true);
            SetPlaybackState(true, PlaybackStateStatus.Playing);
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            _sessionLoader.ReloadSession(CurrentSong, CurrentPosition, true);
        }
    }

    public void StopSession(bool isExplicit)
    {
        _explicitStopRequested = isExplicit; // Set the flag based on the nature of this stop call
        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _playbackEngineCoordinator.Stop(); // This will trigger OnEngineCoordinatorPlaybackStopped
                                               // which will then use the _explicitStopRequested flag.
        }
        else if (isExplicit) // If already stopped but this is an explicit "make sure it's really stopped and finalized" call
        {
            // Manually invoke completion handler to ensure cleanup/scrobble logic for an explicit stop.
            // This simulates the event for consistency if the engine was already stopped.
            _completionHandler.Handle(
              new StoppedEventArgs(), // Empty args as engine didn't fire it
              CurrentSong,
              this.CurrentPosition,
              this.CurrentSongDuration,
              _explicitStopRequested // will be true here
          );
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
        SetPlaybackState(false, PlaybackStateStatus.Stopped); // Position remains, state is stopped
        Debug.WriteLine($"[SessionManager] Engine resources released for {CurrentSong.Title}. State set to Stopped.");
    }

    internal bool ForceReloadAndPlayEngine(Song song, TimeSpan position, bool shouldBePlaying)
    {
        Debug.WriteLine($"[SessionManager] ForceReloadAndPlayEngine for: {song.Title}, Position: {position}, ShouldPlay: {shouldBePlaying}");
        _explicitStopRequested = false; // This is a programmatic reload, not an explicit user stop.
        return _sessionLoader.ReloadSession(song, position, shouldBePlaying);
    }


    private void OnEngineCoordinatorPositionUpdated(object? sender, PositionEventArgs e)
    {
        UpdatePlaybackPositionAndDuration(e.Position, e.Duration);
    }

    private void OnEngineCoordinatorPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Song? songThatJustStopped = CurrentSong; // Capture before it's potentially changed by the handler

        // It's crucial that `songThatJustStopped` refers to the song whose engine instance *actually* stopped.
        // If `_sessionLoader.LoadNewSession` has already updated `CurrentSong` by the time this event arrives
        // for the *old* song, then `CurrentSong` here would be the *new* song.
        // This requires careful handling. The `NAudioPipeline` or `NAudioPlaybackEngine` should ideally pass
        // context about which song's playback stopped. For now, assume the event carries the context.
        // Let's refine: `OnEngineCoordinatorPlaybackStopped` itself is for the *current* coordinator's engine.
        // The state of `_explicitStopRequested` is the key.

        TimeSpan actualStoppedPosition = _playbackEngineCoordinator.CurrentPosition; // Position from the engine that stopped
        TimeSpan actualStoppedSongDuration = _playbackEngineCoordinator.CurrentSongDuration; // Duration from the engine that stopped

        // If the engine that stopped had a song, and its duration was zero (e.g., error during load),
        // use the model's duration for scrobbling.
        if (songThatJustStopped != null && actualStoppedSongDuration == TimeSpan.Zero)
        {
            actualStoppedSongDuration = songThatJustStopped.Duration;
        }

        _completionHandler.Handle(
            e,
            songThatJustStopped, // Pass the song that was current when the stop occurred.
            actualStoppedPosition,
            actualStoppedSongDuration,
            _explicitStopRequested // Use the flag as it was when Stop() was called or event occurred.
        );
    }

    // Methods called by PlaybackCompletionHandler
    internal void StopUiUpdateMonitor()
    {
        _infrastructureProvider.Monitor.Stop(); // Stop via infrastructure provider
    }

    internal void UpdateStateForNaturalPlaybackEnd()
    {
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
        // CurrentSong is intentionally kept by PlaybackCompletionHandler for SessionEndedNaturally event
    }
    internal void FinalizeCurrentSong(Song? song)
    {
        _sessionState.CurrentSong = song;
        if (song == null)
        {
            _sessionState.ResetToDefault();
        }
        _playbackEngineCoordinator.SetSong(song); // Inform coordinator about the (new) current song
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
        if (_sessionState != null)
        {
            _sessionState.PropertyChanged -= SessionState_PropertyChanged;
        }
        if (_playbackEngineCoordinator != null)
        {
            _playbackEngineCoordinator.EnginePlaybackStopped -= OnEngineCoordinatorPlaybackStopped;
            _playbackEngineCoordinator.EnginePositionUpdated -= OnEngineCoordinatorPositionUpdated;
        }
        _infrastructureProvider?.Dispose(); // This will dispose the coordinator and engine
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackSessionManager] Dispose completed.");
    }

    ~PlaybackSessionManager()
    {
        Dispose();
    }
}