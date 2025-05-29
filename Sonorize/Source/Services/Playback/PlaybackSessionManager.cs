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
                _sessionState.PlaybackRate = value; // Setter on _sessionState will raise its PropertyChanged
                _playbackEngineCoordinator.UpdateRateAndPitch(value, PitchSemitones);
                // OnPropertyChanged(nameof(PlaybackRate)); // Forwarded by SessionState_PropertyChanged
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
                _sessionState.PitchSemitones = value; // Setter on _sessionState will raise its PropertyChanged
                _playbackEngineCoordinator.UpdateRateAndPitch(PlaybackRate, value);
                // OnPropertyChanged(nameof(PitchSemitones)); // Forwarded by SessionState_PropertyChanged
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

        _playbackEngineCoordinator.EnginePlaybackStopped += OnEngineCoordinatorPlaybackStopped;
        _playbackEngineCoordinator.EnginePositionUpdated += OnEngineCoordinatorPositionUpdated;
        Debug.WriteLine("[PlaybackSessionManager] Initialized.");
    }

    private void SessionState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Forward PropertyChanged events from _sessionState
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
            return false;
        }

        _sessionState.CurrentSong = song;
        _playbackEngineCoordinator.SetSong(song); // Keep engine coordinator in sync
        _explicitStopRequested = false;

        try
        {
            if (!_playbackEngineCoordinator.Load(song.FilePath, PlaybackRate, PitchSemitones))
            {
                _sessionState.CurrentSong = null;
                return false;
            }
            UpdatePlaybackPositionAndDuration(TimeSpan.Zero, _playbackEngineCoordinator.CurrentSongDuration);
            _playbackEngineCoordinator.Play(startMonitor: true);
            SetPlaybackState(true, PlaybackStateStatus.Playing);
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SessionManager] Error starting new session for '{CurrentSong?.FilePath}': {ex}");
            if (CurrentSong != null) _playbackEngineCoordinator.Stop();
            _sessionState.CurrentSong = null;
            return false;
        }
    }

    public bool ReinitializePlayback(Song song, TimeSpan position, bool shouldBePlaying)
    {
        Debug.WriteLine($"[SessionManager] ReinitializePlayback for: {song.Title}, Position: {position}, ShouldPlay: {shouldBePlaying}");
        _explicitStopRequested = false;

        _sessionState.CurrentSong = song;
        _playbackEngineCoordinator.SetSong(song); // Keep engine coordinator in sync

        try
        {
            if (!_playbackEngineCoordinator.Load(song.FilePath, PlaybackRate, PitchSemitones))
            {
                Debug.WriteLine($"[SessionManager] ReinitializePlayback: Failed to load '{song.Title}'.");
                _sessionState.CurrentSong = null;
                SetPlaybackState(false, PlaybackStateStatus.Stopped);
                return false;
            }

            UpdatePlaybackPositionAndDuration(position, _playbackEngineCoordinator.CurrentSongDuration);
            _playbackEngineCoordinator.Seek(position);

            if (shouldBePlaying)
            {
                _playbackEngineCoordinator.Play(startMonitor: true);
                SetPlaybackState(true, PlaybackStateStatus.Playing);
                _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
            }
            else
            {
                SetPlaybackState(false, PlaybackStateStatus.Paused);
            }
            Debug.WriteLine($"[SessionManager] ReinitializePlayback successful for '{song.Title}'. State: {CurrentPlaybackStatus}, IsPlaying: {IsPlaying}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SessionManager] Error during ReinitializePlayback for '{song.FilePath}': {ex}");
            _sessionState.CurrentSong = null;
            SetPlaybackState(false, PlaybackStateStatus.Stopped);
            return false;
        }
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
            ReinitializePlayback(CurrentSong, CurrentPosition, true);
        }
    }

    public void StopSession(bool isExplicit)
    {
        _explicitStopRequested = isExplicit;
        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _playbackEngineCoordinator.Stop();
        }
        else if (isExplicit)
        {
            _completionHandler.Handle(
               new StoppedEventArgs(),
               CurrentSong,
               this.CurrentPosition,
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

        _isEngineReleaseExpected = true;
        _playbackEngineCoordinator.DisposeCurrentEngineInternals();
        _isEngineReleaseExpected = false;

        SetPlaybackState(false, PlaybackStateStatus.Stopped);

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
            return;
        }

        Song? songThatJustStopped = CurrentSong;
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
    }

    internal void StopUiUpdateMonitor() => _playbackEngineCoordinator.Stop();
    internal void UpdateStateForNaturalPlaybackEnd()
    {
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
    }
    internal void FinalizeCurrentSong(Song? song)
    {
        _sessionState.CurrentSong = song;
        if (song == null)
        {
            _sessionState.ResetToDefault(); // Or specific reset logic
        }
        _playbackEngineCoordinator.SetSong(song);
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
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackSessionManager] Dispose completed.");
    }

    ~PlaybackSessionManager()
    {
        Dispose();
    }
}