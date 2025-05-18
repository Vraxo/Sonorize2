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
using SoundTouch.Net.NAudioSupport;

namespace Sonorize.Services;

public enum PlaybackStateStatus { Stopped, Playing, Paused }

public class PlaybackService : ViewModelBase, IDisposable
{
    private Song? _currentSong;
    public Song? CurrentSong // ViewModel will observe this
    {
        get => _currentSong;
        private set
        {
            if (SetProperty(ref _currentSong, value))
            {
                Debug.WriteLine($"[PlaybackService] CurrentSong property set to: {value?.Title ?? "null"}");
                // ViewModel will react to this PropertyChanged event
            }
        }
    }

    // No HasCurrentSong property here

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
            value = Math.Clamp(value, 0.5f, 2.0f);
            if (SetProperty(ref _playbackRate, value) && soundTouch != null)
            {
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
            if (SetProperty(ref _pitchSemitones, value) && pitchShifter != null)
            {
                pitchShifter.PitchFactor = (float)Math.Pow(2, value / 12.0);
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
                if (audioFileReader == null || _waveOutDevice == null) return;

                CurrentPosition = audioFileReader.CurrentTime;

                if (CurrentSong?.SavedLoop != null)
                {
                    var loop = CurrentSong.SavedLoop;
                    if (CurrentPosition >= loop.End && loop.End > loop.Start)
                    {
                        Seek(loop.Start);
                    }
                }
            });
        }
    }

    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService] Play requested for: {(song?.Title ?? "null song")}");
        StopPlaybackInternal(resetCurrentSong: false);

        if (song == null || string.IsNullOrEmpty(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] Play rejected: Song or FilePath is null/empty. CurrentSong cleared.");
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            return;
        }

        CurrentSong = song; // Set the new song; PropertyChanged will fire

        bool pipelineInitialized = InitializeNAudioPipeline(song.FilePath);

        if (pipelineInitialized && _waveOutDevice != null && audioFileReader != null)
        {
            _waveOutDevice.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Playback not started for {song.Title} due to NAudio pipeline initialization failure.");
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            StopUiUpdateTimer();
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            // CurrentSong is already set, or was null if song parameter was invalid.
            // If pipeline init failed, CurrentSong will be non-null but playback won't start.
            // The ViewModel needs to handle this state (e.g. show an error or just no playback).
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
            CurrentPosition = TimeSpan.Zero;
            Debug.WriteLine($"[PlaybackService] NAudio pipeline initialization COMPLETE for: {Path.GetFileName(filePath)}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during NAudio pipeline init for {Path.GetFileName(filePath)}: {ex.ToString()}");
            CleanUpNAudioResources();
            return false;
        }
    }

    private void StartUiUpdateTimer()
    {
        uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    private void StopUiUpdateTimer()
    {
        uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    private void CleanUpNAudioResources()
    {
        Debug.WriteLine("[PlaybackService] CleanUpNAudioResources called.");
        if (_waveOutDevice != null)
        {
            _waveOutDevice.PlaybackStopped -= OnPlaybackStopped;
            _waveOutDevice.Stop();
            _waveOutDevice.Dispose();
            _waveOutDevice = null;
            _waveOutDeviceInstanceForStopEventCheck = null;
        }
        pitchShifter = null;
        soundTouch = null;
        if (audioFileReader != null)
        {
            audioFileReader.Dispose();
            audioFileReader = null;
        }
        Debug.WriteLine("[PlaybackService] CleanUpNAudioResources finished.");
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (sender != _waveOutDeviceInstanceForStopEventCheck && _waveOutDeviceInstanceForStopEventCheck != null)
        {
            Debug.WriteLine($"[PlaybackService] OnPlaybackStopped event from an old device instance. IGNORING.");
            return;
        }
        Debug.WriteLine($"[PlaybackService] OnPlaybackStopped event. Exception: {e.Exception?.Message ?? "None"}");
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            StopUiUpdateTimer();
            if (audioFileReader != null && audioFileReader.CurrentTime >= audioFileReader.TotalTime && e.Exception == null)
            {
                CurrentPosition = CurrentSongDuration;
            }
            else if (e.Exception != null)
            {
                CurrentPosition = TimeSpan.Zero;
            }
        });
    }

    public void Pause()
    {
        if (IsPlaying && _waveOutDevice?.PlaybackState == PlaybackState.Playing)
        {
            _waveOutDevice.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer();
        }
    }

    public void Resume()
    {
        if (!IsPlaying && CurrentSong != null && CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            if (_waveOutDevice != null && audioFileReader != null && _waveOutDevice.PlaybackState == PlaybackState.Paused)
            {
                _waveOutDevice.Play();
                IsPlaying = true;
                CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                StartUiUpdateTimer();
            }
            else
            {
                Play(CurrentSong); // Re-initialize and play if state is not as expected
            }
        }
    }

    private void StopPlaybackInternal(bool resetCurrentSong = true)
    {
        Debug.WriteLine($"[PlaybackService] StopPlaybackInternal called. resetCurrentSong: {resetCurrentSong}");
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        StopUiUpdateTimer();
        CleanUpNAudioResources();
        CurrentPosition = TimeSpan.Zero;
        if (resetCurrentSong)
        {
            CurrentSong = null; // This will fire PropertyChanged for CurrentSong
            CurrentSongDuration = TimeSpan.Zero;
        }
        Debug.WriteLine($"[PlaybackService] StopPlaybackInternal finished.");
    }

    public void Stop()
    {
        StopPlaybackInternal(resetCurrentSong: true);
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (audioFileReader != null && _waveOutDevice != null)
        {
            var targetPosition = TimeSpan.FromSeconds(Math.Clamp(positionInTrueTime.TotalSeconds, 0, audioFileReader.TotalTime.TotalSeconds));
            audioFileReader.CurrentTime = targetPosition;
            CurrentPosition = audioFileReader.CurrentTime;
        }
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose called.");
        StopPlaybackInternal(resetCurrentSong: true);
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackService] Dispose finished.");
    }
}