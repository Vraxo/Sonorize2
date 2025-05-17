using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Threading;

namespace Sonorize.Services;

// file-scoped namespace
public enum PlaybackStateStatus { Stopped, Playing, Paused }

public class PlaybackService : ViewModelBase, IDisposable
{
    // ── Public API ───────────────────────────────────────────────────────────────

    private Song? _currentSong;
    public Song? CurrentSong
    {
        get => _currentSong;
        private set { SetProperty(ref _currentSong, value); OnPropertyChanged(nameof(HasCurrentSong)); }
    }
    public bool HasCurrentSong => CurrentSong != null;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    private PlaybackStateStatus _currentStatus = PlaybackStateStatus.Stopped;
    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get => _currentStatus;
        private set => SetProperty(ref _currentStatus, value);
    }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition
    {
        get => _currentPosition;
        set { if (SetProperty(ref _currentPosition, value)) OnPropertyChanged(nameof(CurrentPositionSeconds)); }
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

    private TimeSpan _songDuration;
    public TimeSpan CurrentSongDuration
    {
        get => _songDuration;
        private set { if (SetProperty(ref _songDuration, value)) OnPropertyChanged(nameof(CurrentSongDurationSeconds)); }
    }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0
        ? CurrentSongDuration.TotalSeconds
        : 1;

    private float _playbackRate = 1.0f;
    /// <summary>
    /// 0.5→2.0 typical range; changes time-stretch speed in real time.
    /// </summary>
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (SetProperty(ref _playbackRate, value))
            {
                if (isPipelineInitialized)
                    varispeed!.PlaybackRate = value;
                UpdateDurationForRate();
            }
        }
    }

    private float _pitchSemitones = 0f;
    /// <summary>
    /// -12→+12 typical; changes pitch independently.
    /// </summary>
    public float PitchSemitones
    {
        get => _pitchSemitones;
        set
        {
            if (SetProperty(ref _pitchSemitones, value) && pitchShifter != null)
                pitchShifter.PitchFactor = (float)Math.Pow(2, value / 12.0);
        }
    }

    public PlaybackService()
    {
        uiTimer = new Timer(UiTimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Play(Song song)
    {
        if (song == null) return;
        CurrentSong = song;
        InitializePipeline(song.FilePath);
        waveOut!.Play();
        IsPlaying = true;
        CurrentPlaybackStatus = PlaybackStateStatus.Playing;
        uiTimer!.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
    }

    public void Pause()
    {
        if (IsPlaying && waveOut?.PlaybackState == PlaybackState.Playing)
        {
            waveOut.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            uiTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public void Resume()
    {
        if (!IsPlaying && audioFileReader != null)
        {
            waveOut?.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            uiTimer?.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        }
    }

    public void Stop()
    {
        waveOut?.Stop();
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        uiTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        CleanUp();
        CurrentPosition = TimeSpan.Zero;
    }

    public void Seek(TimeSpan position)
    {
        if (audioFileReader != null)
        {
            position = position < TimeSpan.Zero ? TimeSpan.Zero
                      : position > audioFileReader.TotalTime ? audioFileReader.TotalTime
                      : position;
            audioFileReader.CurrentTime = position;
            CurrentPosition = position;
        }
    }

    public void Dispose()
    {
        CleanUp();
        uiTimer?.Dispose();
        GC.SuppressFinalize(this);
    }

    // ── Internals ────────────────────────────────────────────────────────────────

    private IWavePlayer? waveOut;
    private AudioFileReader? audioFileReader;
    private VarispeedSampleProvider? varispeed;
    private SmbPitchShiftingSampleProvider? pitchShifter;
    private Timer? uiTimer;
    private bool isPipelineInitialized;

    private void InitializePipeline(string filePath)
    {
        CleanUp();

        // 1) core reader
        audioFileReader = new AudioFileReader(filePath);

        // 2) varispeed time-stretch provider
        varispeed = new VarispeedSampleProvider(
            source: audioFileReader.ToSampleProvider(),
            fftLength: 1024,
            new SoundTouchProfile(matchTempo: true, matchPitch: true))
        {
            PlaybackRate = PlaybackRate
        };

        // 3) pitch shift
        pitchShifter = new SmbPitchShiftingSampleProvider(varispeed)
        {
            PitchFactor = (float)Math.Pow(2, PitchSemitones / 12.0)
        };

        // 4) hook up output
        waveOut = new WaveOutEvent();
        waveOut.PlaybackStopped += (s, e) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                uiTimer?.Change(Timeout.Infinite, Timeout.Infinite);
            });
        };
        waveOut.Init(pitchShifter);

        isPipelineInitialized = true;
        UpdateDurationForRate();
        CurrentPosition = TimeSpan.Zero;
    }

    private void CleanUp()
    {
        isPipelineInitialized = false;
        waveOut?.Stop();
        waveOut?.Dispose();
        waveOut = null;
        audioFileReader?.Dispose();
        audioFileReader = null;
        varispeed = null;
        pitchShifter = null;
    }

    private void UiTimerCallback(object? _)
    {
        if (IsPlaying && audioFileReader != null && waveOut?.PlaybackState == PlaybackState.Playing)
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CurrentPosition = audioFileReader.CurrentTime;
                // loop logic omitted for brevity…
            });
        }
    }

    private void UpdateDurationForRate()
    {
        if (audioFileReader != null)
            CurrentSongDuration = TimeSpan.FromSeconds(
                audioFileReader.TotalTime.TotalSeconds / PlaybackRate);
    }
}
