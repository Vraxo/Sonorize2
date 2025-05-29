using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using NAudio.Wave; // For StoppedEventArgs
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackSessionManager : INotifyPropertyChanged, IDisposable
{
    private readonly PlaybackEngineCoordinator _playbackEngineCoordinator;
    private readonly PlaybackCompletionHandler _completionHandler;
    private readonly ScrobblingService _scrobblingService;
    private readonly PlaybackSessionState _sessionState;
    private readonly PlaybackSessionLoader _sessionLoader; // New helper

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
    private bool _isEngineReleaseExpected = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SessionEndedNaturally;

    public PlaybackSessionManager(ScrobblingService scrobblingService, PlaybackLoopHandler loopHandler)
    {
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        _sessionState = new PlaybackSessionState();
        _sessionState.PropertyChanged += SessionState_PropertyChanged;

        var engineController = new NAudioEngineController();
        _playbackEngineCoordinator = new PlaybackEngineCoordinator(engineController, loopHandler, new PlaybackMonitor(engineController, loopHandler));
        _completionHandler = new PlaybackCompletionHandler(this, _scrobblingService);
        _sessionLoader = new PlaybackSessionLoader(_playbackEngineCoordinator, _sessionState, _scrobblingService); // Initialize new helper


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
        // This logic is now primarily handled by PlaybackSessionLoader post-load
        // and by this method for runtime updates.
        _sessionState.CurrentPosition = position;
        _sessionState.CurrentSongDuration = duration;
    }

    public bool StartNewSession(Song song)
    {
        Debug.WriteLine($"[SessionManager] StartNewSession requested for: {(song?.Title ?? "null song")}");

        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong != null && CurrentSong != song)
        {
            _playbackEngineCoordinator.Stop(); // Stop previous different song
        }
        else if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong == song)
        {
            _playbackEngineCoordinator.Stop(); // Stop and restart same song
        }

        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[SessionManager] New song is null or invalid. Stopping current playback if any.");
            if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped) _playbackEngineCoordinator.Stop();
            _sessionState.CurrentSong = null;
            SetPlaybackState(false, PlaybackStateStatus.Stopped);
            return false;
        }

        _explicitStopRequested = false;
        return _sessionLoader.LoadNewSession(song);
    }

    public bool ReinitializePlayback(Song song, TimeSpan position, bool shouldBePlaying)
    {
        Debug.WriteLine($"[SessionManager] ReinitializePlayback for: {song.Title}, Position: {position}, ShouldPlay: {shouldBePlaying}");
        _explicitStopRequested = false;

        // Ensure any previous engine state is cleared if it was for a different song or needs full reload
        // This might be implicitly handled by PlaybackEngineCoordinator.Load if it disposes previous
        // For safety, if the song is different, ensure a full stop.
        if (CurrentSong != song && CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _playbackEngineCoordinator.Stop();
        }

        return _sessionLoader.ReloadSession(song, position, shouldBePlaying);
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
            // ReinitializePlayback handles loading, seeking, and starting play
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
        else if (isExplicit) // If already stopped but an explicit stop is requested
        {
            _completionHandler.Handle(
              new StoppedEventArgs(), // Empty args as it's an artificial stop event
              CurrentSong,
              this.CurrentPosition, // Use current state's position/duration
              this.CurrentSongDuration,
              _explicitStopRequested
          );
        }
    }

    public (bool WasPlaying, TimeSpan Position)? StopAndReleaseFileResources()
    {
        if (CurrentSong == null)
        {
            Debug.WriteLine("[SessionManager] StopAndReleaseFileResources: No current song. Nothing to release.");
            return null;
        }

        Debug.WriteLine($"[SessionManager] StopAndReleaseFileResources for: {CurrentSong.Title}");
        bool wasPlaying = IsPlaying;
        TimeSpan position = CurrentPosition;

        _isEngineReleaseExpected = true; // Signal that the upcoming PlaybackStopped event is expected
        _playbackEngineCoordinator.DisposeCurrentEngineInternals();
        _isEngineReleaseExpected = false;

        // Manually update state as the engine is gone and won't report stopped.
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
        // _sessionState.CurrentPosition remains as it was, which is correct.

        Debug.WriteLine($"[SessionManager] File resources released for {CurrentSong.Title}. WasPlaying: {wasPlaying}, Position: {position}");
        return (wasPlaying, position);
    }

    public void SeekSession(TimeSpan requestedPosition)
    {
        if (CurrentSong == null || CurrentSongDuration == TimeSpan.Zero) return;
        _playbackEngineCoordinator.Seek(requestedPosition);
    }

    private void OnEngineCoordinatorPositionUpdated(object? sender, PositionEventArgs e)
    {
        UpdatePlaybackPositionAndDuration(e.Position, e.Duration);
    }

    private void OnEngineCoordinatorPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_isEngineReleaseExpected)
        {
            Debug.WriteLine("[SessionManager] OnEngineCoordinatorPlaybackStopped: Event ignored as engine release was expected.");
            return; // Don't process if we explicitly disposed the engine
        }

        Song? songThatJustStopped = CurrentSong;
        TimeSpan actualStoppedPosition = _playbackEngineCoordinator.CurrentPosition; // Get final position from engine
        TimeSpan actualStoppedSongDuration = _playbackEngineCoordinator.CurrentSongDuration;

        // If duration from engine is zero (e.g., error before full load), use song's metadata duration
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
    }

    // Methods called by PlaybackCompletionHandler
    internal void StopUiUpdateMonitor() => _playbackEngineCoordinator.Stop(); // This might be too aggressive; monitor stops itself. Consider if this is needed.
    internal void UpdateStateForNaturalPlaybackEnd()
    {
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
        // Position should remain at end of song or be reset by PlaybackSessionState.ResetToDefault
        // _sessionState.CurrentPosition = _sessionState.CurrentSongDuration; // or _sessionState.ResetToDefault();
    }
    internal void FinalizeCurrentSong(Song? song)
    {
        _sessionState.CurrentSong = song; // This will trigger HasCurrentSong update
        if (song == null)
        {
            _sessionState.ResetToDefault();
        }
        _playbackEngineCoordinator.SetSong(song); // Keep coordinator in sync
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
            _playbackEngineCoordinator.Dispose();
        }
        // _completionHandler does not currently implement IDisposable
        // _sessionLoader does not currently implement IDisposable
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackSessionManager] Dispose completed.");
    }

    ~PlaybackSessionManager()
    {
        Dispose();
    }
}