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
            SetProperty(ref _currentSong, value);
            OnPropertyChanged(nameof(HasCurrentSong));
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
                OnPropertyChanged(nameof(CurrentPositionSeconds));
        }
    }
    public double CurrentPositionSeconds
    {
        get => CurrentPosition.TotalSeconds;
        set
        {
            if (audioFileReader != null && Math.Abs(CurrentPosition.TotalSeconds - value) > 0.1)
                Seek(TimeSpan.FromSeconds(value));
        }
    }

    private TimeSpan _currentSongDuration;
    public TimeSpan CurrentSongDuration
    {
        get => _currentSongDuration;
        private set
        {
            if (SetProperty(ref _currentSongDuration, value))
                OnPropertyChanged(nameof(CurrentSongDurationSeconds));
        }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1;

    private IWavePlayer? waveOutDevice;
    private AudioFileReader? audioFileReader;
    private ISampleProvider? finalSampleProvider;
    private SmbPitchShiftingSampleProvider? pitchShifter;
    private Timer? uiUpdateTimer;

    private SoundTouchWaveProvider? soundTouch;
    private IWaveProvider? soundTouchWaveProvider;

    // Modify PlaybackRate property
    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            value = Math.Clamp(value, 0.5f, 2.0f);
            if (SetProperty(ref _playbackRate, value) && soundTouch != null)
            {
                soundTouch.Tempo = value;  // Set TEMPO not RATE
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
                if (pitchShifter != null)
                {
                    pitchShifter.PitchFactor = (float)Math.Pow(2, value / 12.0);
                }
                // If pitch changes while paused, and we want it to apply immediately without full reinit like speed:
                // This is fine as SMB provider can change pitch factor on the fly.
            }
        }
    }

    public PlaybackService()
    {
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateUiCallback(object? state)
    {
        if (IsPlaying && audioFileReader != null && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (audioFileReader == null) return;

                CurrentPosition = audioFileReader.CurrentTime; // CurrentPosition is true time

                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    // Loop times are stored as true time. CurrentPosition is true time.
                    if (CurrentPosition >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        Seek(activeLoop.Start); // Use Seek to handle underlying audioFileReader.CurrentTime
                        CurrentPosition = activeLoop.Start; // Explicitly update after seek
                    }
                }
            });
        }
    }

    private ISampleProvider AdjustSpeed(AudioFileReader reader, float speedFactor)
    {
        // Use a small epsilon for float comparison
        if (Math.Abs(speedFactor - 1.0f) < 0.001f)
        {
            return reader.ToSampleProvider(); // No speed change needed
        }

        var sourceWaveFormat = reader.WaveFormat; // This is typically IEEE float from AudioFileReader

        // To play 'speedFactor' times faster, MFR needs to output samples at 'speedFactor' times the original rate.
        int targetSampleRate = (int)(sourceWaveFormat.SampleRate * speedFactor);

        // The output format for MediaFoundationResampler must be float if SmbPitchShiftingSampleProvider expects float.
        // AudioFileReader's ISampleProvider interface provides float samples.
        var resamplerOutputFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetSampleRate, sourceWaveFormat.Channels);

        var resampler = new MediaFoundationResampler(reader, resamplerOutputFormat)
        {
            ResamplerQuality = 60 // 0 (linear interpolation, fastest) to 60 (best quality, slowest)
        };
        return resampler.ToSampleProvider();
    }

    private void InitializeNAudioPipeline(string filePath)
    {
        CleanUpPlaybackResources(); // Ensure this is working and called

        try
        {
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] Initializing NAudio pipeline for: {Path.GetFileName(filePath)}");
            audioFileReader = new AudioFileReader(filePath);
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] AudioFileReader created. Format: {audioFileReader.WaveFormat}, Type: {audioFileReader.GetType().Name}");

            // Step 1: Convert AudioFileReader to ISampleProvider
            ISampleProvider sourceSampleProvider = audioFileReader.ToSampleProvider();
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] SourceSampleProvider (from AudioFileReader) created. Format: {sourceSampleProvider.WaveFormat}, Type: {sourceSampleProvider.GetType().Name}");

            // Step 2: Convert to Mono ISampleProvider
            ISampleProvider monoSampleProvider = sourceSampleProvider.ToMono();
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] MonoSampleProvider (from ToMono) created. Format: {monoSampleProvider.WaveFormat}, Type: {monoSampleProvider.GetType().Name}");
            // The StereoToMonoSampleProvider takes the sourceProvider in its constructor, but doesn't expose it publicly.
            // We can infer its input format was sourceSampleProvider.WaveFormat.

            // Step 3: Convert the mono ISampleProvider to IWaveProvider for SoundTouchWaveProvider
            // This is the crucial conversion. SampleToWaveProvider converts float samples back to a byte-based wave format (typically 16-bit PCM).
            IWaveProvider monoWaveProviderForSoundTouch = new SampleToWaveProvider(monoSampleProvider);
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] MonoWaveProviderForSoundTouch (from SampleToWaveProvider) created. Format: {monoWaveProviderForSoundTouch.WaveFormat}, Type: {monoWaveProviderForSoundTouch.GetType().Name}");

            // Step 4: Initialize SoundTouchWaveProvider with the IWaveProvider
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] Attempting to create SoundTouchWaveProvider...");
            soundTouch = new SoundTouchWaveProvider(monoWaveProviderForSoundTouch)
            {
                Tempo = PlaybackRate,
                Rate = 1.0f,
            };
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] SoundTouchWaveProvider created. Output Format: {soundTouch.WaveFormat}, Type: {soundTouch.GetType().Name}");

            // Step 5: Apply pitch shifting. SoundTouchWaveProvider also offers ISampleProvider via .ToSampleProvider()
            ISampleProvider soundTouchAsSampleProvider = soundTouch.ToSampleProvider();
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] SoundTouch as ISampleProvider. Format: {soundTouchAsSampleProvider.WaveFormat}, Type: {soundTouchAsSampleProvider.GetType().Name}");

            pitchShifter = new SmbPitchShiftingSampleProvider(soundTouchAsSampleProvider)
            {
                PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0)
            };
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] PitchShifter created. Output Format: {pitchShifter.WaveFormat}, Type: {pitchShifter.GetType().Name}");

            // Step 6: Initialize WaveOutDevice. SmbPitchShiftingSampleProvider needs to be converted back to IWaveProvider.
            IWaveProvider finalWaveProviderForDevice = pitchShifter.ToWaveProvider();
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] FinalWaveProviderForDevice (from pitchShifter.ToWaveProvider) created. Format: {finalWaveProviderForDevice.WaveFormat}, Type: {finalWaveProviderForDevice.GetType().Name}");

            waveOutDevice = new WaveOutEvent();
            waveOutDevice.Init(finalWaveProviderForDevice);
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] WaveOutDevice initialized.");

            CurrentSongDuration = audioFileReader.TotalTime;
            CurrentPosition = TimeSpan.Zero;

            if (waveOutDevice != null)
            {
                waveOutDevice.PlaybackStopped += OnPlaybackStopped;
            }
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] NAudio pipeline initialization complete for: {Path.GetFileName(filePath)}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService {DateTime.Now:HH:mm:ss.fff}] CRITICAL ERROR during NAudio pipeline init for {Path.GetFileName(filePath)}: {ex.ToString()}");
            // Clean up again in case of partial initialization to prevent issues on next attempt
            CleanUpPlaybackResources();
            // Optional: re-throw or handle UI error reporting
            // throw; 
        }
    }

    public void Play(Song song)
    {
        if (song == null || string.IsNullOrEmpty(song.FilePath)) return;

        bool isSameSong = CurrentSong?.FilePath == song.FilePath;
        TimeSpan restartPosition = TimeSpan.Zero;

        if (isSameSong && audioFileReader != null)
        {
            // If it's the same song, potentially restart from current pos or 0
            // For now, standard behavior is play from start unless it's a resume-like action
            // Let's assume play means from the beginning unless explicitly seeking.
        }

        CurrentSong = song; // Set current song before initialization

        InitializeNAudioPipeline(song.FilePath);

        if (waveOutDevice != null && audioFileReader != null) // Check if initialization was successful
        {
            if (restartPosition > TimeSpan.Zero && restartPosition < audioFileReader.TotalTime)
            {
                audioFileReader.CurrentTime = restartPosition;
            }
            waveOutDevice.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
        else
        {
            // Initialization failed, states should be reset by InitializeNAudioPipeline's catch block
            Debug.WriteLine($"[PlaybackService] Playback not started due to initialization failure for {song.Title}.");
        }
    }

    private void StartUiUpdateTimer() => uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private void StopUiUpdateTimer() => uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);

    private void CleanUpPlaybackResources()
    {
        StopUiUpdateTimer();

        waveOutDevice?.Stop();
        if (waveOutDevice != null)
        {
            waveOutDevice.PlaybackStopped -= OnPlaybackStopped; // Important to unhook event handlers
            waveOutDevice.Dispose();
            waveOutDevice = null;
        }

        // pitchShifter itself doesn't implement IDisposable typically if it's just a sample provider wrapper.
        // It's the underlying stream (audioFileReader) that needs disposal.
        pitchShifter = null;
        finalSampleProvider = null; // Also just a reference, not disposable itself

        audioFileReader?.Dispose();
        audioFileReader = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // This event can fire for multiple reasons: natural end, Stop() called, or error.
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Only change state if it wasn't an intentional stop that already changed state
            if (CurrentPlaybackStatus != PlaybackStateStatus.Stopped || IsPlaying)
            {
                // If playback stops due to reaching the end of the song or an error
                if (audioFileReader != null && audioFileReader.CurrentTime >= audioFileReader.TotalTime)
                {
                    // Song finished naturally
                    CurrentPosition = CurrentSongDuration; // Ensure position reflects end
                }
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }

            StopUiUpdateTimer();
            if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] NAudio Playback Error: {e.Exception.Message}");
                // Optionally, update UI with error
            }
        });
    }

    public void Pause()
    {
        if (IsPlaying && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            waveOutDevice.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer();
        }
    }

    public void Resume()
    {
        if (!IsPlaying && CurrentSong != null)
        {
            // If pipeline is not initialized (e.g., after stop or if it was never played)
            if (waveOutDevice == null || audioFileReader == null || waveOutDevice.PlaybackState == PlaybackState.Stopped)
            {
                TimeSpan resumePosition = CurrentPosition; // Use the last known true position

                InitializeNAudioPipeline(CurrentSong.FilePath);
                if (audioFileReader != null) // If init successful
                {
                    Seek(resumePosition); // Seek to the saved position
                }
                else
                {
                    Debug.WriteLine($"[PlaybackService] Resume failed: Could not re-initialize pipeline for {CurrentSong.Title}.");
                    return; // Init failed
                }
            }

            if (waveOutDevice != null && waveOutDevice.PlaybackState != PlaybackState.Playing)
            {
                waveOutDevice.Play();
                IsPlaying = true;
                CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                StartUiUpdateTimer();
            }
        }
    }

    public void Stop()
    {
        // Set state before actually stopping/disposing to avoid race conditions with PlaybackStopped event
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;

        // CleanUpPlaybackResources will call waveOutDevice.Stop() and dispose
        CleanUpPlaybackResources();

        CurrentPosition = TimeSpan.Zero; // Reset position after stopping
        // CurrentSong remains, so user can see what was last played. To clear it: CurrentSong = null;
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (audioFileReader != null)
        {
            var targetPosition = positionInTrueTime;
            if (targetPosition < TimeSpan.Zero) targetPosition = TimeSpan.Zero;
            if (targetPosition > audioFileReader.TotalTime) targetPosition = audioFileReader.TotalTime;

            audioFileReader.CurrentTime = targetPosition;
            CurrentPosition = audioFileReader.CurrentTime; // Update our tracking property immediately
        }
    }

    public void Dispose()
    {
        CleanUpPlaybackResources();
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
        GC.SuppressFinalize(this);
    }
}