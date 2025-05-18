using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
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
                OnPropertyChanged(nameof(HasCurrentSong)); // Ensure HasCurrentSong updates
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
            if (SetProperty(ref _currentPosition, value))
            {
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
        if (IsPlaying && audioFileReader != null && _waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (audioFileReader == null || _waveOutDevice == null || CurrentSong == null) return;

                CurrentPosition = audioFileReader.CurrentTime;

                if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
                {
                    var loop = CurrentSong.SavedLoop;
                    if (loop.End > loop.Start && CurrentPosition >= loop.End)
                    {
                        Debug.WriteLine($"[PlaybackService] Loop active & end reached ({CurrentPosition} >= {loop.End}). Seeking to loop start: {loop.Start}");
                        Seek(loop.Start);
                    }
                }
            });
        }
    }

    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService] Play requested for: {(song?.Title ?? "null song")}");
        // Stop existing playback but don't reset CurrentSong/Duration/Position yet,
        // as this method will handle it for the new song or clear it if the new song is invalid.
        StopPlaybackInternal(resetCurrentSongAndRelatedState: false);

        if (song == null || string.IsNullOrEmpty(song.FilePath))
        {
            // New song is invalid, so ensure state reflects no song loaded.
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            // IsPlaying and CurrentPlaybackStatus are already set to Stopped by StopPlaybackInternal.
            Debug.WriteLine("[PlaybackService] Play called with null/invalid song. State is stopped, CurrentSong nulled.");
            return;
        }

        // Set the new song. This fires PropertyChanged, and the VM will react.
        CurrentSong = song;
        // InitializeNAudioPipeline will update CurrentSongDuration and reset CurrentPosition.
        bool pipelineInitialized = InitializeNAudioPipeline(song.FilePath);

        if (pipelineInitialized && _waveOutDevice != null && audioFileReader != null)
        {
            if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
            {
                Debug.WriteLine($"[PlaybackService] Song has active loop. Seeking to loop start: {CurrentSong.SavedLoop.Start} before playing.");
                Seek(CurrentSong.SavedLoop.Start);
            }

            _waveOutDevice.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}");
        }
        else // Pipeline initialization failed for the new song.
        {
            Debug.WriteLine($"[PlaybackService] Pipeline init failed for {Path.GetFileName(song.FilePath)}. Cleaning up and stopping.");
            // CleanUpNAudioResources is called by InitializeNAudioPipeline on failure.
            // CurrentSongDuration and CurrentPosition are reset by InitializeNAudioPipeline on failure.
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            StopUiUpdateTimer();
            // Critical: Ensure CurrentSong is nulled out to reflect that nothing is playable.
            CurrentSong = null;
        }
    }


    private bool InitializeNAudioPipeline(string filePath)
    {
        Debug.WriteLine($"[PlaybackService] InitializeNAudioPipeline for: {Path.GetFileName(filePath)}");
        try
        {
            audioFileReader = new AudioFileReader(filePath);
            ISampleProvider sourceSampleProvider = audioFileReader.ToSampleProvider();
            ISampleProvider monoSampleProvider = sourceSampleProvider.ToMono(); // Convert to mono for SoundTouch/SMB
            IWaveProvider monoWaveProviderForSoundTouch = new SampleToWaveProvider(monoSampleProvider);

            soundTouch = new SoundTouchWaveProvider(monoWaveProviderForSoundTouch)
            {
                Tempo = PlaybackRate, // Apply current rate
                Rate = 1.0f,          // Pitch is handled by SmbPitchShiftingSampleProvider
            };

            ISampleProvider soundTouchAsSampleProvider = soundTouch.ToSampleProvider();

            pitchShifter = new SmbPitchShiftingSampleProvider(soundTouchAsSampleProvider)
            {
                PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0) // Apply current pitch
            };

            IWaveProvider finalWaveProviderForDevice = pitchShifter.ToWaveProvider();

            _waveOutDevice = new WaveOutEvent();
            _waveOutDeviceInstanceForStopEventCheck = _waveOutDevice; // Track this instance for PlaybackStopped event
            _waveOutDevice.PlaybackStopped += OnPlaybackStopped;
            _waveOutDevice.Init(finalWaveProviderForDevice);

            CurrentSongDuration = audioFileReader.TotalTime;
            CurrentPosition = TimeSpan.Zero; // Explicitly reset position for new song / successful init
            Debug.WriteLine($"[PlaybackService] NAudio pipeline initialization COMPLETE for: {Path.GetFileName(filePath)}. Duration: {CurrentSongDuration}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during NAudio pipeline init for {Path.GetFileName(filePath)}: {ex.ToString()}");
            CleanUpNAudioResources(); // Ensure cleanup on failure
            CurrentSongDuration = TimeSpan.Zero; // Reset duration on failure
            CurrentPosition = TimeSpan.Zero;     // Reset position on failure
            return false;
        }
    }

    private void StartUiUpdateTimer()
    {
        uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        Debug.WriteLine("[PlaybackService] UI Update Timer Started.");
    }

    private void StopUiUpdateTimer()
    {
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        Debug.WriteLine("[PlaybackService] UI Update Timer Stopped.");
    }

    private void CleanUpNAudioResources()
    {
        // Dispose of the WaveOutDevice
        if (_waveOutDevice != null)
        {
            // Only unsubscribe if this is the instance we subscribed to.
            // This helps prevent issues if events are racing during cleanup.
            if (_waveOutDeviceInstanceForStopEventCheck == _waveOutDevice)
            {
                _waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
            }
            _waveOutDevice.Stop();    // Stop playback
            _waveOutDevice.Dispose(); // Dispose of the device
            _waveOutDevice = null;    // Nullify the reference
        }
        _waveOutDeviceInstanceForStopEventCheck = null; // Always nullify this after dealing with _waveOutDevice

        // Dispose of the AudioFileReader
        audioFileReader?.Dispose();
        audioFileReader = null;

        // SmbPitchShiftingSampleProvider is not IDisposable itself, just nullify
        pitchShifter = null;

        // SoundTouchWaveProvider is IDisposable
        soundTouch = null;

        Debug.WriteLine("[PlaybackService] NAudio resources cleaned up.");
    }


    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // Ensure this event is for the currently active WaveOutDevice instance
        if (sender != _waveOutDeviceInstanceForStopEventCheck)
        {
            Debug.WriteLine("[PlaybackService] OnPlaybackStopped received for a stale WaveOutDevice instance. Ignoring.");
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Debug.WriteLine($"[PlaybackService] OnPlaybackStopped: Exception: {e.Exception?.Message ?? "None"}");
            if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] Playback stopped due to error: {e.Exception.Message}");
                // Optionally, notify user or log more detailed error
            }

            // Common actions for any stop (natural end or error)
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            StopUiUpdateTimer(); // Stop UI updates

            // If playback stopped naturally (reached the end of the file)
            // and no loop is active or the loop condition wasn't met to restart it.
            // CurrentPosition might not be exactly TotalTime due to timing, check if close.
            bool naturalEndOfSong = CurrentSong != null && audioFileReader != null &&
                                    audioFileReader.CurrentTime >= audioFileReader.TotalTime - TimeSpan.FromMilliseconds(500);

            if (naturalEndOfSong && (CurrentSong?.IsLoopActive == false || CurrentSong?.SavedLoop == null))
            {
                Debug.WriteLine($"[PlaybackService] Natural end of song: {CurrentSong?.Title}. Resetting position.");
                CurrentPosition = TimeSpan.Zero; // Reset position for next play
                                                 // Do not null out CurrentSong here, allow re-playing the same song.
            }
            // If an error occurred, or if it was an explicit stop, resources might be cleaned by the caller.
            // If it was an error during playback, we might want to clean up here.
            // For now, Play, Stop, Pause, Seek handle their own resource cleanup or state changes.
            // This handler primarily sets IsPlaying and CurrentPlaybackStatus.
            // If an error stopped playback, CleanUpNAudioResources should ideally be called.
            // However, if Play() is called next, it will clean them up anyway.
        });
    }

    public void Pause()
    {
        if (IsPlaying && _waveOutDevice != null && _waveOutDevice.PlaybackState == PlaybackState.Playing)
        {
            Debug.WriteLine("[PlaybackService] Pause requested.");
            _waveOutDevice.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer(); // Stop updates while paused
        }
    }

    public void Resume()
    {
        if (CurrentSong == null)
        {
            Debug.WriteLine("[PlaybackService] Resume requested but no CurrentSong. Doing nothing.");
            return;
        }

        if (_waveOutDevice != null && _waveOutDevice.PlaybackState == PlaybackState.Paused && audioFileReader != null)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Paused state.");
            _waveOutDevice.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
        // If stopped (e.g., song finished or explicitly stopped) and user hits "Play" (which calls Resume)
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Stopped state. Re-playing current song.");
            // Re-play the current song. Play() handles stopping previous, init, etc.
            // Ensure that if IsPlaying is false, the UI shows "Play" which then calls Resume,
            // and Resume correctly re-plays the song from its current state (usually start if stopped).
            Play(CurrentSong);
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Resume requested but conditions not met. State: {_waveOutDevice?.PlaybackState}, AFR: {audioFileReader != null}, Status: {CurrentPlaybackStatus}");
        }
    }

    // Stops playback and optionally resets the current song and its related state.
    // Called by public Stop() with true, and by Play() with false.
    private void StopPlaybackInternal(bool resetCurrentSongAndRelatedState = true)
    {
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        StopUiUpdateTimer();
        CleanUpNAudioResources(); // This is the most critical part for stopping audio.

        if (resetCurrentSongAndRelatedState)
        {
            CurrentSong = null; // Null out the song, affects HasCurrentSong
            CurrentSongDuration = TimeSpan.Zero; // Reset duration
            CurrentPosition = TimeSpan.Zero;     // Reset position
            Debug.WriteLine("[PlaybackService] StopPlaybackInternal: CurrentSong, Duration, Position reset.");
        }
        else
        {
            // If not resetting, it means Play() will immediately set CurrentSong,
            // and InitializeNAudioPipeline will set CurrentSongDuration and CurrentPosition.
            // IsPlaying and CurrentPlaybackStatus are already set to Stopped.
            Debug.WriteLine("[PlaybackService] StopPlaybackInternal: CurrentSong and related state NOT reset by this call (new song incoming). Resources cleaned.");
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
            Debug.WriteLine($"[PlaybackService] Seek ignored: AFR null? {audioFileReader == null}, Device null? {_waveOutDevice == null}, Song null? {CurrentSong == null}");
            return;
        }

        TimeSpan targetPosition = requestedPosition;

        if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
        {
            var loop = CurrentSong.SavedLoop;
            if (loop.End > loop.Start)
            {
                if (targetPosition < loop.Start)
                {
                    Debug.WriteLine($"[PlaybackService] Seek: Loop active, target {targetPosition} < loop start {loop.Start}. Snapping to loop start.");
                    targetPosition = loop.Start;
                }
                else if (targetPosition > loop.End)
                {
                    Debug.WriteLine($"[PlaybackService] Seek: Loop active, target {targetPosition} > loop end {loop.End}. Snapping to loop start (as per spec: seeking outside loop end goes to loop start).");
                    targetPosition = loop.Start;
                }
            }
        }

        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, audioFileReader.TotalTime.TotalSeconds - TimeSpan.FromMilliseconds(100).TotalSeconds)); // Prevent seeking too close to the very end

        Debug.WriteLine($"[PlaybackService] Seeking to: {targetPosition}. Current AFR Time: {audioFileReader.CurrentTime}");
        audioFileReader.CurrentTime = targetPosition;
        CurrentPosition = audioFileReader.CurrentTime; // Update our tracking property immediately
        Debug.WriteLine($"[PlaybackService] Seek completed. New Position: {CurrentPosition}, New AFR Time: {audioFileReader.CurrentTime}");

        // If paused and seek happens, playback should remain paused at new position.
        // If playing, it continues from new position.
        // If stopped, seeking changes position, next Play/Resume will use it (or reset if Play(new song)).
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");
        StopPlaybackInternal(true); // Ensure everything is stopped and cleaned.
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
        GC.SuppressFinalize(this);
    }
}