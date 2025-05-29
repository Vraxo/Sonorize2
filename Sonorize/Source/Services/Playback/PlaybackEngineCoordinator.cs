using System;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackEngineCoordinator : IDisposable
{
    private readonly NAudioEngineController _engineController;
    private readonly PlaybackLoopHandler _loopHandler;
    private readonly PlaybackMonitor _playbackMonitor;
    private Song? _currentSong;
    private bool _disposed = false;

    public event EventHandler<StoppedEventArgs>? EnginePlaybackStopped;
    public event EventHandler<PositionEventArgs>? EnginePositionUpdated;

    public TimeSpan CurrentPosition => _engineController.CurrentPosition;
    public TimeSpan CurrentSongDuration => _engineController.CurrentSongDuration;
    public PlaybackStateStatus CurrentPlaybackStatus => _engineController.CurrentPlaybackStatus;

    public PlaybackEngineCoordinator(NAudioEngineController engineController, PlaybackLoopHandler loopHandler, PlaybackMonitor playbackMonitor)
    {
        _engineController = engineController ?? throw new ArgumentNullException(nameof(engineController));
        _loopHandler = loopHandler ?? throw new ArgumentNullException(nameof(loopHandler));
        _playbackMonitor = playbackMonitor ?? throw new ArgumentNullException(nameof(playbackMonitor));

        _engineController.PlaybackStopped += OnEngineControllerPlaybackStoppedRelay;
        Debug.WriteLine("[PlaybackEngineCoordinator] Initialized.");
    }

    private void OnEngineControllerPlaybackStoppedRelay(object? sender, StoppedEventArgs e)
    {
        EnginePlaybackStopped?.Invoke(this, e);
    }

    private void OnMonitorPositionUpdate(TimeSpan position, TimeSpan duration)
    {
        EnginePositionUpdated?.Invoke(this, new PositionEventArgs(position, duration));
    }

    public void SetSong(Song? song)
    {
        _currentSong = song;
        _loopHandler.UpdateCurrentSong(song);
        Debug.WriteLine($"[PlaybackEngineCoordinator] SetSong: {song?.Title ?? "null"}");
    }

    public bool Load(string filePath, float rate, float pitch)
    {
        Debug.WriteLine($"[PlaybackEngineCoordinator] Load called for: {Path.GetFileName(filePath)}");
        try
        {
            _engineController.PlaybackRate = rate;
            _engineController.PitchSemitones = pitch;
            _engineController.Load(filePath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackEngineCoordinator] Error loading: {ex.Message}");
            return false;
        }
    }

    public void Play(bool startMonitor)
    {
        if (_currentSong == null)
        {
            Debug.WriteLine("[PlaybackEngineCoordinator] Play called but no current song. Aborting.");
            return;
        }

        TimeSpan initialPosition = _loopHandler.GetInitialPlaybackPosition(_engineController.CurrentSongDuration);
        if (initialPosition > TimeSpan.Zero && initialPosition < _engineController.CurrentSongDuration)
        {
            _engineController.Seek(initialPosition);
        }
        _engineController.Play();
        if (startMonitor)
        {
            _playbackMonitor.Start(_currentSong, OnMonitorPositionUpdate);
        }
        Debug.WriteLine($"[PlaybackEngineCoordinator] Play initiated for {_currentSong.Title}. Monitor started: {startMonitor}");
    }

    public void Pause()
    {
        _playbackMonitor.Stop();
        _engineController.Pause();
        Debug.WriteLine("[PlaybackEngineCoordinator] Pause initiated.");
    }

    public void Resume(bool startMonitor)
    {
        if (_currentSong == null)
        {
            Debug.WriteLine("[PlaybackEngineCoordinator] Resume called but no current song. Aborting.");
            return;
        }
        _engineController.Play(); // NAudio handles resuming from paused or re-playing from stopped if applicable
        if (startMonitor)
        {
            _playbackMonitor.Start(_currentSong, OnMonitorPositionUpdate);
        }
        Debug.WriteLine($"[PlaybackEngineCoordinator] Resume initiated for {_currentSong.Title}. Monitor started: {startMonitor}");
    }

    public void Stop()
    {
        _playbackMonitor.Stop();
        _engineController.Stop(); // This will trigger the EnginePlaybackStopped event
        Debug.WriteLine("[PlaybackEngineCoordinator] Stop initiated.");
    }

    public void DisposeCurrentEngineInternals() // New method
    {
        _playbackMonitor.Stop(); // Stop monitor before disposing engine
        _engineController.DisposeEngineInternalsOnly();
        Debug.WriteLine("[PlaybackEngineCoordinator] Disposed current engine internals.");
    }

    public void Seek(TimeSpan requestedPosition)
    {
        if (_currentSong == null || _engineController.CurrentSongDuration == TimeSpan.Zero) return;

        TimeSpan targetPosition = _loopHandler.GetAdjustedSeekPosition(requestedPosition, _engineController.CurrentSongDuration);

        var totalMs = _engineController.CurrentSongDuration.TotalMilliseconds;
        var seekMarginMs = totalMs > 200 ? 100 : (totalMs > 0 ? Math.Min(totalMs / 2, 50) : 0);
        var maxSeekablePosition = TimeSpan.FromMilliseconds(totalMs - seekMarginMs);
        if (maxSeekablePosition < TimeSpan.Zero) maxSeekablePosition = TimeSpan.Zero;

        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, maxSeekablePosition.TotalSeconds));

        _engineController.Seek(targetPosition);
        Debug.WriteLine($"[PlaybackEngineCoordinator] Seek to {targetPosition} initiated.");

        // If not playing, the monitor won't update position, so fire an event manually
        if (_engineController.CurrentPlaybackStatus != PlaybackStateStatus.Playing)
        {
            OnMonitorPositionUpdate(_engineController.CurrentPosition, _engineController.CurrentSongDuration);
        }
    }

    public void UpdateRateAndPitch(float rate, float pitch)
    {
        _engineController.PlaybackRate = rate;
        _engineController.PitchSemitones = pitch;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            Debug.WriteLine("[PlaybackEngineCoordinator] Dispose called.");
            _playbackMonitor?.Dispose();
            if (_engineController != null)
            {
                _engineController.PlaybackStopped -= OnEngineControllerPlaybackStoppedRelay;
                _engineController.Dispose();
            }
            // _loopHandler is not owned by PlaybackEngineCoordinator, so it's not disposed here.
            // Its lifecycle is managed by PlaybackService.
            Debug.WriteLine("[PlaybackEngineCoordinator] Dispose completed.");
        }
        _disposed = true;
    }

    ~PlaybackEngineCoordinator()
    {
        Dispose(false);
    }
}

// Helper class for event arguments
public class PositionEventArgs(TimeSpan position, TimeSpan duration) : EventArgs
{
    public TimeSpan Position { get; } = position;
    public TimeSpan Duration { get; } = duration;
}