using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders; // For SmbPitchShiftingSampleProvider
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Diagnostics;
using System.Threading;

namespace Sonorize.Services;

public enum PlaybackStateStatus { Stopped, Playing, Paused }

public class PlaybackService : ViewModelBase, IDisposable
{
    private Song? _currentSong;
    public Song? CurrentSong { get => _currentSong; private set { SetProperty(ref _currentSong, value); OnPropertyChanged(nameof(HasCurrentSong)); } }
    public bool HasCurrentSong => CurrentSong != null;

    private bool _isPlaying;
    public bool IsPlaying { get => _isPlaying; private set => SetProperty(ref _isPlaying, value); }

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus { get => _currentPlaybackStatus; private set => SetProperty(ref _currentPlaybackStatus, value); }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition { get => _currentPosition; set { if (SetProperty(ref _currentPosition, value)) OnPropertyChanged(nameof(CurrentPositionSeconds)); } }
    public double CurrentPositionSeconds { get => CurrentPosition.TotalSeconds; set { if (audioFileReader != null && Math.Abs(CurrentPosition.TotalSeconds - value) > 0.1) Seek(TimeSpan.FromSeconds(value)); } }

    private TimeSpan _currentSongDuration;
    public TimeSpan CurrentSongDuration { get => _currentSongDuration; private set { if (SetProperty(ref _currentSongDuration, value)) OnPropertyChanged(nameof(CurrentSongDurationSeconds)); } }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1;

    private IWavePlayer? waveOutDevice;
    private AudioFileReader? audioFileReader;
    private MediaFoundationResampler? speedAdjuster; // MFR for speed + pitch change
    private SmbPitchShiftingSampleProvider? pitchShifter; // For pitch correction and user pitch
    private ISampleProvider? finalSampleProvider;
    private Timer? uiUpdateTimer;

    private float _playbackRate = 1.0f; // Speed factor
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            float newRate = Math.Max(0.5f, Math.Min(value, 2.0f));
            if (Math.Abs(_playbackRate - newRate) < 0.001f) return;

            _playbackRate = newRate;
            Debug.WriteLine($"[PlaybackService] PlaybackRate changed to: {_playbackRate}");
            OnPropertyChanged();

