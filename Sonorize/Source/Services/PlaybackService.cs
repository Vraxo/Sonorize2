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
                Debug.WriteLine($"[PlaybackService] CurrentSong set to: {value?.Title ?? "null"}");
                OnPropertyChanged(nameof(HasCurrentSong));
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
            if (SetProperty(ref _isPlaying, value))
            {
                Debug.WriteLine($"[PlaybackService] IsPlaying set to: {value}");
            }
        }
    }

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get => _currentPlaybackStatus;
        private set
        {
            if (SetProperty(ref _currentPlaybackStatus, value))
            {
                Debug.WriteLine($"[PlaybackService] CurrentPlaybackStatus set to: {value}");
            }
        }
    }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        set
        {
            if (SetProperty(ref _currentPosition, value))
            {
                // Debug.WriteLine($"[PlaybackService] CurrentPosition (TimeSpan) set to: {value}"); // Can be too noisy
                OnPropertyChanged(nameof(CurrentPositionSeconds));
            }
        }
    }
    public double CurrentPositionSeconds
    {
        get => CurrentPosition.TotalSeconds;
        set
        {
            if (audioFileReader != null && Math.Abs(CurrentPosition.TotalSeconds - value) > 0.1)
            {
                Debug.WriteLine($"[PlaybackService] CurrentPositionSeconds (double) set by UI/Seek to: {value}");
                Seek(TimeSpan.FromSeconds(value));
            }
        }
    }

    private TimeSpan _currentSongDuration;
    public TimeSpan CurrentSongDuration
    {
        get => _currentSongDuration;
        private set
        {
            if (SetProperty(ref _currentSongDuration, value))
            {
                Debug.WriteLine($"[PlaybackService] CurrentSongDuration set to: {value}");
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
            }
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1;

    private IWavePlayer? waveOutDevice;
    private AudioFileReader? audioFileReader;
    private ISampleProvider? finalSampleProvider; // Not directly used after pipeline construction, consider removing if truly unused.
    private SmbPitchShiftingSampleProvider? pitchShifter;
    private Timer? uiUpdateTimer;
    private SoundTouchWaveProvider? soundTouch;
    // private IWaveProvider? soundTouchWaveProvider; // This was unused, can be removed

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            value = Math.Clamp(value, 0.5f, 2.0f);
            if (SetProperty(ref _playbackRate, value) && soundTouch != null)
            {
                Debug.WriteLine($"[PlaybackService] PlaybackRate (Tempo) set to: {value}");
                soundTouch.Tempo = value;
            }
        }
    }

    private float _pitchSemitones = 0f;
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            if (SetProperty(ref _pitchSemitones, value))
            {
                Debug.WriteLine($"[PlaybackService] PitchSemitones set to: {value}");
                if (pitchShifter != null)
                {
                    pitchShifter.PitchFactor = (float)Math.Pow(2, value / 12.0);
                }
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
        // This check is crucial: IsPlaying must be true AND the device must report it's playing.
        if (IsPlaying && audioFileReader != null && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (audioFileReader == null || waveOutDevice == null) // Double check after dispatch
                {
                    Debug.WriteLineIf(audioFileReader == null, "[PlaybackService] UpdateUiCallback: audioFileReader became null after dispatch.");
                    Debug.WriteLineIf(waveOutDevice == null, "[PlaybackService] UpdateUiCallback: waveOutDevice became null after dispatch.");
                    return;
                }

                CurrentPosition = audioFileReader.CurrentTime;
                // Debug.WriteLine($"[PlaybackService] UpdateUiCallback: New Position = {CurrentPosition}"); // Can be too noisy

                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    if (CurrentPosition >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        Debug.WriteLine($"[PlaybackService] Loop detected: Current {CurrentPosition}, LoopEnd {activeLoop.End}. Seeking to LoopStart {activeLoop.Start}");
                        Seek(activeLoop.Start);
                        CurrentPosition = activeLoop.Start;
                    }
                }
            });
        }
        else if (IsPlaying) // If IsPlaying is true but device is not playing (e.g. stopped unexpectedly)
        {
            Debug.WriteLine($"[PlaybackService] UpdateUiCallback: IsPlaying is true, but waveOutDevice.PlaybackState is {waveOutDevice?.PlaybackState}. Forcing check.");
            // This might indicate an issue where PlaybackStopped didn't fire or state is inconsistent.
            // Consider forcing a state update or re-check.
        }
    }

    private void InitializeNAudioPipeline(string filePath)
    {
        Debug.WriteLine($"[PlaybackService] InitializeNAudioPipeline started for: {Path.GetFileName(filePath)}");
        CleanUpPlaybackResources();

        try
        {
            audioFileReader = new AudioFileReader(filePath);
            Debug.WriteLine($"[PlaybackService] AudioFileReader created. Format: {audioFileReader.WaveFormat}, Duration: {audioFileReader.TotalTime}");

            ISampleProvider sourceSampleProvider = audioFileReader.ToSampleProvider();
            ISampleProvider monoSampleProvider = sourceSampleProvider.ToMono();
            IWaveProvider monoWaveProviderForSoundTouch = new SampleToWaveProvider(monoSampleProvider);

            soundTouch = new SoundTouchWaveProvider(monoWaveProviderForSoundTouch)
            {
                Tempo = PlaybackRate, // Use current PlaybackRate
                Rate = 1.0f, // Rate for pitch, Tempo for speed. Keep Rate at 1 for SoundTouch to preserve pitch when changing tempo.
            };
            Debug.WriteLine($"[PlaybackService] SoundTouchWaveProvider created. Output Format: {soundTouch.WaveFormat}");

            ISampleProvider soundTouchAsSampleProvider = soundTouch.ToSampleProvider();
            pitchShifter = new SmbPitchShiftingSampleProvider(soundTouchAsSampleProvider)
            {
                PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0) // Use current PitchSemitones
            };
            Debug.WriteLine($"[PlaybackService] PitchShifter created. Output Format: {pitchShifter.WaveFormat}");

            IWaveProvider finalWaveProviderForDevice = pitchShifter.ToWaveProvider();
            Debug.WriteLine($"[PlaybackService] FinalWaveProviderForDevice created. Format: {finalWaveProviderForDevice.WaveFormat}");

            waveOutDevice = new WaveOutEvent();
            waveOutDevice.PlaybackStopped += OnPlaybackStopped; // Hook event BEFORE Init
            waveOutDevice.Init(finalWaveProviderForDevice);
            Debug.WriteLine($"[PlaybackService] WaveOutDevice initialized.");

            CurrentSongDuration = audioFileReader.TotalTime;
            CurrentPosition = TimeSpan.Zero; // Reset position for new song
            Debug.WriteLine($"[PlaybackService] NAudio pipeline initialization COMPLETE for: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during NAudio pipeline init for {Path.GetFileName(filePath)}: {ex.ToString()}");
            CleanUpPlaybackResources(); // Clean up again in case of partial initialization
        }
    }

    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService] Play requested for: {song?.Title ?? "null song"}");
        if (song == null || string.IsNullOrEmpty(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] Play rejected: Song or FilePath is null/empty.");
            return;
        }

        CurrentSong = song; // Set current song before initialization

        InitializeNAudioPipeline(song.FilePath);

        if (waveOutDevice != null && audioFileReader != null)
        {
            Debug.WriteLine($"[PlaybackService] Attempting to play: {CurrentSong.Title}. Device State: {waveOutDevice.PlaybackState}");
            waveOutDevice.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Play started. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}, Device State after Play(): {waveOutDevice.PlaybackState}");
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Playback not started due to initialization failure for {song.Title}. Forcing stopped state.");
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            StopUiUpdateTimer();
        }
    }

    private void StartUiUpdateTimer()
    {
        Debug.WriteLine("[PlaybackService] StartUiUpdateTimer called.");
        uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }
    private void StopUiUpdateTimer()
    {
        Debug.WriteLine("[PlaybackService] StopUiUpdateTimer called.");
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void CleanUpPlaybackResources()
    {
        Debug.WriteLine("[PlaybackService] CleanUpPlaybackResources called.");
        StopUiUpdateTimer();

        if (waveOutDevice != null)
        {
            Debug.WriteLine($"[PlaybackService] Stopping and disposing waveOutDevice. Current state: {waveOutDevice.PlaybackState}");
            waveOutDevice.Stop(); // This might trigger PlaybackStopped
            waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
            waveOutDevice.Dispose();
            waveOutDevice = null;
            Debug.WriteLine("[PlaybackService] waveOutDevice disposed.");
        }

        // No explicit dispose needed for pitchShifter (ISampleProvider) as it doesn't own unmanaged resources typically.
        pitchShifter = null;
        // No explicit dispose needed for soundTouch (SoundTouchWaveProvider) if it's correctly managed by NAudio's disposal chain
        // starting from AudioFileReader or WaveOutEvent. However, its source (monoWaveProviderForSoundTouch) is created by us.
        // AudioFileReader is the primary resource.
        soundTouch = null;

        if (audioFileReader != null)
        {
            Debug.WriteLine("[PlaybackService] Disposing audioFileReader.");
            audioFileReader.Dispose();
            audioFileReader = null;
            Debug.WriteLine("[PlaybackService] audioFileReader disposed.");
        }
        Debug.WriteLine("[PlaybackService] CleanUpPlaybackResources finished.");
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Debug.WriteLine($"[PlaybackService] OnPlaybackStopped event fired. Current IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}. Exception: {e.Exception?.Message ?? "None"}");

        // This event can fire for multiple reasons: natural end, Stop() called, or error.
        // We should ensure state is correctly set regardless of how it was triggered.
        // The Dispatcher is important because this event often comes from a background NAudio thread.
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            bool wasPlayingOrPaused = IsPlaying || CurrentPlaybackStatus == PlaybackStateStatus.Paused;

            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            StopUiUpdateTimer(); // Ensure timer is stopped

            if (audioFileReader != null && audioFileReader.CurrentTime >= audioFileReader.TotalTime && e.Exception == null)
            {
                Debug.WriteLine("[PlaybackService] Song finished naturally.");
                CurrentPosition = CurrentSongDuration;
            }
            else if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] NAudio Playback Error reported in OnPlaybackStopped: {e.Exception.Message}");
                // Optionally, update UI with error message or reset song position
                CurrentPosition = TimeSpan.Zero; // Reset position on error
            }
            else if (wasPlayingOrPaused) // Stopped by user or other non-error reason before end
            {
                Debug.WriteLine("[PlaybackService] Playback stopped by means other than natural end or error (e.g. user stop, new song).");
                // CurrentPosition should already be where it was, or will be set to zero by Stop() if called.
            }
            // If CleanUpPlaybackResources was called by Stop(), audioFileReader might be null here.
            // Only update CurrentPosition if it's a natural end.
        });
    }

    public void Pause()
    {
        Debug.WriteLine($"[PlaybackService] Pause requested. IsPlaying: {IsPlaying}, Device State: {waveOutDevice?.PlaybackState}");
        if (IsPlaying && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            waveOutDevice.Pause();
            IsPlaying = false; // State change
            CurrentPlaybackStatus = PlaybackStateStatus.Paused; // State change
            StopUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Paused. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}, Device State: {waveOutDevice.PlaybackState}");
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Pause ignored. IsPlaying: {IsPlaying}, Device State: {waveOutDevice?.PlaybackState}");
        }
    }

    public void Resume()
    {
        Debug.WriteLine($"[PlaybackService] Resume requested. CurrentSong: {CurrentSong?.Title}, IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}, Device State: {waveOutDevice?.PlaybackState}");
        if (!IsPlaying && CurrentSong != null)
        {
            if (waveOutDevice == null || audioFileReader == null || waveOutDevice.PlaybackState == PlaybackState.Stopped)
            {
                Debug.WriteLine("[PlaybackService] Resume: Pipeline not initialized or was stopped. Re-initializing.");
                TimeSpan resumePosition = CurrentPosition;
                InitializeNAudioPipeline(CurrentSong.FilePath);
                if (audioFileReader != null && waveOutDevice != null)
                {
                    Debug.WriteLine($"[PlaybackService] Resume: Pipeline re-initialized. Seeking to {resumePosition}");
                    Seek(resumePosition);
                }
                else
                {
                    Debug.WriteLine($"[PlaybackService] Resume failed: Could not re-initialize pipeline for {CurrentSong.Title}. Forcing stopped state.");
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    StopUiUpdateTimer();
                    return;
                }
            }

            if (waveOutDevice != null && waveOutDevice.PlaybackState != PlaybackState.Playing)
            {
                Debug.WriteLine($"[PlaybackService] Resume: Attempting to play. Device State: {waveOutDevice.PlaybackState}");
                waveOutDevice.Play();
                IsPlaying = true; // State change
                CurrentPlaybackStatus = PlaybackStateStatus.Playing; // State change
                StartUiUpdateTimer();
                Debug.WriteLine($"[PlaybackService] Resumed. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}, Device State after Play(): {waveOutDevice.PlaybackState}");
            }
            else
            {
                Debug.WriteLine($"[PlaybackService] Resume ignored or already playing. Device State: {waveOutDevice?.PlaybackState}");
            }
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Resume ignored. IsPlaying: {IsPlaying}, CurrentSong: {CurrentSong?.Title}");
        }
    }

    public void Stop()
    {
        Debug.WriteLine($"[PlaybackService] Stop requested. Current IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}");
        // Set state immediately to prevent race conditions with PlaybackStopped event
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        // CleanUpPlaybackResources will call waveOutDevice.Stop() and StopUiUpdateTimer()
        CleanUpPlaybackResources();
        CurrentPosition = TimeSpan.Zero; // Reset position after stopping
        Debug.WriteLine($"[PlaybackService] Stopped. IsPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}");
        // CurrentSong remains, so user can see what was last played.
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (audioFileReader != null)
        {
            var targetPosition = positionInTrueTime;
            if (targetPosition < TimeSpan.Zero) targetPosition = TimeSpan.Zero;
            if (targetPosition > audioFileReader.TotalTime) targetPosition = audioFileReader.TotalTime;

            Debug.WriteLine($"[PlaybackService] Seek requested to: {targetPosition}. Current AFR Time: {audioFileReader.CurrentTime}");
            audioFileReader.CurrentTime = targetPosition;
            CurrentPosition = audioFileReader.CurrentTime; // Update our tracking property immediately
            Debug.WriteLine($"[PlaybackService] Seek completed. New Position: {CurrentPosition}, New AFR Time: {audioFileReader.CurrentTime}");
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Seek ignored: audioFileReader is null.");
        }
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose called.");
        CleanUpPlaybackResources();
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackService] Dispose finished.");
    }
}