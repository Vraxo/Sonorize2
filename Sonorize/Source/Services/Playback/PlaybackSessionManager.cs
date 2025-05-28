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
                    // This state reset is crucial when the song becomes null
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
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong); // Fire and forget NowPlaying update
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
            _ = _scrobblingService.UpdateNowPlayingAsync(CurrentSong); // Fire and forget NowPlaying update
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            StartNewSession(CurrentSong);
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

    // Methods for PlaybackCompletionHandler to call
    internal void StopUiUpdateMonitor() => _playbackEngineCoordinator.Stop();
    internal void UpdateStateForNaturalPlaybackEnd()
    {
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
        // Position reset when CurrentSong becomes null
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