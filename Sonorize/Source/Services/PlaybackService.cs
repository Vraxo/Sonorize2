using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using NAudio.Dsp; // Required for WdlResamplingSampleProvider
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Threading;
using System.Diagnostics;

namespace Sonorize.Services;
public enum PlaybackStateStatus { Stopped, Playing, Paused }
public class PlaybackService : ViewModelBase, IDisposable
{
    private Song? _currentSong;
    public Song? CurrentSong { get => _currentSong; private set { SetProperty(ref _currentSong, value); OnPropertyChanged(nameof(HasCurrentSong)); } }
    public bool HasCurrentSong => CurrentSong != null;

    private bool _isPlaying;
    public bool IsPlaying { get => _isPlaying; private set { SetProperty(ref _isPlaying, value); } }

    private PlaybackStateStatus _currentPlaybackStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get => _currentPlaybackStatus;
        private set => SetProperty(ref _currentPlaybackStatus, value);
    }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition { get => _currentPosition; set { if (SetProperty(ref _currentPosition, value)) OnPropertyChanged(nameof(CurrentPositionSeconds)); } }

    public double CurrentPositionSeconds
    {
        get => CurrentPosition.TotalSeconds;
        set
        {
            if (audioFileReader != null && _playbackRate > 0)
            {
                var trueFileTime = TimeSpan.FromSeconds(value * _playbackRate);
                // Adjusted sensitivity for seeking, especially at higher rates
                if (Math.Abs(audioFileReader.CurrentTime.TotalSeconds - trueFileTime.TotalSeconds) > (0.2 / _playbackRate))
                {
                    Seek(trueFileTime);
                }
            }
        }
    }

    private TimeSpan _currentSongDuration;
    public TimeSpan CurrentSongDuration { get => _currentSongDuration; private set { if (SetProperty(ref _currentSongDuration, value)) OnPropertyChanged(nameof(CurrentSongDurationSeconds)); } }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1;

    private IWavePlayer? waveOutDevice;
    private AudioFileReader? audioFileReader;
    private WdlResamplingSampleProvider? resampler;
    private Timer? uiUpdateTimer;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            float newRate = Math.Max(0.1f, value); // Ensure rate is positive
            Debug.WriteLine($"[PlaybackService] PlaybackRate SETTER called. Current field _playbackRate: {this._playbackRate}, Attempting to set to: {newRate}");

            if (Math.Abs(this._playbackRate - newRate) > 0.001f)
            {
                this._playbackRate = newRate; // CRITICAL: Update the backing field FIRST
                OnPropertyChanged(); // Notify for "PlaybackRate" property if anything binds directly to it
                Debug.WriteLine($"[PlaybackService] _playbackRate field UPDATED to: {this._playbackRate}");

                if (CurrentSong != null && (IsPlaying || CurrentPlaybackStatus == PlaybackStateStatus.Paused))
                {
                    Debug.WriteLine($"[PlaybackService] Rate changed while playing/paused. Re-initializing. WasPlaying: {IsPlaying}, Status: {CurrentPlaybackStatus}");
                    TimeSpan? currentTrueFileTime = audioFileReader?.CurrentTime;
                    bool wasPlaying = IsPlaying;

                    // Stop current playback cleanly before re-initializing
                    // Do not call CleanUpPlaybackResources() here as it nulls out audioFileReader too early
                    waveOutDevice?.Stop();
                    waveOutDevice?.Dispose();
                    waveOutDevice = null;
                    // resampler will be recreated, audioFileReader is preserved if path is same

                    Debug.WriteLine($"[PlaybackService] Old waveOutDevice stopped for rate change.");

                    try
                    {
                        InitializeNAudioPipeline(CurrentSong.FilePath, false); // false = preserve audioFileReader if possible, re-init resampler

                        if (audioFileReader != null && currentTrueFileTime.HasValue)
                        {
                            audioFileReader.CurrentTime = currentTrueFileTime.Value; // Restore position in file
                            Debug.WriteLine($"[PlaybackService] Restored true file time to {audioFileReader.CurrentTime} after rate change re-init.");
                        }
                        UpdateDurationsAndPositions(); // Update UI times based on new rate and current file time

                        if (wasPlaying)
                        {
                            waveOutDevice?.Play();
                            Debug.WriteLine($"[PlaybackService] Restarted waveOutDevice play after rate change.");
                        }
                        else
                        {
                            Debug.WriteLine($"[PlaybackService] Was paused. Pipeline re-initialized for new rate. Ready to resume.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PlaybackService] CRITICAL Error re-initializing for new playback rate: {ex.Message}. Cleaning up.");
                        CleanUpPlaybackResources(); // Full cleanup on error
                        IsPlaying = false;
                        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                        UpdateDurationsAndPositions(); // Reset durations
                    }
                }
                else if (CurrentSong != null)
                {
                    UpdateDurationsAndPositions(); // Song loaded but not playing, just update perceived durations
                    Debug.WriteLine($"[PlaybackService] Rate changed while stopped/no song. Updated durations for new rate {this._playbackRate}.");
                }
                Debug.WriteLine($"[PlaybackService] PlaybackRate property logic finished. Final _playbackRate: {this._playbackRate}. Speed AND PITCH will be affected.");
            }
            else
            {
                Debug.WriteLine($"[PlaybackService] PlaybackRate SETTER: New value {newRate} is too close to current {this._playbackRate}. No change triggered.");
            }
        }
    }

    private float _pitchSemitones = 0f; // Unused with current resampling method for speed
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            if (SetProperty(ref _pitchSemitones, value))
            {
                Debug.WriteLineIf(_pitchSemitones != 0f, "[PlaybackService] PitchSemitones changed, but current speed control (resampling) changes pitch with speed.");
            }
        }
    }

    public PlaybackService()
    {
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateDurationsAndPositions()
    {
        if (audioFileReader != null && this._playbackRate > 0)
        {
            CurrentSongDuration = audioFileReader.TotalTime.Divide(this._playbackRate);
            CurrentPosition = audioFileReader.CurrentTime.Divide(this._playbackRate);
        }
        else
        {
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
        }
        // Debug.WriteLine($"[PlaybackService.UpdateDurations] Rate: {this._playbackRate}, FileDur: {audioFileReader?.TotalTime}, UIDur: {CurrentSongDuration}, FilePos: {audioFileReader?.CurrentTime}, UIPos: {CurrentPosition}");
    }

    private void UpdateUiCallback(object? state)
    {
        if (IsPlaying && audioFileReader != null && waveOutDevice?.PlaybackState == NAudio.Wave.PlaybackState.Playing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (audioFileReader == null || resampler == null || this._playbackRate <= 0) return;
                CurrentPosition = audioFileReader.CurrentTime.Divide(this._playbackRate);

                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    var actualPlaybackTimeInFile = audioFileReader.CurrentTime;
                    if (actualPlaybackTimeInFile >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        audioFileReader.CurrentTime = CurrentSong.ActiveLoop.Start;
                        CurrentPosition = audioFileReader.CurrentTime.Divide(this._playbackRate);
                    }
                }
            });
        }
    }

    private void InitializeNAudioPipeline(string filePath, bool resetFullStateAndReader = true)
    {
        Debug.WriteLine($"[PlaybackService.InitPipeline] Called. File: '{filePath}', resetFullStateAndReader: {resetFullStateAndReader}, Current field _playbackRate: {this._playbackRate}");

        // Clean up waveOut and resampler, preserve audioFileReader if path is same & not full reset
        waveOutDevice?.Stop(); // Stop first
        waveOutDevice?.Dispose();
        waveOutDevice = null;
        resampler = null;
        Debug.WriteLine($"[PlaybackService.InitPipeline] Old waveOutDevice and resampler disposed/nulled.");

        if (resetFullStateAndReader || audioFileReader == null || audioFileReader.FileName != filePath)
        {
            audioFileReader?.Dispose();
            audioFileReader = new AudioFileReader(filePath);
            Debug.WriteLine($"[PlaybackService.InitPipeline] AudioFileReader CREATED/REPLACED for '{filePath}'. CurrentTime reset to 0.");
        }
        else
        {
            Debug.WriteLine($"[PlaybackService.InitPipeline] AudioFileReader PRESERVED for '{filePath}'. CurrentTime: {audioFileReader?.CurrentTime}");
        }

        if (audioFileReader == null) throw new InvalidOperationException("AudioFileReader could not be initialized or remained null.");
        Debug.WriteLine($"[PlaybackService.InitPipeline] audioFileReader SampleRate: {audioFileReader.WaveFormat.SampleRate}, Channels: {audioFileReader.WaveFormat.Channels}");

        // Ensure _playbackRate is used for targetSampleRate
        int targetSampleRate = (int)(audioFileReader.WaveFormat.SampleRate * this._playbackRate);
        if (targetSampleRate < 8000) targetSampleRate = 8000;
        if (targetSampleRate > 384000) targetSampleRate = 384000; // Increased upper sanity bound

        Debug.WriteLine($"[PlaybackService.InitPipeline] Using _playbackRate: {this._playbackRate} for calculation. Calculated targetSampleRate for WDLResampler: {targetSampleRate}");

        resampler = new WdlResamplingSampleProvider(audioFileReader, targetSampleRate);
        // Explicitly set quality for testing, though default should be okay.
        // Values are 0 (poorest/fastest) to 64 (best/slowest). Let's try a decent one.
        // resampler.ResamplerQuality = 2; // NAudio default for WDL fixed at 2
        Debug.WriteLine($"[PlaybackService.InitPipeline] WdlResampler created. Output WaveFormat: SR={resampler.WaveFormat.SampleRate}, Channels={resampler.WaveFormat.Channels}, Bits={resampler.WaveFormat.BitsPerSample}");

        if (resampler.WaveFormat.SampleRate != targetSampleRate)
        {
            Debug.WriteLine($"[PlaybackService.InitPipeline] WARNING: Resampler output SR ({resampler.WaveFormat.SampleRate}) does NOT match target SR ({targetSampleRate})!");
        }

        waveOutDevice = new WaveOutEvent();
        waveOutDevice.PlaybackStopped += OnPlaybackStopped;
        waveOutDevice.Init(resampler); // Init with the resampler
        Debug.WriteLine($"[PlaybackService.InitPipeline] New waveOutDevice initialized with resampler.");

        if (resetFullStateAndReader)
        {
            UpdateDurationsAndPositions();
        }
    }

    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService.Play] Called for song: '{song?.Title}'. Current _playbackRate: {this._playbackRate}");
        if (song == null) return;

        CurrentSong = song;

        try
        {
            InitializeNAudioPipeline(song.FilePath, true); // true = full reset for new song

            waveOutDevice?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService.Play] Playback started for '{song.Title}'.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing playback for {song.FilePath}: {ex.Message}");
            Debug.WriteLine($"[PlaybackService.Play] EXCEPTION: {ex.ToString()}");
            CleanUpPlaybackResources();
            IsPlaying = false; CurrentSong = null;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            UpdateDurationsAndPositions();
        }
    }

    private void StartUiUpdateTimer() => uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private void StopUiUpdateTimer() => uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);

    private void CleanUpPlaybackResources()
    {
        Debug.WriteLine("[PlaybackService.CleanUpResources] Called.");
        StopUiUpdateTimer();

        waveOutDevice?.Stop();
        waveOutDevice?.Dispose();
        waveOutDevice = null;

        resampler = null;
        audioFileReader?.Dispose();
        audioFileReader = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Debug.WriteLine($"[PlaybackService.OnPlaybackStopped] Called. CurrentPlaybackStatus: {CurrentPlaybackStatus}, IsPlaying: {IsPlaying}, Exception: {e.Exception?.Message}");
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (CurrentPlaybackStatus != PlaybackStateStatus.Paused) // If not explicitly paused by user
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped; // Default to stopped
            }
            StopUiUpdateTimer();
            if (e.Exception != null)
            {
                Console.WriteLine($"NAudio Playback Error: {e.Exception.Message}");
                Debug.WriteLine($"[PlaybackService.OnPlaybackStopped] NAudio EXCEPTION: {e.Exception.ToString()}");
            }
        });
    }

    public void Pause()
    {
        Debug.WriteLine("[PlaybackService.Pause] Called.");
        if (IsPlaying && waveOutDevice?.PlaybackState == NAudio.Wave.PlaybackState.Playing)
        {
            waveOutDevice.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer();
        }
    }

    public void Resume()
    {
        Debug.WriteLine($"[PlaybackService.Resume] Called. CurrentPlaybackStatus: {CurrentPlaybackStatus}, HasCurrentSong: {HasCurrentSong}");
        if (!IsPlaying && CurrentSong != null)
        {
            if (waveOutDevice == null || audioFileReader == null || resampler == null || waveOutDevice.PlaybackState == NAudio.Wave.PlaybackState.Stopped)
            {
                Debug.WriteLine("[PlaybackService.Resume] Pipeline seems stopped or incomplete, re-initializing.");
                TimeSpan resumePositionInTrueFileTime = audioFileReader?.CurrentTime ?? TimeSpan.Zero;
                if (resumePositionInTrueFileTime == TimeSpan.Zero && CurrentPosition > TimeSpan.Zero && this._playbackRate > 0)
                {
                    resumePositionInTrueFileTime = CurrentPosition.Multiply(this._playbackRate);
                }

                try
                {
                    InitializeNAudioPipeline(CurrentSong.FilePath, false);
                    if (audioFileReader != null)
                    {
                        audioFileReader.CurrentTime = resumePositionInTrueFileTime;
                    }
                    UpdateDurationsAndPositions();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error re-initializing for resume: {ex.Message}");
                    CleanUpPlaybackResources();
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    UpdateDurationsAndPositions();
                    return;
                }
            }
            waveOutDevice?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
    }

    public void Stop()
    {
        Debug.WriteLine("[PlaybackService.Stop] Called.");
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        waveOutDevice?.Stop();

        if (audioFileReader != null) audioFileReader.Position = 0; // Reset file position
        UpdateDurationsAndPositions(); // Update UI to show 0 position
    }

    public void Seek(TimeSpan positionInTrueFileTime)
    {
        Debug.WriteLine($"[PlaybackService.Seek] Called. positionInTrueFileTime: {positionInTrueFileTime}");
        if (audioFileReader != null && resampler != null) // Ensure resampler exists as it's part of active pipeline
        {
            var targetFilePosition = positionInTrueFileTime;
            if (targetFilePosition < TimeSpan.Zero) targetFilePosition = TimeSpan.Zero;
            if (targetFilePosition > audioFileReader.TotalTime) targetFilePosition = audioFileReader.TotalTime;

            audioFileReader.CurrentTime = targetFilePosition;

            if (this._playbackRate > 0)
            {
                CurrentPosition = audioFileReader.CurrentTime.Divide(this._playbackRate);
            }
            Debug.WriteLine($"[PlaybackService.Seek] Seeked. audioFileReader.CurrentTime: {audioFileReader.CurrentTime}, UI CurrentPosition: {CurrentPosition}");
        }
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService.Dispose] Called.");
        CleanUpPlaybackResources();
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
        GC.SuppressFinalize(this);
    }
}