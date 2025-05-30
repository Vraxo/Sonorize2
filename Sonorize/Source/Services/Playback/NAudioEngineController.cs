﻿using System;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace Sonorize.Services.Playback;

public class NAudioEngineController : IDisposable
{
    private NAudioPlaybackEngine? _playbackEngine;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public TimeSpan CurrentPosition => _playbackEngine?.CurrentPosition ?? TimeSpan.Zero;
    public TimeSpan CurrentSongDuration => _playbackEngine?.CurrentSongDuration ?? TimeSpan.Zero;

    public PlaybackStateStatus CurrentPlaybackStatus => _playbackEngine?.CurrentPlaybackStatus ?? PlaybackStateStatus.Stopped;

    public float PlaybackRate
    {
        get;
        set
        {
            field = value;

            if (_playbackEngine is null)
            {
                return;
            }

            _playbackEngine.PlaybackRate = value;
        }
    } = 1.0f;

    public float PitchSemitones
    {
        get;
        set
        {
            field = value;

            if (_playbackEngine is null)
            {
                return;
            }

            _playbackEngine.PitchSemitones = value;
        }
    } = 0f;

    public NAudioEngineController()
    {
        Debug.WriteLine("[EngineController] Constructor called.");
    }

    public void Load(string filePath)
    {
        Debug.WriteLine($"[EngineController] Load requested for: {Path.GetFileName(filePath)}");
        DisposePreviousEngine(); // Ensure any existing engine is disposed before loading a new one

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.WriteLine($"[EngineController] Load failed: File path is invalid or file does not exist at '{filePath}'.");
            throw new FileNotFoundException("Audio file not found or path invalid.", filePath);
        }

        try
        {
            _playbackEngine = new NAudioPlaybackEngine();
            _playbackEngine.PlaybackStopped += OnEnginePlaybackStopped;
            _playbackEngine.PlaybackRate = this.PlaybackRate; // Apply current rate
            _playbackEngine.PitchSemitones = this.PitchSemitones; // Apply current pitch
            _playbackEngine.Load(filePath);
            Debug.WriteLine($"[EngineController] Engine loaded successfully for: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[EngineController] CRITICAL ERROR during engine Load for '{filePath}': {ex.ToString()}");
            DisposePreviousEngine(); // Cleanup on failure
            throw; // Re-throw to allow higher levels to handle
        }
    }

    private void OnEnginePlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Debug.WriteLine("[EngineController] Internal OnEnginePlaybackStopped. Relaying event.");
        PlaybackStopped?.Invoke(this, e);
    }

    public void Play()
    {
        if (_playbackEngine == null)
        {
            Debug.WriteLine("[EngineController] Play ignored: Engine not loaded.");
            return;
        }
        _playbackEngine.Play();
        Debug.WriteLine($"[EngineController] Play initiated. Engine state: {_playbackEngine.CurrentPlaybackStatus}");
    }

    public void Pause()
    {
        if (_playbackEngine == null)
        {
            Debug.WriteLine("[EngineController] Pause ignored: Engine not loaded.");
            return;
        }
        _playbackEngine.Pause();
        Debug.WriteLine($"[EngineController] Pause initiated. Engine state: {_playbackEngine.CurrentPlaybackStatus}");
    }

    public void Stop()
    {
        if (_playbackEngine == null)
        {
            Debug.WriteLine("[EngineController] Stop ignored: Engine not loaded.");
            return;
        }
        _playbackEngine.Stop(); // This will trigger the engine's PlaybackStopped event, then ours
        Debug.WriteLine("[EngineController] Stop initiated on engine.");
    }

    public void DisposeEngineInternalsOnly() // New method
    {
        if (_playbackEngine != null)
        {
            Debug.WriteLine("[EngineController] DisposeEngineInternalsOnly called. Disposing NAudioPlaybackEngine.");
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
        }
    }


    public void Seek(TimeSpan position)
    {
        if (_playbackEngine == null)
        {
            Debug.WriteLine("[EngineController] Seek ignored: Engine not loaded.");
            return;
        }
        _playbackEngine.Seek(position);
        Debug.WriteLine($"[EngineController] Seek initiated to {position}.");
    }

    private void DisposePreviousEngine()
    {
        if (_playbackEngine != null)
        {
            Debug.WriteLine("[EngineController] Disposing previous engine instance.");
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
        }
    }

    public void Dispose()
    {
        Debug.WriteLine("[EngineController] Dispose() called.");
        DisposePreviousEngine();
        GC.SuppressFinalize(this);
        Debug.WriteLine("[EngineController] Dispose() completed.");
    }

    ~NAudioEngineController()
    {
        Debug.WriteLine("[EngineController] Finalizer called.");
        Dispose();
    }
}