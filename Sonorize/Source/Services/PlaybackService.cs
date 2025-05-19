using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sonorize.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Timers;

namespace Sonorize.Services;

public enum PlaybackStateStatus
{
    Stopped,
    Playing,
    Paused
}

public class PlaybackService : INotifyPropertyChanged, IDisposable
{
    private IWavePlayer? _waveOutDevice;
    private AudioFileReader? _audioFile;
    private SampleChannel? _sampleChannel;
    private float _volume = 0.5f; // Default volume
    private float _playbackRate = 1.0f; // Default speed
    private float _pitchSemitones = 0.0f; // Default pitch

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    private Song? _currentSong;
    private TimeSpan _currentPosition = TimeSpan.Zero;
    private TimeSpan _currentSongDuration = TimeSpan.Zero;

    private Timer _positionTimer = new Timer(100); // Update position every 100ms

    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get => _currentPlaybackStatus;
        private set => SetProperty(ref _currentPlaybackStatus, value);
    }

    public bool IsPlaying => CurrentPlaybackStatus == PlaybackStateStatus.Playing;
    public bool IsPaused => CurrentPlaybackStatus == PlaybackStateStatus.Paused;
    public bool IsStopped => CurrentPlaybackStatus == PlaybackStateStatus.Stopped;
    public bool HasCurrentSong => _currentSong != null;

    public Song? CurrentSong
    {
        get => _currentSong;
        private set => SetProperty(ref _currentSong, value);
    }

    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        private set
        {
            if (SetProperty(ref _currentPosition, value))
            {
                OnPropertyChanged(nameof(CurrentPositionString)); // Notify for string format
            }
        }
    }

    public string CurrentPositionString => CurrentPosition.TotalSeconds >= 3600 ? CurrentPosition.ToString(@"hh\:mm\:ss") : CurrentPosition.ToString(@"mm\:ss");


    public TimeSpan CurrentSongDuration
    {
        get => _currentSongDuration;
        private set
        {
            if (SetProperty(ref _currentSongDuration, value))
            {
                OnPropertyChanged(nameof(CurrentDurationString)); // Notify for string format
            }
        }
    }

    public string CurrentDurationString => CurrentSongDuration.TotalSeconds >= 3600 ? CurrentSongDuration.ToString(@"hh\:mm\:ss") : CurrentSongDuration.ToString(@"mm\:ss");


    public float Volume
    {
        get => _volume;
        set
        {
            value = Math.Clamp(value, 0.0f, 1.0f);
            if (SetProperty(ref _volume, value))
            {
                if (_sampleChannel != null)
                {
                    _sampleChannel.Volume = _volume;
                }
            }
        }
    }

    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            value = Math.Clamp(value, 0.5f, 2.0f);
            if (SetProperty(ref _playbackRate, value))
            {
                ApplyPlaybackModifiers();
            }
        }
    }

    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            // Round to nearest 0.5 semitone as per UI (but internal allows float)
            // value = (float)Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2.0f;
            value = Math.Clamp(value, -12.0f, 12.0f); // Keep internal range wider than UI
            if (SetProperty(ref _pitchSemitones, value))
            {
                ApplyPlaybackModifiers();
            }
        }
    }


    public PlaybackService()
    {
        _positionTimer.Elapsed += PositionTimer_Elapsed;
        _positionTimer.Start();
    }

    private void PositionTimer_Elapsed(object? sender, ElapsedEventArgs e)
    {
        // This event is on a different thread pool thread.
        // Update UI-bound properties on the UI thread.
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_waveOutDevice?.PlaybackState == PlaybackState.Playing && _audioFile != null)
            {
                CurrentPosition = _audioFile.CurrentTime;
            }
            else if (_currentPlaybackStatus == PlaybackStateStatus.Stopped)
            {
                // Ensure position is reset when stopped
                if (_currentPosition != TimeSpan.Zero)
                {
                    CurrentPosition = TimeSpan.Zero;
                }
            }
        });
    }

    private void ReleaseAudio()
    {
        // Stop timer while changing audio file
        _positionTimer.Stop();

        if (_waveOutDevice != null)
        {
            _waveOutDevice.Stop();
            _waveOutDevice.Dispose();
            _waveOutDevice = null;
        }
        if (_audioFile != null)
        {
            _audioFile.Dispose();
            _audioFile = null;
        }
        _sampleChannel = null;

        // Reset position and duration
        CurrentPosition = TimeSpan.Zero;
        CurrentSongDuration = TimeSpan.Zero;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;

        // Restart timer
        _positionTimer.Start();
    }

    private void SetupAudio(string filePath)
    {
        ReleaseAudio(); // Release any previous resources

        try
        {
            _audioFile = new AudioFileReader(filePath);

            // Apply speed/pitch modifications using SoundTouch (if available and needed)
            var sourceStream = _audioFile.ToSampleProvider();
            ISampleProvider provider = sourceStream;

            // Add playback rate and pitch control
            var speedPitchProvider = new SampleToWaveProvider(provider);
            var soundTouchProvider = new SoundTouchProvider(speedPitchProvider.ToSampleProvider());

            // Apply initial speed and pitch
            soundTouchProvider.SetTempo((float)PlaybackRate);
            soundTouchProvider.PitchSemitones = (float)PitchSemitones;

            _sampleChannel = new SampleChannel(soundTouchProvider);
            _sampleChannel.Volume = _volume; // Apply current volume

            _waveOutDevice = new WaveOutEvent();
            _waveOutDevice.Init(_sampleChannel);
            _waveOutDevice.PlaybackStopped += WaveOutDevice_PlaybackStopped;

            CurrentSongDuration = _audioFile.TotalTime;

        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error setting up audio for {filePath}: {ex.Message}");
            ReleaseAudio(); // Ensure resources are released if setup fails
            // Consider notifying UI about the error
        }
    }

    private void ApplyPlaybackModifiers()
    {
        if (_sampleChannel?.SourceProvider is SoundTouchProvider soundTouchProvider)
        {
            // These setters trigger internal SoundTouch updates
            soundTouchProvider.SetTempo(PlaybackRate);
            soundTouchProvider.PitchSemitones = PitchSemitones;
            // Debug.WriteLine($"[PlaybackService] Applied speed={PlaybackRate:F2}, pitch={PitchSemitones:F1}");
        }
        // Note: Changing playback rate or pitch via SoundTouchProvider
        // while playing often causes pops/artifacts.
        // For smooth changes, a more complex audio graph might be needed.
        // For simplicity here, we rely on it mostly being set before playback or
        // accept potential minor artifacts during adjustment.
    }


    private void WaveOutDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Check if stopping was due to reaching end or error
            if (e.Exception == null && CurrentPlaybackStatus == PlaybackStateStatus.Playing && CurrentSong != null)
            {
                // Reached end of file, handle looping or stopping
                if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
                {
                    // Seek back to loop start and continue playing
                    // Note: Direct seek might not be frame-accurate or might cause pops.
                    // A more robust loop implementation might use an ISampleProvider
                    // that handles looping internally.
                    var loopStart = CurrentSong.SavedLoop.Start;
                    Seek(loopStart);
                    // Start playing again. Check if it was supposed to be playing.
                    if (_waveOutDevice?.PlaybackState == PlaybackState.Stopped)
                    {
                        _waveOutDevice.Play(); // Restart playback after seeking
                    }
                    CurrentPlaybackStatus = PlaybackStateStatus.Playing; // Status is still playing
                    // Debug.WriteLine($"[PlaybackService] Loop triggered. Seeking back to {loopStart}");
                }
                else
                {
                    // Not looping, stop
                    Stop();
                    // Debug.WriteLine("[PlaybackService] Playback ended.");
                }
            }
            else if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] Playback stopped due to error: {e.Exception.Message}");
                Stop(); // Ensure full stop state
                // Consider notifying UI about the error
            }
            else
            {
                // Was stopped manually or paused then stopped
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
            // Ensure timer stops when playback actually stops (not just pauses)
            if (_waveOutDevice?.PlaybackState == PlaybackState.Stopped)
            {
                // _positionTimer.Stop(); // Keep timer running to update 0 position
                CurrentPosition = TimeSpan.Zero; // Explicitly reset on stop
            }

            // Debug.WriteLine($"[PlaybackService] PlaybackStopped Event. New Status: {CurrentPlaybackStatus}");
        });
    }


    public void Play(Song song)
    {
        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine($"[PlaybackService] Cannot play invalid song or file not found: {song?.FilePath ?? "null"}");
            Stop();
            return;
        }

        // If playing the same song, just resume or seek to start?
        if (CurrentSong == song && CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            Resume(); // If paused, resume
            return; // If already playing same song, do nothing? Or seek to start? Let's seek to start for now.
        }

        Debug.WriteLine($"[PlaybackService] Playing: {song.Title}");
        CurrentSong = song; // Set CurrentSong before SetupAudio to ensure it's ready for duration/loop data setup

        // Set up audio processing graph
        SetupAudio(song.FilePath);

        if (_waveOutDevice != null)
        {
            _waveOutDevice.Play();
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            // _positionTimer.Start(); // Timer is already started in constructor
        }
        else
        {
            // Handle setup failure
            CurrentSong = null; // Clear song if playback couldn't start
        }
    }

    public void Pause()
    {
        if (_waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            Debug.WriteLine("[PlaybackService] Pausing playback.");
            _waveOutDevice.Pause();
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            // _positionTimer.Stop(); // Stop timer when paused
        }
    }

    public void Resume()
    {
        if (_waveOutDevice?.PlaybackState == PlaybackState.Paused)
        {
            Debug.WriteLine("[PlaybackService] Resuming playback.");
            _waveOutDevice.Play();
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            // _positionTimer.Start(); // Restart timer when resuming
        }
    }

    public void Stop()
    {
        Debug.WriteLine("[PlaybackService] Stopping playback.");
        ReleaseAudio(); // Releases resources and sets status to Stopped
        CurrentSong = null; // Explicitly clear the current song
    }

    public void Seek(TimeSpan position)
    {
        if (_audioFile != null && CurrentSongDuration.TotalSeconds > 0)
        {
            // Clamp position to duration
            position = TimeSpan.FromSeconds(Math.Max(0, Math.Min(CurrentSongDuration.TotalSeconds, position.TotalSeconds)));

            try
            {
                // Seeking while playback modifiers (like SoundTouch) are active can be tricky.
                // Simply setting CurrentTime might not work correctly or might cause artifacts.
                // NAudio's AudioFileReader.CurrentTime setter *can* work with providers
                // downstream, but its behavior with complex graphs might vary.
                // For SoundTouchProvider specifically, seeking the underlying source
                // like AudioFileReader is the typical approach.

                bool wasPlaying = _waveOutDevice?.PlaybackState == PlaybackState.Playing;

                // Stop briefly to seek smoothly (might still cause click/pop)
                // _waveOutDevice?.Stop(); // Or Pause() might be better depending on behavior

                _audioFile.CurrentTime = position;
                CurrentPosition = _audioFile.CurrentTime; // Update UI immediately

                // Resume if it was playing
                if (wasPlaying && _waveOutDevice?.PlaybackState != PlaybackState.Playing)
                {
                    _waveOutDevice?.Play();
                }
                // Else, if paused, keep paused but update position.
                // If stopped, seeking is possible but playback doesn't start.

                Debug.WriteLine($"[PlaybackService] Seeked to: {_audioFile.CurrentTime}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaybackService] Error seeking: {ex.Message}");
                // Consider notifying UI about the error
            }
        }
    }


    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual bool SetProperty<T>(ref T field, T newValue, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, newValue))
        {
            return false;
        }
        field = newValue;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        // Ensure PropertyChanged event is raised on the UI thread
        Dispatcher.UIThread.Post(() =>
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        });
    }

    // IDisposable implementation
    private bool disposedValue;

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // Dispose managed state (managed objects)
                _positionTimer.Stop();
                _positionTimer.Dispose();
                ReleaseAudio(); // Dispose NAudio objects
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            // Set large fields to null
            disposedValue = true;
        }
    }

    // Override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~PlaybackService()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
