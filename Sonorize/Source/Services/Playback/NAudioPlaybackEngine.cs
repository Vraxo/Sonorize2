using System;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace Sonorize.Services;

public class NAudioPlaybackEngine : IDisposable
{
    private IWavePlayer? _waveOutDevice;
    private AudioFileReader? audioFileReader;
    private NAudioEffectsProcessor? _effectsProcessor;

    private IWavePlayer? _waveOutDeviceInstanceForEvent;

    public event EventHandler<StoppedEventArgs>? PlaybackStopped;

    public TimeSpan CurrentPosition
    {
        get => audioFileReader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (audioFileReader != null) audioFileReader.CurrentTime = value;
            else Debug.WriteLine("[Engine] Attempted to set CurrentPosition on null audioFileReader.");
        }
    }

    public TimeSpan CurrentSongDuration => audioFileReader?.TotalTime ?? TimeSpan.Zero;

    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get
        {
            if (_waveOutDevice == null) return PlaybackStateStatus.Stopped;
            return _waveOutDevice.PlaybackState switch
            {
                NAudio.Wave.PlaybackState.Playing => PlaybackStateStatus.Playing,
                NAudio.Wave.PlaybackState.Paused => PlaybackStateStatus.Paused,
                NAudio.Wave.PlaybackState.Stopped => PlaybackStateStatus.Stopped,
                _ => PlaybackStateStatus.Stopped
            };
        }
    }

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            _playbackRate = value;
            if (_effectsProcessor != null) _effectsProcessor.Tempo = value;
        }
    }

    private float _pitchSemitones = 0f;
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            _pitchSemitones = value;
            if (_effectsProcessor != null) _effectsProcessor.PitchSemitones = value;
        }
    }


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

        Dispose(disposing: true);

        try
        {
            audioFileReader = new AudioFileReader(filePath);
            Debug.WriteLine($"[Engine] Loaded AudioFileReader. Channels: {audioFileReader.WaveFormat.Channels}, SampleRate: {audioFileReader.WaveFormat.SampleRate}, Duration: {audioFileReader.TotalTime}");

            _effectsProcessor = new NAudioEffectsProcessor();
            _effectsProcessor.Initialize(audioFileReader.ToSampleProvider());

            _effectsProcessor.Tempo = PlaybackRate;
            _effectsProcessor.PitchSemitones = PitchSemitones;
            Debug.WriteLine($"[Engine] Effects Processor initialized. Applied Tempo: {_effectsProcessor.Tempo}, Pitch: {_effectsProcessor.PitchSemitones}");

            _waveOutDevice = new WaveOutEvent();
            _waveOutDeviceInstanceForEvent = _waveOutDevice;
            _waveOutDevice.PlaybackStopped += OnWaveOutPlaybackStopped;

            _waveOutDevice.Init(_effectsProcessor.OutputProvider.ToWaveProvider());

            Debug.WriteLine($"[Engine] NAudio pipeline loaded successfully for: {Path.GetFileName(filePath)}.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Engine] CRITICAL ERROR during NAudio pipeline Load for {Path.GetFileName(filePath)}: {ex.ToString()}");
            Dispose(disposing: true);
            throw new Exception($"Failed to load audio pipeline for {Path.GetFileName(filePath)}", ex);
        }
    }

    public void Play()
    {
        Debug.WriteLine("[Engine] Play requested.");
        if (_waveOutDevice != null && (_waveOutDevice.PlaybackState == NAudio.Wave.PlaybackState.Paused || _waveOutDevice.PlaybackState == NAudio.Wave.PlaybackState.Stopped))
        {
            Debug.WriteLine("[Engine] Calling device.Play().");
            try
            {
                _waveOutDevice.Play();
                Debug.WriteLine($"[Engine] Playback started/resumed. State: {CurrentPlaybackStatus}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Engine] Error during device.Play(): {ex.Message}");
            }
        }
        else if (_waveOutDevice != null && _waveOutDevice.PlaybackState == NAudio.Wave.PlaybackState.Playing)
        {
            Debug.WriteLine("[Engine] Already playing. Doing nothing.");
        }
        else
        {
            Debug.WriteLine("[Engine] Cannot Play: Device not initialized.");
        }
    }

    public void Pause()
    {
        Debug.WriteLine("[Engine] Pause requested.");
        if (_waveOutDevice != null && _waveOutDevice.PlaybackState == NAudio.Wave.PlaybackState.Playing)
        {
            Debug.WriteLine("[Engine] Calling device.Pause().");
            try
            {
                _waveOutDevice.Pause();
                Debug.WriteLine($"[Engine] Playback paused. State: {CurrentPlaybackStatus}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Engine] Error during device.Pause(): {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine($"[Engine] Cannot Pause: Device state is {_waveOutDevice?.PlaybackState ?? NAudio.Wave.PlaybackState.Stopped}.");
        }
    }

    public void Stop()
    {
        Debug.WriteLine("[Engine] Stop requested.");
        if (_waveOutDevice != null && _waveOutDevice.PlaybackState != NAudio.Wave.PlaybackState.Stopped)
        {
            Debug.WriteLine("[Engine] Calling device.Stop().");
            try
            {
                _waveOutDevice.Stop();
                Debug.WriteLine($"[Engine] Stop initiated. Device state: {_waveOutDevice?.PlaybackState}. PlaybackStopped event should follow.");
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
        if (audioFileReader != null)
        {
            try
            {
                audioFileReader.CurrentTime = position;
                Debug.WriteLine($"[Engine] Seek successful. Actual position: {audioFileReader.CurrentTime:mm\\:ss\\.ff}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Engine] Error during audioFileReader.CurrentTime = {position:mm\\:ss\\.ff}: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine("[Engine] Cannot Seek: AudioFileReader not initialized.");
        }
    }

    private void OnWaveOutPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (sender == _waveOutDeviceInstanceForEvent)
        {
            Debug.WriteLine("[Engine] OnWaveOutPlaybackStopped event received from current device.");
            PlaybackStopped?.Invoke(this, e);
        }
        else
        {
            Debug.WriteLine("[Engine] OnWaveOutPlaybackStopped event received from old device instance. Ignoring.");
        }
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
            if (_waveOutDevice != null && _waveOutDeviceInstanceForEvent == _waveOutDevice)
            {
                _waveOutDevice.PlaybackStopped -= OnWaveOutPlaybackStopped;
                Debug.WriteLine("[Engine] Detached PlaybackStopped handler.");
            }
            _waveOutDeviceInstanceForEvent = null;

            if (_waveOutDevice != null)
            {
                Debug.WriteLine($"[Engine] Disposing WaveOutDevice (State: {_waveOutDevice.PlaybackState}).");
                try { _waveOutDevice.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[Engine] Error disposing WaveOutDevice: {ex.Message}"); }
                _waveOutDevice = null;
            }

            if (_effectsProcessor != null)
            {
                Debug.WriteLine("[Engine] Disposing Effects Processor.");
                try { _effectsProcessor.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[Engine] Error disposing Effects Processor: {ex.Message}"); }
                _effectsProcessor = null;
            }

            if (audioFileReader != null)
            {
                Debug.WriteLine("[Engine] Disposing AudioFileReader.");
                try { audioFileReader.Dispose(); } catch (Exception ex) { Debug.WriteLine($"[Engine] Error disposing AudioFileReader: {ex.Message}"); }
                audioFileReader = null;
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