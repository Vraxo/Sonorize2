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

    private Song? _currentSong;
    public Song? CurrentSong
    {
        get => _currentSong;
        private set
        {
            if (_currentSong != value)
            {
                _currentSong = value;
                OnPropertyChanged(nameof(CurrentSong));
                OnPropertyChanged(nameof(HasCurrentSong));
                _playbackEngineCoordinator.SetSong(value);

                if (value == null)
                {
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    UpdatePlaybackPositionAndDuration(TimeSpan.Zero, TimeSpan.Zero);
                }
            }
        }
    }

    public bool HasCurrentSong => CurrentSong != null;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (_isPlaying != value)
            {
                _isPlaying = value;
                OnPropertyChanged(nameof(IsPlaying));
            }
        }
    }

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get => _currentPlaybackStatus;
        private set
        {
            if (_currentPlaybackStatus != value)
            {
                _currentPlaybackStatus = value;
                OnPropertyChanged(nameof(CurrentPlaybackStatus));
            }
        }
    }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        private set
        {
            if (_currentPosition != value)
            {
                _currentPosition = value;
                OnPropertyChanged(nameof(CurrentPosition));
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
            if (_currentSongDuration != value)
            {
                _currentSongDuration = value;
                OnPropertyChanged(nameof(CurrentSongDuration));
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
            }
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1.0;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (_playbackRate != value)
            {
                _playbackRate = value;
                _playbackEngineCoordinator.UpdateRateAndPitch(value, PitchSemitones);
                OnPropertyChanged(nameof(PlaybackRate));
            }
        }
    }

    private float _pitchSemitones = 0f;
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            if (_pitchSemitones != value)
            {
                _pitchSemitones = value;
                _playbackEngineCoordinator.UpdateRateAndPitch(PlaybackRate, value);
                OnPropertyChanged(nameof(PitchSemitones));
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

        var engineController = new NAudioEngineController();
        _playbackEngineCoordinator = new PlaybackEngineCoordinator(engineController, loopHandler, new PlaybackMonitor(engineController, loopHandler));

        _completionHandler = new PlaybackCompletionHandler(this, _scrobblingService);

        _playbackEngineCoordinator.EnginePlaybackStopped += OnEngineCoordinatorPlaybackStopped;
        _playbackEngineCoordinator.EnginePositionUpdated += OnEngineCoordinatorPositionUpdated;
        Debug.WriteLine("[PlaybackSessionManager] Initialized.");
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdatePlaybackPositionAndDuration(TimeSpan position, TimeSpan duration)
    {
        this.CurrentPosition = position;
        this.CurrentSongDuration = duration;
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
            CurrentSong = null;
            return false;
        }

        CurrentSong = song;
        _explicitStopRequested = false;

        try
        {
            if (!_playbackEngineCoordinator.Load(song.FilePath, PlaybackRate, PitchSemitones))
            {
                CurrentSong = null;
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
            CurrentSong = null;
            return false;
        }
    }

    public bool ReinitializePlayback(Song song, TimeSpan position, bool shouldBePlaying)
    {
        Debug.WriteLine($"[SessionManager] ReinitializePlayback for: {song.Title}, Position: {position}, ShouldPlay: {shouldBePlaying}");
        _explicitStopRequested = false; // Reset any explicit stop flag

        CurrentSong = song; // Ensure current song is set (might be redundant if not nulled before)

        try
        {
            if (!_playbackEngineCoordinator.Load(song.FilePath, PlaybackRate, PitchSemitones))
            {
                Debug.WriteLine($"[SessionManager] ReinitializePlayback: Failed to load '{song.Title}'.");
                CurrentSong = null; // Clear song if load fails
                SetPlaybackState(false, PlaybackStateStatus.Stopped);
                return false;
            }

            UpdatePlaybackPositionAndDuration(position, _playbackEngineCoordinator.CurrentSongDuration);
            _playbackEngineCoordinator.Seek(position); // Seek after load

            if (shouldBePlaying)
            {
                _playbackEngineCoordinator.Play(startMonitor: true);
                SetPlaybackState(true, PlaybackStateStatus.Playing);
                _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong);
            }
            else
            {
                // For a paused state, we might need to ensure the engine is truly paused.
                // If Load implicitly starts, we might need to Pause immediately.
                // However, NAudio usually loads into a stopped state.
                // Manually setting position might be enough for a paused-like state.
                // If PlaybackEngineCoordinator.Play is not called, it remains stopped.
                // If it was paused, we set the status to paused.
                SetPlaybackState(false, PlaybackStateStatus.Paused);
            }
            Debug.WriteLine($"[SessionManager] ReinitializePlayback successful for '{song.Title}'. State: {CurrentPlaybackStatus}, IsPlaying: {IsPlaying}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SessionManager] Error during ReinitializePlayback for '{song.FilePath}': {ex}");
            CurrentSong = null;
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
            // If stopped, re-start the session from the current position
            ReinitializePlayback(CurrentSong, CurrentPosition, true);
        }
    }

    public void StopSession(bool isExplicit)
    {
        _explicitStopRequested = isExplicit;
        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _playbackEngineCoordinator.Stop(); // This will trigger OnEngineCoordinatorPlaybackStopped
        }
        else if (isExplicit) // If already stopped but an explicit stop is requested again
        {
            // Manually trigger completion handler if already stopped and an explicit stop is requested
            _completionHandler.Handle(
               new StoppedEventArgs(), // No actual exception
               CurrentSong, // Pass current song if any
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

        _isEngineReleaseExpected = true; // Signal that the upcoming stop is for file release
        _playbackEngineCoordinator.DisposeCurrentEngineInternals(); // This stops playback and disposes NAudio components
        _isEngineReleaseExpected = false;

        // Update state after release
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
        // Keep CurrentPosition as is, CurrentSong is also kept.
        // CurrentSongDuration might become zero if engine is disposed; consider preserving it or re-fetching.
        // For simplicity, we'll re-fetch duration upon reinitialization.

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
            // Do not process completion if it's due to explicit file release
            return;
        }

        Song? songThatJustStopped = CurrentSong;
        TimeSpan actualStoppedPosition = _playbackEngineCoordinator.CurrentPosition; // Get position before it's reset
        TimeSpan actualStoppedSongDuration = _playbackEngineCoordinator.CurrentSongDuration;

        // If the duration became zero (e.g., engine disposed), try to use the song's original duration
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

    // Methods for PlaybackCompletionHandler to call
    internal void StopUiUpdateMonitor() => _playbackEngineCoordinator.Stop(); // This might be redundant if playback stops monitor
    internal void UpdateStateForNaturalPlaybackEnd()
    {
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
    }
    internal void FinalizeCurrentSong(Song? song)
    {
        CurrentSong = song;
    }
    internal Song? GetCurrentSongForCompletion() => CurrentSong;
    internal void SetPlaybackState(bool isPlaying, PlaybackStateStatus status)
    {
        IsPlaying = isPlaying;
        CurrentPlaybackStatus = status;
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