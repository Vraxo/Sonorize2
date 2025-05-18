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
        private set // Changed to private set
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

                this.CurrentPosition = audioFileReader.CurrentTime; // Update CurrentPosition via its private setter

                if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
                {
                    var loop = CurrentSong.SavedLoop;
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
            this.CurrentPosition = TimeSpan.Zero;
            Debug.WriteLine("[PlaybackService] Play called with null/invalid song. State is stopped, CurrentSong nulled.");
            return;
        }

        CurrentSong = song;
        bool pipelineInitialized = InitializeNAudioPipeline(song.FilePath); // This sets CurrentPosition to Zero initially

        if (pipelineInitialized && _waveOutDevice != null && audioFileReader != null)
        {
            if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
            {
                Debug.WriteLine($"[PlaybackService] Song has active loop. Seeking to loop start: {CurrentSong.SavedLoop.Start} before playing.");
                Seek(CurrentSong.SavedLoop.Start); // Seek will update CurrentPosition
            }
            // If not looping, CurrentPosition is already Zero from InitializeNAudioPipeline

            _waveOutDevice.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}");
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Pipeline init failed for {Path.GetFileName(song.FilePath)}. Cleaning up and stopping.");
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            StopUiUpdateTimer();
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            this.CurrentPosition = TimeSpan.Zero;
        }
    }


    private bool InitializeNAudioPipeline(string filePath)
    {
        Debug.WriteLine($"[PlaybackService] InitializeNAudioPipeline for: {Path.GetFileName(filePath)}");
        try
        {
            audioFileReader = new AudioFileReader(filePath);
            ISampleProvider sourceSampleProvider = audioFileReader.ToSampleProvider();
            ISampleProvider monoSampleProvider = sourceSampleProvider.ToMono();
            IWaveProvider monoWaveProviderForSoundTouch = new SampleToWaveProvider(monoSampleProvider);

            soundTouch = new SoundTouchWaveProvider(monoWaveProviderForSoundTouch)
            {
                Tempo = PlaybackRate,
                Rate = 1.0f,
            };

            ISampleProvider soundTouchAsSampleProvider = soundTouch.ToSampleProvider();

            pitchShifter = new SmbPitchShiftingSampleProvider(soundTouchAsSampleProvider)
            {
                PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0)
            };

            IWaveProvider finalWaveProviderForDevice = pitchShifter.ToWaveProvider();

            _waveOutDevice = new WaveOutEvent();
            _waveOutDeviceInstanceForStopEventCheck = _waveOutDevice;
            _waveOutDevice.PlaybackStopped += OnPlaybackStopped;
            _waveOutDevice.Init(finalWaveProviderForDevice);

            CurrentSongDuration = audioFileReader.TotalTime;
            this.CurrentPosition = TimeSpan.Zero; // Set position to zero for new song
            Debug.WriteLine($"[PlaybackService] NAudio pipeline initialization COMPLETE for: {Path.GetFileName(filePath)}. Duration: {CurrentSongDuration}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during NAudio pipeline init for {Path.GetFileName(filePath)}: {ex.ToString()}");
            CleanUpNAudioResources();
            CurrentSongDuration = TimeSpan.Zero;
            this.CurrentPosition = TimeSpan.Zero;
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
            Debug.WriteLine($"[PlaybackService] OnPlaybackStopped: Exception: {e.Exception?.Message ?? "None"}");
            if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] Playback stopped due to error: {e.Exception.Message}");
            }

            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            StopUiUpdateTimer();

            bool naturalEndOfSong = CurrentSong != null && audioFileReader != null &&
                                    audioFileReader.CurrentTime >= audioFileReader.TotalTime - TimeSpan.FromMilliseconds(500);

            if (naturalEndOfSong && (CurrentSong?.IsLoopActive == false || CurrentSong?.SavedLoop == null))
            {
                Debug.WriteLine($"[PlaybackService] Natural end of song: {CurrentSong?.Title}. Resetting position.");
                this.CurrentPosition = TimeSpan.Zero;
            }
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
            StopUiUpdateTimer();
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
        else if (CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Stopped state. Re-playing current song.");
            Play(CurrentSong);
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Resume requested but conditions not met. State: {_waveOutDevice?.PlaybackState}, AFR: {audioFileReader != null}, Status: {CurrentPlaybackStatus}");
        }
    }

    private void StopPlaybackInternal(bool resetCurrentSongAndRelatedState = true)
    {
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        StopUiUpdateTimer();
        CleanUpNAudioResources();

        if (resetCurrentSongAndRelatedState)
        {
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            this.CurrentPosition = TimeSpan.Zero;
            Debug.WriteLine("[PlaybackService] StopPlaybackInternal: CurrentSong, Duration, Position reset.");
        }
        else
        {
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

        // Clamp targetPosition to valid range within the audio file.
        // Prevent seeking too close to the very end to avoid issues with NAudio or file formats.
        var maxSeekablePosition = audioFileReader.TotalTime - TimeSpan.FromMilliseconds(100);
        if (maxSeekablePosition < TimeSpan.Zero) maxSeekablePosition = TimeSpan.Zero; // Handle very short files
        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, maxSeekablePosition.TotalSeconds));


        Debug.WriteLine($"[PlaybackService] Seeking to: {targetPosition}. Current AFR Time: {audioFileReader.CurrentTime}");
        audioFileReader.CurrentTime = targetPosition; // Actual engine seek

        // Update our CurrentPosition property to reflect the new position and notify listeners.
        this.CurrentPosition = audioFileReader.CurrentTime;
        Debug.WriteLine($"[PlaybackService] Seek completed. New Position: {this.CurrentPosition}, New AFR Time: {audioFileReader.CurrentTime}");
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