// Path: Source/Services/PlaybackService.cs
using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SoundTouch.Net;
using SoundTouch.Net.NAudioSupport;

namespace Sonorize.Services;

public enum PlaybackStateStatus { Stopped, Playing, Paused }

public class PlaybackService : ViewModelBase, IDisposable
{
    private Song? _currentSong;
    public Song? CurrentSong
    {
        get => _currentSong;
        private set
        {
            if (SetProperty(ref _currentSong, value))
            {
                Debug.WriteLine($"[PlaybackService] CurrentSong property set to: {value?.Title ?? "null"}");
                OnPropertyChanged(nameof(HasCurrentSong));
            }
        }
    }

    public bool HasCurrentSong => CurrentSong != null;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get => _currentPlaybackStatus;
        private set => SetProperty(ref _currentPlaybackStatus, value);
    }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        set
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                if (SetProperty(ref _currentPosition, value))
                {
                    OnPropertyChanged(nameof(CurrentPositionSeconds));
                }
            }
            else
            {
                Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (SetProperty(ref _currentPosition, value))
                    {
                        OnPropertyChanged(nameof(CurrentPositionSeconds));
                    }
                });
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
            if (SetProperty(ref _currentSongDuration, value))
            {
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
            }
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1.0;

    private IWavePlayer? _waveOutDevice;
    private AudioFileReader? audioFileReader;
    private SmbPitchShiftingSampleProvider? pitchShifter;
    private Timer? uiUpdateTimer;
    private SoundTouchWaveProvider? soundTouch;
    private IWavePlayer? _waveOutDeviceInstanceForStopEventCheck;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (Math.Abs(_playbackRate - value) > float.Epsilon)
            {
                _playbackRate = value;
                if (soundTouch != null) soundTouch.Tempo = _playbackRate;
                OnPropertyChanged();
            }
        }
    }

    private float _pitchSemitones = 0f;
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            if (Math.Abs(_pitchSemitones - value) > float.Epsilon)
            {
                _pitchSemitones = value;
                if (pitchShifter != null) pitchShifter.PitchFactor = (float)Math.Pow(2, _pitchSemitones / 12.0);
                OnPropertyChanged();
            }
        }
    }

    public PlaybackService()
    {
        Debug.WriteLine("[PlaybackService] Constructor called.");
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateUiCallback(object? state)
    {
        if (CurrentPlaybackStatus == PlaybackStateStatus.Playing &&
            _waveOutDevice?.PlaybackState == PlaybackState.Playing &&
            audioFileReader != null &&
            CurrentSong != null)
        {
            // CurrentPosition setter will handle UI thread dispatch if necessary
            CurrentPosition = audioFileReader.CurrentTime;

            // Handle looping on the UI thread where CurrentPosition (and thus its consumers) are typically managed
            Dispatcher.UIThread.InvokeAsync(() => {
                if (CurrentSong != null && CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null) // Re-check CurrentSong for safety
                {
                    var loop = CurrentSong.SavedLoop;
                    // Use the property CurrentPosition which is already on UI thread or dispatched
                    if (loop.End > loop.Start && this.CurrentPosition >= loop.End)
                    {
                        Debug.WriteLine($"[PlaybackService] Loop active & end reached ({this.CurrentPosition} >= {loop.End}). Seeking to loop start: {loop.Start}");
                        Seek(loop.Start);
                    }
                }
            });
        }
    }

    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService] Play requested for: {(song?.Title ?? "null song")}");
        StopPlaybackInternal(resetCurrentSongAndRelatedState: false);

        if (song == null || string.IsNullOrEmpty(song.FilePath))
        {
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            Debug.WriteLine("[PlaybackService] Play called with null/invalid song. State is stopped, CurrentSong nulled.");
            return;
        }

        CurrentSong = song;
        bool pipelineInitialized = InitializeNAudioPipeline(song.FilePath);

        if (pipelineInitialized && _waveOutDevice != null && audioFileReader != null)
        {
            if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
            {
                Debug.WriteLine($"[PlaybackService] Song has active loop. Seeking to loop start: {CurrentSong.SavedLoop.Start} before playing.");
                Seek(CurrentSong.SavedLoop.Start);
            }
            else
            {
                if (audioFileReader.CurrentTime != TimeSpan.Zero) audioFileReader.CurrentTime = TimeSpan.Zero;
                CurrentPosition = TimeSpan.Zero;
            }

            _waveOutDevice.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}");
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Pipeline init failed for {Path.GetFileName(song.FilePath)}. Cleaning up and stopping.");
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            StopUiUpdateTimer();
        }
    }


    private bool InitializeNAudioPipeline(string filePath)
    {
        Debug.WriteLine($"[PlaybackService] InitializeNAudioPipeline for: {Path.GetFileName(filePath)}");
        CleanUpNAudioResources();
        try
        {
            audioFileReader = new AudioFileReader(filePath);

            ISampleProvider sourceForSoundTouch = audioFileReader.ToSampleProvider().ToMono();
            IWaveProvider waveSourceForSoundTouch = new SampleToWaveProvider(sourceForSoundTouch);

            soundTouch = new SoundTouchWaveProvider(waveSourceForSoundTouch)
            {
                Tempo = PlaybackRate,
                Rate = 1.0f,
            };

            pitchShifter = new SmbPitchShiftingSampleProvider(soundTouch.ToSampleProvider())
            {
                PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0)
            };

            IWaveProvider finalWaveProviderForDevice = pitchShifter.ToWaveProvider();

            _waveOutDevice = new WaveOutEvent();
            _waveOutDeviceInstanceForStopEventCheck = _waveOutDevice;
            _waveOutDevice.PlaybackStopped += OnPlaybackStopped;
            _waveOutDevice.Init(finalWaveProviderForDevice);

            CurrentSongDuration = audioFileReader.TotalTime;
            CurrentPosition = TimeSpan.Zero;
            Debug.WriteLine($"[PlaybackService] NAudio pipeline initialization COMPLETE for: {Path.GetFileName(filePath)}. Duration: {CurrentSongDuration}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during NAudio pipeline init for {Path.GetFileName(filePath)}: {ex.ToString()}");
            CleanUpNAudioResources();
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            return false;
        }
    }

    private void StartUiUpdateTimer()
    {
        uiUpdateTimer?.Change(TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(100)); // Slightly longer initial delay
        Debug.WriteLine("[PlaybackService] UI Update Timer Started.");
    }

    private void StopUiUpdateTimer()
    {
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Debug.WriteLine("[PlaybackService] UI Update Timer Stopped.");
    }

    private void CleanUpNAudioResources()
    {
        if (_waveOutDevice != null)
        {
            if (_waveOutDeviceInstanceForStopEventCheck == _waveOutDevice)
            {
                _waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
            }
            _waveOutDevice.Stop();
            _waveOutDevice.Dispose();
            _waveOutDevice = null;
        }
        _waveOutDeviceInstanceForStopEventCheck = null;

        audioFileReader?.Dispose();
        audioFileReader = null;

        pitchShifter = null;
        soundTouch = null;

        Debug.WriteLine("[PlaybackService] NAudio resources cleaned up.");
    }


    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (sender != _waveOutDeviceInstanceForStopEventCheck)
        {
            Debug.WriteLine("[PlaybackService] OnPlaybackStopped received for a stale WaveOutDevice instance. Ignoring.");
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Debug.WriteLine($"[PlaybackService] OnPlaybackStopped: Exception: {e.Exception?.Message ?? "None"}. Current Status before handling: {CurrentPlaybackStatus}");

            // Check if stop was already handled by StopPlaybackInternal or Seek.
            // If IsPlaying is false AND CurrentPlaybackStatus is Stopped, likely handled.
            bool alreadyHandled = !IsPlaying && CurrentPlaybackStatus == PlaybackStateStatus.Stopped;

            if (!alreadyHandled)
            {
                IsPlaying = false; // Update state
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                // UI Timer stops implicitly because IsPlaying is false

                if (e.Exception != null)
                {
                    Debug.WriteLine($"[PlaybackService] Playback stopped due to error: {e.Exception.Message}");
                }
                else
                {
                    bool naturalEndOfSong = CurrentSong != null && audioFileReader != null &&
                                            (audioFileReader.CurrentTime >= audioFileReader.TotalTime - TimeSpan.FromMilliseconds(500));

                    if (naturalEndOfSong && (CurrentSong?.IsLoopActive == false || CurrentSong?.SavedLoop == null))
                    {
                        Debug.WriteLine($"[PlaybackService] Natural end of song: {CurrentSong?.Title}. Resetting position.");
                        CurrentPosition = TimeSpan.Zero;
                    }
                }
                Debug.WriteLine($"[PlaybackService] OnPlaybackStopped: Processed event. Status is now {CurrentPlaybackStatus}. IsPlaying: {IsPlaying}");
            }
            else
            {
                Debug.WriteLine($"[PlaybackService] OnPlaybackStopped: Event considered already handled. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}");
            }
        });
    }

    public void Pause()
    {
        if (CurrentPlaybackStatus == PlaybackStateStatus.Playing && _waveOutDevice != null && _waveOutDevice.PlaybackState == PlaybackState.Playing)
        {
            Debug.WriteLine("[PlaybackService] Pause requested.");
            _waveOutDevice.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
        }
    }

    public void Resume()
    {
        if (CurrentSong == null)
        {
            Debug.WriteLine("[PlaybackService] Resume requested but no CurrentSong. Doing nothing.");
            return;
        }

        if (CurrentPlaybackStatus == PlaybackStateStatus.Paused && _waveOutDevice != null && audioFileReader != null)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Paused state.");
            _waveOutDevice.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Stopped state. Re-playing current song.");
            Play(CurrentSong);
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Resume requested but conditions not met. PlaybackState: {_waveOutDevice?.PlaybackState}, Status: {CurrentPlaybackStatus}");
        }
    }

    private void StopPlaybackInternal(bool resetCurrentSongAndRelatedState = true)
    {
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;

        CleanUpNAudioResources();

        if (resetCurrentSongAndRelatedState)
        {
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            Debug.WriteLine("[PlaybackService] StopPlaybackInternal: CurrentSong, Duration, Position reset.");
        }
        else
        {
            Debug.WriteLine("[PlaybackService] StopPlaybackInternal: State set to Stopped. Resources cleaned. Song context NOT reset by this call.");
        }
    }

    public void Stop()
    {
        Debug.WriteLine("[PlaybackService] Public Stop() called.");
        StopPlaybackInternal(resetCurrentSongAndRelatedState: true);
    }

    public void Seek(TimeSpan requestedPosition)
    {
        if (audioFileReader == null || _waveOutDevice == null || CurrentSong == null)
        {
            Debug.WriteLine($"[PlaybackService] Seek ignored: Pre-conditions not met. AFR null? {audioFileReader == null}, Device null? {_waveOutDevice == null}, Song null? {CurrentSong == null}");
            return;
        }

        TimeSpan targetPosition = requestedPosition;
        if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
        {
            var loop = CurrentSong.SavedLoop;
            if (loop.End > loop.Start)
            {
                if (targetPosition < loop.Start || targetPosition > loop.End)
                {
                    targetPosition = loop.Start;
                }
            }
        }
        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, audioFileReader.TotalTime.TotalSeconds - TimeSpan.FromMilliseconds(100).TotalSeconds));
        Debug.WriteLine($"[PlaybackService] Seek: Requested {requestedPosition}, Clamped/Looped Target: {targetPosition}");

        PlaybackStateStatus originalStatus = CurrentPlaybackStatus;
        bool wasPlaying = (originalStatus == PlaybackStateStatus.Playing);

        if (wasPlaying)
        {
            _waveOutDevice.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused; // Temporary state
            Debug.WriteLine("[PlaybackService] Seek: Device Paused.");
        }

        // Set reader position and IMMEDIATELY update our CurrentPosition property
        audioFileReader.CurrentTime = targetPosition;
        CurrentPosition = audioFileReader.CurrentTime; // Update VM/UI immediately
        Debug.WriteLine($"[PlaybackService] Seek: audioFileReader.CurrentTime set to {audioFileReader.CurrentTime}. ViewModel CurrentPosition updated to {this.CurrentPosition}.");

        // Rebuild pipeline segment and re-initialize WaveOutDevice
        var currentDevice = _waveOutDevice; // Keep ref to current device
        currentDevice.PlaybackStopped -= OnPlaybackStopped;
        currentDevice.Stop(); // Must stop before Init
        Debug.WriteLine("[PlaybackService] Seek: Device Stopped for re-initialization.");

        try
        {
            ISampleProvider sourceForSoundTouch = audioFileReader.ToSampleProvider().ToMono();
            IWaveProvider waveSourceForSoundTouch = new SampleToWaveProvider(sourceForSoundTouch);
            soundTouch = new SoundTouchWaveProvider(waveSourceForSoundTouch)
            {
                Tempo = PlaybackRate,
                Rate = 1.0f,
            };
            pitchShifter = new SmbPitchShiftingSampleProvider(soundTouch.ToSampleProvider())
            {
                PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0)
            };
            currentDevice.Init(pitchShifter.ToWaveProvider());
            Debug.WriteLine($"[PlaybackService] Seek: Device Re-Initialized with new pipeline at {this.CurrentPosition}.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during seek pipeline rebuild/init: {ex.ToString()}");
            if (currentDevice != null) currentDevice.PlaybackStopped += OnPlaybackStopped; // Re-subscribe before full stop
            StopPlaybackInternal(true);
            return;
        }
        currentDevice.PlaybackStopped += OnPlaybackStopped; // Re-subscribe after successful Init

        if (wasPlaying)
        {
            currentDevice.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            Debug.WriteLine("[PlaybackService] Seek: Playback Resumed from new position.");

            // Force one more update of CurrentPosition after Play to ensure UI consistency
            // Dispatch this to ensure it happens after Play() has had a chance to affect reader's time
            Dispatcher.UIThread.InvokeAsync(() => {
                if (audioFileReader != null)
                {
                    this.CurrentPosition = audioFileReader.CurrentTime;
                    Debug.WriteLine($"[PlaybackService] Seek: Forced CurrentPosition update post-play to {this.CurrentPosition}");
                }
            }, DispatcherPriority.Background); // Lower priority to let audio engine catch up
        }
        else // Was Paused or originally Stopped
        {
            IsPlaying = false; // Ensure IsPlaying is false
            CurrentPlaybackStatus = originalStatus; // Maintain original Paused/Stopped state
            Debug.WriteLine($"[PlaybackService] Seek: Playback remains {originalStatus} at new position {this.CurrentPosition}.");
        }
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");
        StopPlaybackInternal(true);
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
        GC.SuppressFinalize(this);
    }
}