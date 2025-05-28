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
    private readonly ScrobblingService _scrobblingService; // For scrobbling on completion

    public Song? CurrentSong
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
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

    public bool IsPlaying
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(IsPlaying));
            }
        }
    }

    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CurrentPlaybackStatus));
            }
        }
    } = PlaybackStateStatus.Stopped;

    public TimeSpan CurrentPosition
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CurrentPosition));
                OnPropertyChanged(nameof(CurrentPositionSeconds));
            }
        }
    }
    public double CurrentPositionSeconds => CurrentPosition.TotalSeconds;

    public TimeSpan CurrentSongDuration
    {
        get;
        private set
        {
            if (field != value)
            {
                field = value;
                OnPropertyChanged(nameof(CurrentSongDuration));
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
            }
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1.0;

    public float PlaybackRate
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                _playbackEngineCoordinator.UpdateRateAndPitch(value, PitchSemitones);
                OnPropertyChanged(nameof(PlaybackRate));
            }
        }
    } = 1.0f;

    public float PitchSemitones
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                _playbackEngineCoordinator.UpdateRateAndPitch(PlaybackRate, value);
                OnPropertyChanged(nameof(PitchSemitones));
            }
        }
    } = 0f;

    private volatile bool _explicitStopRequested = false;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SessionEndedNaturally;

    // Reference to PlaybackService is needed for PlaybackCompletionHandler
    // This is a bit circular, ideally PlaybackCompletionHandler would take callbacks
    // or PlaybackSessionManager would own more of the state updates.
    // For this refactor, we pass PlaybackService.
    private readonly PlaybackService _playbackServiceReference;


    public PlaybackSessionManager(
        PlaybackEngineCoordinator playbackEngineCoordinator,
        ScrobblingService scrobblingService,
        PlaybackService playbackServiceReference) // Pass PlaybackService for CompletionHandler
    {
        _playbackEngineCoordinator = playbackEngineCoordinator ?? throw new ArgumentNullException(nameof(playbackEngineCoordinator));
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        _playbackServiceReference = playbackServiceReference ?? throw new ArgumentNullException(nameof(playbackServiceReference));

        _completionHandler = new PlaybackCompletionHandler(_playbackServiceReference, _scrobblingService);

        _playbackEngineCoordinator.EnginePlaybackStopped += OnEngineCoordinatorPlaybackStopped;
        _playbackEngineCoordinator.EnginePositionUpdated += OnEngineCoordinatorPositionUpdated;
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
            _playbackEngineCoordinator.Stop(); // Stop current, will trigger OnEngineCoordinatorPlaybackStopped
        }
        else if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped && CurrentSong == song)
        {
            _playbackEngineCoordinator.Stop(); // Restart same song
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
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SessionManager] Error starting new session for '{CurrentSong?.FilePath}': {ex}");
            if (CurrentSong != null) _playbackEngineCoordinator.Stop(); // Ensure cleanup
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
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            StartNewSession(CurrentSong); // Effectively re-plays
        }
    }

    public void StopSession(bool isExplicit)
    {
        _explicitStopRequested = isExplicit;
        if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _playbackEngineCoordinator.Stop(); // Will trigger OnEngineCoordinatorPlaybackStopped
        }
        else if (isExplicit) // If already stopped but an explicit request comes, process it
        {
            _completionHandler.Handle(
               new StoppedEventArgs(),
               CurrentSong,
               this.CurrentPosition, // Use SessionManager's state
               this.CurrentSongDuration, // Use SessionManager's state
               _explicitStopRequested
           );
        }
    }

    public void SeekSession(TimeSpan requestedPosition)
    {
        if (CurrentSong == null || CurrentSongDuration == TimeSpan.Zero) return;
        _playbackEngineCoordinator.Seek(requestedPosition);
        // Position update comes via OnEngineCoordinatorPositionUpdated if monitor is not running
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
            actualStoppedSongDuration = songThatJustStopped.Duration; // Fallback to metadata duration
        }

        // Important: completion handler will call back into PlaybackService's "internal" methods.
        _completionHandler.Handle(
            e,
            songThatJustStopped,
            actualStoppedPosition,
            actualStoppedSongDuration,
            _explicitStopRequested
        );
    }

    // Methods for PlaybackCompletionHandler to call (public on SessionManager)
    public void StopUiUpdateMonitor() => _playbackEngineCoordinator.Stop(); // Monitor is part of coordinator

    public void UpdateStateForNaturalPlaybackEnd()
    {
        SetPlaybackState(false, PlaybackStateStatus.Stopped);
        UpdatePlaybackPositionAndDuration(TimeSpan.Zero, CurrentSongDuration); // Reset position, keep duration
    }

    public void FinalizeCurrentSong(Song? song)
    {
        CurrentSong = song; // Usually sets to null after stop
    }

    public Song? GetCurrentSongForCompletion() => CurrentSong;


    public void SetPlaybackState(bool isPlaying, PlaybackStateStatus status)
    {
        IsPlaying = isPlaying;
        CurrentPlaybackStatus = status;
    }

    public void TriggerSessionEndedNaturally()
    {
        SessionEndedNaturally?.Invoke(this, EventArgs.Empty);
    }

    public void ResetExplicitStopFlag()
    {
        _explicitStopRequested = false;
    }


    public void Dispose()
    {
        if (_playbackEngineCoordinator != null)
        {
            _playbackEngineCoordinator.EnginePlaybackStopped -= OnEngineCoordinatorPlaybackStopped;
            _playbackEngineCoordinator.EnginePositionUpdated -= OnEngineCoordinatorPositionUpdated;
            // _playbackEngineCoordinator is disposed by PlaybackService, which owns it.
        }
        GC.SuppressFinalize(this);
    }

    ~PlaybackSessionManager()
    {
        Dispose();
    }
}