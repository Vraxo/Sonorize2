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
    private readonly PlaybackSessionCommandExecutor _commandExecutor; // New

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

        _completionHandler = new PlaybackCompletionHandler(this, _scrobblingService);
        _sessionLoader = new PlaybackSessionLoader(_playbackEngineCoordinator, _sessionState, _scrobblingService);

        // Instantiate the new command executor
        _commandExecutor = new PlaybackSessionCommandExecutor(
            _playbackEngineCoordinator,
            _sessionLoader,
            _sessionState,
            _scrobblingService,
            isExplicit => _explicitStopRequested = isExplicit // Provide action to set flag
        );

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
        return _commandExecutor.ExecuteStartNewSession(song);
    }

    public void PauseSession()
    {
        _commandExecutor.ExecutePauseSession();
    }

    public void ResumeSession()
    {
        _commandExecutor.ExecuteResumeSession();
    }

    public void StopSession(bool isExplicit)
    {
        _commandExecutor.ExecuteStopSession(isExplicit, () =>
        {
            // This action is invoked if already stopped but stop is explicit
            _completionHandler.Handle(
              new StoppedEventArgs(), // Empty args as it's an artificial trigger
              CurrentSong,
              this.CurrentPosition,
              this.CurrentSongDuration,
              _explicitStopRequested // This will be true
            );
        });
    }

    public void SeekSession(TimeSpan requestedPosition)
    {
        _commandExecutor.ExecuteSeekSession(requestedPosition);
    }

    internal void ForceReleaseEngineForCurrentSong()
    {
        _commandExecutor.ExecuteForceReleaseEngineForCurrentSong();
    }

    internal bool ForceReloadAndPlayEngine(Song song, TimeSpan position, bool shouldBePlaying)
    {
        return _commandExecutor.ExecuteForceReloadAndPlayEngine(song, position, shouldBePlaying);
    }


    private void OnEngineCoordinatorPositionUpdated(object? sender, PositionEventArgs e)
    {
        UpdatePlaybackPositionAndDuration(e.Position, e.Duration);
    }

    private void OnEngineCoordinatorPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Song? songThatJustStopped = CurrentSong; // Use property which gets from _sessionState
        TimeSpan actualStoppedPosition = _playbackEngineCoordinator.CurrentPosition; // Get fresh position from coordinator
        TimeSpan actualStoppedSongDuration = _playbackEngineCoordinator.CurrentSongDuration;

        if (songThatJustStopped != null && actualStoppedSongDuration == TimeSpan.Zero)
        {
            actualStoppedSongDuration = songThatJustStopped.Duration; // Fallback to model's duration if engine's is zero
        }

        // The _explicitStopRequested flag is set by the PlaybackSessionCommandExecutor
        // when StopSession is called, before it calls _playbackEngineCoordinator.Stop().
        _completionHandler.Handle(
            e,
            songThatJustStopped,
            actualStoppedPosition,
            actualStoppedSongDuration,
            _explicitStopRequested
        );
    }

    // Methods called by PlaybackCompletionHandler
    internal void StopUiUpdateMonitor() => _playbackEngineCoordinator.Stop(); // Actually, monitor stops itself or coordinator stops it. This might be redundant or for forceful stop.
    internal void UpdateStateForNaturalPlaybackEnd()
    {
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
    }
    internal void FinalizeCurrentSong(Song? song)
    {
        _sessionState.CurrentSong = song; // Update state
        if (song == null)
        {
            _sessionState.ResetToDefault(); // Reset other state fields if song is null
        }
        _playbackEngineCoordinator.SetSong(song); // Inform coordinator
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
    internal void ResetExplicitStopFlag() // Called by CompletionHandler
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