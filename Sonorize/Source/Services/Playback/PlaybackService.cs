﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using Sonorize.Models;
using Sonorize.Services.Playback;
using Sonorize.ViewModels; // For ViewModelBase, if still needed (likely)

namespace Sonorize.Services;

public enum PlaybackStateStatus { Stopped, Playing, Paused } // This enum might be better placed in a shared Models namespace if used by ViewModels too

public class PlaybackService : ViewModelBase, IDisposable
{
    private readonly PlaybackSessionManager _sessionManager;
    private readonly PlaybackLoopHandler _loopHandler; // Keep loop handler if it needs PlaybackService context

    // Properties that mirror PlaybackSessionManager's state
    public Song? CurrentSong => _sessionManager.CurrentSong;
    public bool HasCurrentSong => _sessionManager.HasCurrentSong;
    public bool IsPlaying => _sessionManager.IsPlaying;
    public PlaybackStateStatus CurrentPlaybackStatus => _sessionManager.CurrentPlaybackStatus;
    public TimeSpan CurrentPosition => _sessionManager.CurrentPosition;
    public double CurrentPositionSeconds => _sessionManager.CurrentPositionSeconds;
    public TimeSpan CurrentSongDuration => _sessionManager.CurrentSongDuration;
    public double CurrentSongDurationSeconds => _sessionManager.CurrentSongDurationSeconds;

    public float PlaybackRate
    {
        get => _sessionManager.PlaybackRate;
        set => _sessionManager.PlaybackRate = value;
    }

    public float PitchSemitones
    {
        get => _sessionManager.PitchSemitones;
        set => _sessionManager.PitchSemitones = value;
    }

    public event EventHandler? PlaybackEndedNaturally
    {
        add => _sessionManager.SessionEndedNaturally += value;
        remove => _sessionManager.SessionEndedNaturally -= value;
    }

    // Expose PlaybackSessionManager for PlaybackResourceInterlockService
    internal PlaybackSessionManager SessionManager => _sessionManager;


    public PlaybackService(ScrobblingService scrobblingService)
    {
        Debug.WriteLine("[PlaybackService] Constructor called.");
        _loopHandler = new PlaybackLoopHandler(this);
        _sessionManager = new PlaybackSessionManager(scrobblingService, _loopHandler);
        _sessionManager.PropertyChanged += SessionManager_PropertyChanged;
    }

    private void SessionManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(e.PropertyName);
        if (e.PropertyName == nameof(PlaybackSessionManager.CurrentSong))
        {
            OnPropertyChanged(nameof(HasCurrentSong));
        }
    }

    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService facade] Play requested for: {(song?.Title ?? "null song")}");
        _sessionManager.StartNewSession(song);
    }

    public void Pause()
    {
        Debug.WriteLine($"[PlaybackService facade] Pause requested.");
        _sessionManager.PauseSession();
    }

    public void Resume()
    {
        Debug.WriteLine($"[PlaybackService facade] Resume requested.");
        _sessionManager.ResumeSession();
    }

    public void Stop()
    {
        Debug.WriteLine("[PlaybackService facade] Public Stop() called.");
        _sessionManager.StopSession(isExplicit: true);
    }

    public void Seek(TimeSpan requestedPosition)
    {
        if (CurrentSong == null || CurrentSongDuration == TimeSpan.Zero)
        {
            Debug.WriteLine($"[PlaybackService facade] Seek ignored: No current song or duration is zero.");
            return;
        }
        _sessionManager.SeekSession(requestedPosition);
    }

    internal void PerformSeekInternal(TimeSpan position)
    {
        Seek(position);
    }


    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");
        if (_sessionManager != null)
        {
            _sessionManager.PropertyChanged -= SessionManager_PropertyChanged;
            _sessionManager.Dispose();
        }
        _loopHandler?.Dispose();
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackService] Dispose() completed.");
    }

    ~PlaybackService()
    {
        Dispose();
    }
}