using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
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

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (SetProperty(ref _playbackRate, value))
            {
                if (IsPlaying && CurrentSong != null)
                {
                    var resumeTime = audioFileReader?.CurrentTime ?? TimeSpan.Zero;
                    Play(CurrentSong);
                    Seek(resumeTime);
                }
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

                CurrentPosition = audioFileReader.CurrentTime;

                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    var actualPlaybackTimeInFile = audioFileReader.CurrentTime;
                    if (actualPlaybackTimeInFile >= activeLoop.End && activeLoop.End > activeLoop.Start)
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
        CleanUpPlaybackResources();

        audioFileReader = new AudioFileReader(filePath);

        ISampleProvider speedAdjustedProvider = AdjustSpeed(audioFileReader, PlaybackRate);

        pitchShifter = new SmbPitchShiftingSampleProvider(speedAdjustedProvider);
        pitchShifter.PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0);

        finalSampleProvider = pitchShifter;

        waveOutDevice = new WaveOutEvent();
        waveOutDevice.PlaybackStopped += OnPlaybackStopped;
        waveOutDevice.Init(finalSampleProvider);

        CurrentSongDuration = audioFileReader.TotalTime;
        CurrentPosition = TimeSpan.Zero;
    }

    private ISampleProvider AdjustSpeed(AudioFileReader reader, float speed)
    {
        if (speed == 1.0f)
            return reader.ToSampleProvider();

        int newSampleRate = (int)(reader.WaveFormat.SampleRate * speed);
        var outFormat = new WaveFormat(newSampleRate, reader.WaveFormat.Channels);

        var resampler = new MediaFoundationResampler(reader, outFormat)
        {
            ResamplerQuality = 60
        };

        return resampler.ToSampleProvider();
    }

    public void Play(Song song)
    {
        if (song == null) return;
        CurrentSong = song;

        try
        {
            InitializeNAudioPipeline(song.FilePath);

            waveOutDevice?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing playback for {song.FilePath}: {ex.Message}");
            IsPlaying = false;
            CurrentSong = null;
            CurrentSongDuration = TimeSpan.Zero;
            CurrentPosition = TimeSpan.Zero;
            CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            CleanUpPlaybackResources();
        }
    }

    private void StartUiUpdateTimer() => uiUpdateTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    private void StopUiUpdateTimer() => uiUpdateTimer?.Change(Timeout.Infinite, Timeout.Infinite);

    private void CleanUpPlaybackResources()
    {
        StopUiUpdateTimer();

        waveOutDevice?.Stop();
        waveOutDevice?.Dispose();
        waveOutDevice = null;

        pitchShifter = null;
        finalSampleProvider = null;
        audioFileReader?.Dispose();
        audioFileReader = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (IsPlaying || (waveOutDevice != null && waveOutDevice.PlaybackState == PlaybackState.Stopped))
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            }
            StopUiUpdateTimer();
            if (e.Exception != null)
            {
                Console.WriteLine($"NAudio Playback Error: {e.Exception.Message}");
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
            if (waveOutDevice == null || audioFileReader == null || waveOutDevice.PlaybackState == PlaybackState.Stopped)
            {
                TimeSpan resumePosition = TimeSpan.Zero;
                if (audioFileReader != null)
                {
                    resumePosition = audioFileReader.CurrentTime;
                }
                else if (CurrentPosition > TimeSpan.Zero)
                {
                    resumePosition = CurrentPosition;
                }

                try
                {
                    InitializeNAudioPipeline(CurrentSong.FilePath);
                    if (audioFileReader != null)
                    {
                        audioFileReader.CurrentTime = resumePosition;
                        CurrentPosition = resumePosition;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error re-initializing for resume: {ex.Message}");
                    CleanUpPlaybackResources();
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
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
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        waveOutDevice?.Stop();
        CleanUpPlaybackResources();
        CurrentPosition = TimeSpan.Zero;
    }

    public void Seek(TimeSpan positionInTrueTime)
    {
        if (audioFileReader != null)
        {
            var targetPosition = positionInTrueTime;
            if (targetPosition < TimeSpan.Zero) targetPosition = TimeSpan.Zero;
            if (targetPosition > audioFileReader.TotalTime) targetPosition = audioFileReader.TotalTime;

            audioFileReader.CurrentTime = targetPosition;
            CurrentPosition = targetPosition;
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
