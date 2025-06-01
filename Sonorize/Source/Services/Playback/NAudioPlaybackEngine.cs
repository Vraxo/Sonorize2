using System;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;
using Sonorize.Services.Playback; // Added for NAudioPipeline

namespace Sonorize.Services;

public class NAudioPlaybackEngine : IDisposable
{
    private NAudioPipeline? _pipeline;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public TimeSpan CurrentPosition
    {
        get => _pipeline?.AudioReader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_pipeline?.AudioReader != null) _pipeline.AudioReader.CurrentTime = value;
            else Debug.WriteLine("[Engine] Attempted to set CurrentPosition on null pipeline/audioReader.");
        }
    }

    public TimeSpan CurrentSongDuration => _pipeline?.AudioReader?.TotalTime ?? TimeSpan.Zero;

    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get
        {
            if (_pipeline?.OutputDevice == null) return PlaybackStateStatus.Stopped;
            return _pipeline.OutputDevice.PlaybackState switch
            {
                NAudio.Wave.PlaybackState.Playing => PlaybackStateStatus.Playing,
                NAudio.Wave.PlaybackState.Paused => PlaybackStateStatus.Paused,
                NAudio.Wave.PlaybackState.Stopped => PlaybackStateStatus.Stopped,
                _ => PlaybackStateStatus.Stopped
            };
        }
    }

    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            _playbackRate = value;
            if (_pipeline?.EffectsProcessor != null) _pipeline.EffectsProcessor.Tempo = value;
        }
    }
    private float _playbackRate = 1.0f;

    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            _pitchSemitones = value;
            if (_pipeline?.EffectsProcessor != null) _pipeline.EffectsProcessor.PitchSemitones = value;
        }
    }
    private float _pitchSemitones = 0f;


    public NAudioPlaybackEngine()
    {
        Debug.WriteLine("[Engine] Constructor called.");
    }

    public void Load(string filePath)
    {
        Debug.WriteLine($"[Engine] Load called for: {Path.GetFileName(filePath)}");

        if (!File.Exists(filePath))
        {
            Debug.WriteLine($"[Engine] Load failed: File not found at {filePath}");
            throw new FileNotFoundException("Audio file not found.", filePath);
        }

        Dispose(disposing: true); // Dispose existing pipeline first

        try
        {
            // Properties PlaybackRate and PitchSemitones are used by NAudioPipeline constructor
            _pipeline = new NAudioPipeline(filePath, PlaybackRate, PitchSemitones, OnPipelinePlaybackStopped);
            Debug.WriteLine($"[Engine] NAudio pipeline loaded successfully via NAudioPipeline for: {Path.GetFileName(filePath)}.");
        }
        catch (Exception ex)
        {
            // NAudioPipeline constructor re-throws on failure after its own cleanup.
            Debug.WriteLine($"[Engine] CRITICAL ERROR during NAudioPipeline creation for {Path.GetFileName(filePath)}: {ex.ToString()}");
            Dispose(disposing: true); // Ensure engine's _pipeline is null
            throw new Exception($"Failed to load audio pipeline for {Path.GetFileName(filePath)}", ex);
        }
    }

    public void Play()
    {
        Debug.WriteLine("[Engine] Play requested.");
        if (_pipeline?.OutputDevice != null && (_pipeline.OutputDevice.PlaybackState == NAudio.Wave.PlaybackState.Paused || _pipeline.OutputDevice.PlaybackState == NAudio.Wave.PlaybackState.Stopped))
        {
            Debug.WriteLine("[Engine] Calling device.Play().");
            try
            {
                _pipeline.OutputDevice.Play();
                Debug.WriteLine($"[Engine] Playback started/resumed. State: {CurrentPlaybackStatus}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Engine] Error during device.Play(): {ex.Message}");
            }
        }
        else if (_pipeline?.OutputDevice != null && _pipeline.OutputDevice.PlaybackState == NAudio.Wave.PlaybackState.Playing)
        {
            Debug.WriteLine("[Engine] Already playing. Doing nothing.");
        }
        else
        {
            Debug.WriteLine("[Engine] Cannot Play: Device not initialized via pipeline.");
        }
    }

    public void Pause()
    {
        Debug.WriteLine("[Engine] Pause requested.");
        if (_pipeline?.OutputDevice != null && _pipeline.OutputDevice.PlaybackState == NAudio.Wave.PlaybackState.Playing)
        {
            Debug.WriteLine("[Engine] Calling device.Pause().");
            try
            {
                _pipeline.OutputDevice.Pause();
                Debug.WriteLine($"[Engine] Playback paused. State: {CurrentPlaybackStatus}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Engine] Error during device.Pause(): {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine($"[Engine] Cannot Pause: Device state is {_pipeline?.OutputDevice?.PlaybackState ?? NAudio.Wave.PlaybackState.Stopped}.");
        }
    }

    public void Stop()
    {
        Debug.WriteLine("[Engine] Stop requested.");
        if (_pipeline?.OutputDevice != null && _pipeline.OutputDevice.PlaybackState != NAudio.Wave.PlaybackState.Stopped)
        {
            Debug.WriteLine("[Engine] Calling device.Stop().");
            try
            {
                _pipeline.OutputDevice.Stop();
                Debug.WriteLine($"[Engine] Stop initiated. Device state: {_pipeline?.OutputDevice?.PlaybackState}. PlaybackStopped event should follow.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Engine] Error during device.Stop(): {ex.Message}. PlaybackStopped event may not fire.");
            }
        }
        else
        {
            Debug.WriteLine("[Engine] Already stopped or not initialized. Doing nothing.");
        }
    }

    public void Seek(TimeSpan position)
    {
        Debug.WriteLine($"[Engine] Seek requested to {position:mm\\:ss\\.ff}.");
        if (_pipeline?.AudioReader != null)
        {
            try
            {
                _pipeline.AudioReader.CurrentTime = position;
                Debug.WriteLine($"[Engine] Seek successful. Actual position: {_pipeline.AudioReader.CurrentTime:mm\\:ss\\.ff}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Engine] Error during audioFileReader.CurrentTime = {position:mm\\:ss\\.ff}: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine("[Engine] Cannot Seek: AudioFileReader (via pipeline) not initialized.");
        }
    }

    private void OnPipelinePlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // The sender here will be the NAudioPipeline instance, or its OutputDevice.
        // We trust that NAudioPipeline correctly manages its event subscriptions.
        Debug.WriteLine("[Engine] OnPipelinePlaybackStopped event received from NAudioPipeline.");
        PlaybackStopped?.Invoke(this, e); // Raise engine's own event
    }


    public void Dispose()
    {
        Debug.WriteLine("[Engine] Dispose() called.");
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
        Debug.WriteLine("[Engine] Dispose() completed.");
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_pipeline != null)
            {
                // Unsubscribe from _pipeline events if any were directly subscribed by this class
                // (Currently, NAudioPipeline forwards its OutputDevice.PlaybackStopped to OnPipelinePlaybackStopped,
                // so no direct subscription to _pipeline here that needs unsubscribing).
                _pipeline.Dispose();
                _pipeline = null;
            }
        }
    }

    ~NAudioPlaybackEngine()
    {
        Debug.WriteLine("[Engine] Finalizer called for NAudioPlaybackEngine.");
        Dispose(disposing: false);
        Debug.WriteLine("[Engine] Finalizer completed for NAudioPlaybackEngine.");
    }
}