            // Changing speed requires full pipeline re-initialization
            // because the output format of MediaFoundationResampler changes.
            if (CurrentSong != null && (CurrentPlaybackStatus == PlaybackStateStatus.Playing || CurrentPlaybackStatus == PlaybackStateStatus.Paused))
            {
                ReInitializePipelineForParameterChange(true); // true indicates rate change
            }
        }
    }

    private float _pitchSemitones = 0f;
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            float newPitch = Math.Max(-12f, Math.Min(value, 12f)); // e.g., +/- 1 octave
            if (Math.Abs(_pitchSemitones - newPitch) < 0.001f) return;

            _pitchSemitones = newPitch;
            Debug.WriteLine($"[PlaybackService] PitchSemitones changed to: {_pitchSemitones}");
            OnPropertyChanged();

            // If only pitch changes, we can try to update SmbPitchShiftingSampleProvider directly
            // without a full pipeline rebuild, IF the speed part (MFR) hasn't changed.
            if (pitchShifter != null && CurrentSong != null && (CurrentPlaybackStatus == PlaybackStateStatus.Playing || CurrentPlaybackStatus == PlaybackStateStatus.Paused))
            {
                UpdatePitchShifterFactor();
            }
            // If pipeline is not active, new pitch is picked up on next Play/Resume.
        }
    }

    private void UpdatePitchShifterFactor()
    {
        if (pitchShifter == null) return;

        float userPitchFactor = (float)Math.Pow(2, _pitchSemitones / 12.0);
        // Correct for the pitch change introduced by MediaFoundationResampler's speed adjustment
        float rateCorrectionFactor = (Math.Abs(_playbackRate) > 0.001f && Math.Abs(_playbackRate - 1.0f) > 0.001f) ? (1.0f / _playbackRate) : 1.0f;

        pitchShifter.PitchFactor = userPitchFactor * rateCorrectionFactor;
        Debug.WriteLine($"[PlaybackService] Updated SmbPitchShifter.PitchFactor to: {pitchShifter.PitchFactor} (UserSemi: {_pitchSemitones}, Rate: {_playbackRate}, UserFactor: {userPitchFactor}, RateCorrection: {rateCorrectionFactor})");
    }


    public PlaybackService()
    {
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void ReInitializePipelineForParameterChange(bool rateChanged)
    {
        if (CurrentSong == null || audioFileReader == null && !rateChanged) // if rateChanged, audioFileReader might be null from previous cleanup
        {
            // If only pitch changed but no song, do nothing.
            // If rate changed, we expect to rebuild from CurrentSong.FilePath.
            if (!rateChanged)
            {
                Debug.WriteLine("[PlaybackService] ReInitialize (pitch only) called with no song/reader.");
                return;
            }
        }

        TimeSpan currentTrueFileTime = audioFileReader?.CurrentTime ?? CurrentPosition; // Use CurrentPosition as fallback if reader is already gone
        bool wasPlaying = (CurrentPlaybackStatus == PlaybackStateStatus.Playing);

        Debug.WriteLine($"[PlaybackService] Re-initializing pipeline. RateChanged: {rateChanged}, WasPlaying: {wasPlaying}, TargetRate: {_playbackRate}, TargetPitchSemi: {_pitchSemitones}, RestoreTime: {currentTrueFileTime}");

        CleanUpPlaybackResources(); // Stops device, disposes components

        try
        {
            InitializeNAudioPipeline(CurrentSong.FilePath); // Rebuild with current _playbackRate and _pitchSemitones
            Seek(currentTrueFileTime); // Restore position

            if (wasPlaying)
            {
                waveOutDevice?.Play();
                IsPlaying = true;
                CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                StartUiUpdateTimer();
            }
            else // Was Paused or Stopped
            {
                IsPlaying = false;
                // If it was paused, it should remain conceptually paused, ready for Resume.
                // InitializeNAudioPipeline does not start playback.
                CurrentPlaybackStatus = PlaybackStateStatus.Paused; // Set to Paused so Resume works correctly
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error during ReInitializePipelineForParameterChange: {ex.ToString()}");
            CleanUpPlaybackResources(); // Ensure full cleanup on error
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        }
    }

    private void UpdateUiCallback(object? state)
    {
        if (IsPlaying && audioFileReader != null && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (audioFileReader == null) return;
                CurrentPosition = audioFileReader.CurrentTime;
                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    if (audioFileReader.CurrentTime >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        audioFileReader.CurrentTime = activeLoop.Start;
                        CurrentPosition = activeLoop.Start;
                    }
                }
            });
        }
    }

    private void InitializeNAudioPipeline(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.WriteLine("[PlaybackService] InitializeNAudioPipeline: filePath is null or empty.");
            throw new ArgumentNullException(nameof(filePath));
        }
        Debug.WriteLine($"[PlaybackService] Initializing NAudio pipeline for: {filePath}. Rate: {_playbackRate}, PitchSemi: {_pitchSemitones}");

        try
        {
            audioFileReader = new AudioFileReader(filePath);
            ISampleProvider providerChain = audioFileReader.ToSampleProvider(); // Start with basic provider

            // 1. Speed adjustment (which also affects pitch) using MediaFoundationResampler
            if (Math.Abs(_playbackRate - 1.0f) > 0.001f)
            {
                int targetSampleRate = (int)(audioFileReader.WaveFormat.SampleRate * _playbackRate);
                var outFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetSampleRate, audioFileReader.WaveFormat.Channels);
                speedAdjuster = new MediaFoundationResampler(audioFileReader, outFormat) { ResamplerQuality = 60 };
                providerChain = speedAdjuster.ToSampleProvider();
                Debug.WriteLine($"[PlaybackService] Applied speedAdjuster. Output SR: {targetSampleRate}");
            }
            else
            {
                speedAdjuster = null; // Not used if rate is 1.0
            }

            // 2. Pitch Shifting (corrects MFR pitch and applies user pitch)
            pitchShifter = new SmbPitchShiftingSampleProvider(providerChain);
            UpdatePitchShifterFactor(); // This calculates and sets the combined factor

            finalSampleProvider = pitchShifter;

            waveOutDevice = new WaveOutEvent();
            waveOutDevice.PlaybackStopped += OnPlaybackStopped;
            waveOutDevice.Init(finalSampleProvider);

            CurrentSongDuration = audioFileReader.TotalTime;
            CurrentPosition = TimeSpan.Zero;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] EXCEPTION in InitializeNAudioPipeline for {filePath}: {ex.ToString()}");
            CleanUpPlaybackResources(); // Clean up any partial setup
            throw;
        }
    }

    public void Play(Song song)
    {
        if (song == null) return;
        Debug.WriteLine($"[PlaybackService] Play requested for: {song.Title}");

        CleanUpPlaybackResources(); // Always stop and clean before playing a new song or restarting
        CurrentSong = song;

        try
        {
            InitializeNAudioPipeline(song.FilePath);
            waveOutDevice?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Started playing: {song.Title}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error during Play for {song.FilePath}: {ex.ToString()}");
            CleanUpPlaybackResources();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            CurrentSong = null; // Clear song if it failed to play
        }
    }

    private void StartUiUpdateTimer() => uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private void StopUiUpdateTimer() => uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);

    private void CleanUpPlaybackResources()
    {
        Debug.WriteLine("[PlaybackService] CleanUpPlaybackResources called.");
        StopUiUpdateTimer();

        waveOutDevice?.Stop();
        if (waveOutDevice != null)
        {
            waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
            waveOutDevice.Dispose();
            waveOutDevice = null;
        }

        // Order of disposal/nulling doesn't strictly matter here as they aren't chained in disposal
        pitchShifter = null;
        speedAdjuster?.Dispose(); // MFR is IDisposable
        speedAdjuster = null;
        audioFileReader?.Dispose(); // AudioFileReader is IDisposable
        audioFileReader = null;

        finalSampleProvider = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Debug.WriteLine($"[PlaybackService] OnPlaybackStopped. CurrentStatus: {CurrentPlaybackStatus}, IsPlaying: {IsPlaying}. Exception: {e.Exception?.Message}");

            if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] NAudio PlaybackStoppped Error: {e.Exception.Message}");
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
            else if (CurrentPlaybackStatus != PlaybackStateStatus.Paused)
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
            else if (CurrentPlaybackStatus == PlaybackStateStatus.Paused && (waveOutDevice == null || waveOutDevice.PlaybackState == PlaybackState.Stopped))
            {
                // If it was Paused but underlying device stopped for some other reason (not user pause)
                IsPlaying = false;
            }
            StopUiUpdateTimer();
        });
    }

    public void Pause()
    {
        if (CurrentPlaybackStatus == PlaybackStateStatus.Playing && waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            Debug.WriteLine("[PlaybackService] Pause called.");
            waveOutDevice.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer();
        }
    }

    public void Resume()
    {
        if (CurrentSong == null)
        {
            Debug.WriteLine("[PlaybackService] Resume called but no CurrentSong.");
            return;
        }
        Debug.WriteLine($"[PlaybackService] Resume called. CurrentStatus: {CurrentPlaybackStatus}");

        if (CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            if (waveOutDevice != null && audioFileReader != null && waveOutDevice.PlaybackState == PlaybackState.Paused)
            {
                waveOutDevice.Play();
                IsPlaying = true;
                CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                StartUiUpdateTimer();
                Debug.WriteLine($"[PlaybackService] Resumed playback of {CurrentSong.Title}");
            }
            else
            {
                Debug.WriteLine("[PlaybackService] Resume: Was Paused but pipeline state unexpected. Re-initializing.");
                // This will re-initialize and set to Paused state, then we explicitly Play.
                ReInitializePipelineForParameterChange(false); // false = pitch only conceptually, but it re-inits all
                if (CurrentSong != null && waveOutDevice != null && waveOutDevice.PlaybackState != PlaybackState.Playing)
                {
                    waveOutDevice.Play();
                    IsPlaying = true;
                    CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                    StartUiUpdateTimer();
                    Debug.WriteLine($"[PlaybackService] Resumed (after re-init) playback of {CurrentSong.Title}");
                }
            }
        }
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume: Was Stopped. Calling Play.");
            Play(CurrentSong); // This will re-initialize and start from beginning or CurrentPosition if Play is modified
                               // If Play always starts from 0, and we want to resume from CurrentPosition:
            if (CurrentPosition > TimeSpan.Zero && CurrentPosition < CurrentSongDuration && audioFileReader != null)
            {
                Seek(CurrentPosition);
            }
        }
    }

    public void Stop()
    {
        Debug.WriteLine("[PlaybackService] Stop called.");
        CleanUpPlaybackResources();
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        CurrentPosition = TimeSpan.Zero; // Reset position on explicit stop
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (audioFileReader != null)
        {
            var targetPosition = TimeSpan.FromSeconds(Math.Clamp(positionInTrueTime.TotalSeconds, 0, audioFileReader.TotalTime.TotalSeconds));
            audioFileReader.CurrentTime = targetPosition;
            CurrentPosition = targetPosition; // Keep our ViewModel's CurrentPosition in sync
            Debug.WriteLine($"[PlaybackService] Seeked to: {CurrentPosition} (True file time)");
        }
        else
        {
            CurrentPosition = positionInTrueTime; // Store for later if reader not ready
            Debug.WriteLine($"[PlaybackService] Seek (audioFileReader null): Stored target position {CurrentPosition}");
        }
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose called.");
        CleanUpPlaybackResources();
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
        GC.SuppressFinalize(this);
    }
}