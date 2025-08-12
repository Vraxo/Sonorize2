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
    private Song? _songBeingReplaced;

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

        // --- Step 1: Stop current playback if any ---
        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong != null)
        {
            Debug.WriteLine($"[SessionManager] Current song '{CurrentSong.Title}' ({CurrentPlaybackStatus}) will be stopped for new/re-clicked song '{song?.Title ?? "null song"}'.");
            _songBeingReplaced = CurrentSong; // Flag that this stop is part of a song switch
            _explicitStopRequested = false;
            _playbackEngineCoordinator.Stop();
        }
        else
        {
            _songBeingReplaced = null; // Nothing was playing, so nothing is being replaced.
        }

        // --- Step 2: Validate new song ---
        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[SessionManager] New song is null or invalid. Playback will stop (if it was running).");
            // If nothing was playing initially, we need to ensure the state is clean.
            if (_songBeingReplaced == null)
            {
                FinalizeCurrentSong(null); // Clears song, position, etc.
            }
            return false;
        }

        // --- Step 3: Load the new song ---
        var result = _sessionLoader.LoadNewSession(song);
        if (!result)
        {
            // Loading failed. The state should already be 'Stopped' and clean from the loader.
            // The stop event from the old song might still be pending. We need to clear _songBeingReplaced
            // so that the stop event doesn't get confused.
            Debug.WriteLine($"[SessionManager] New session load failed for '{song.Title}'.");
            _songBeingReplaced = null;
        }

        return result;
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
        // If a song was being replaced, this stop event belongs to it.
        // Otherwise, it belongs to the CurrentSong.
        Song? songThatJustStopped = _songBeingReplaced ?? CurrentSong;
        if (_songBeingReplaced != null)
        {
            Debug.WriteLine($"[SessionManager] OnEngineCoordinatorPlaybackStopped: Attributing stop event to the song that was being replaced: '{_songBeingReplaced.Title}'.");
            _songBeingReplaced = null; // Consume it.
        }

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