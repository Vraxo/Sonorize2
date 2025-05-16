using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Threading;
using System.Diagnostics; // Added for Debug.WriteLineIf

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
    public PlaybackStateStatus CurrentPlaybackStatus
    {
        get => _currentPlaybackStatus;
        private set => SetProperty(ref _currentPlaybackStatus, value);
    }

    private TimeSpan _currentPosition;
    public TimeSpan CurrentPosition { get => _currentPosition; set { if (SetProperty(ref _currentPosition, value)) OnPropertyChanged(nameof(CurrentPositionSeconds)); } }
    public double CurrentPositionSeconds { get => CurrentPosition.TotalSeconds; set { if (audioFileReader != null && Math.Abs(CurrentPosition.TotalSeconds - value) > 0.1) Seek(TimeSpan.FromSeconds(value)); } }

    private TimeSpan _currentSongDuration;
    public TimeSpan CurrentSongDuration { get => _currentSongDuration; private set { if (SetProperty(ref _currentSongDuration, value)) OnPropertyChanged(nameof(CurrentSongDurationSeconds)); } }
    public double CurrentSongDurationSeconds => CurrentSongDuration.TotalSeconds > 0 ? CurrentSongDuration.TotalSeconds : 1;

    private IWavePlayer? waveOutDevice;
    private AudioFileReader? audioFileReader;
    // private VarispeedSampleProvider? varispeedProvider; // Removed: VarispeedSampleProvider is not available
    private Timer? uiUpdateTimer;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (SetProperty(ref _playbackRate, value))
            {
                // VarispeedSampleProvider is removed, so this rate is not applied to audio.
                // The property behaves as a simple store for the UI value.
                // Audio will always play at 1.0x speed if VarispeedSampleProvider is not used.
                Debug.WriteLineIf(_playbackRate != 1.0f, "PlaybackService: PlaybackRate set, but VarispeedSampleProvider is not used. Audio will play at 1.0x speed.");
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
                // VarispeedSampleProvider is removed, so pitch is not applied to audio.
                Debug.WriteLineIf(_pitchSemitones != 0.0f, "PlaybackService: PitchSemitones set, but VarispeedSampleProvider is not used. Audio pitch will not change.");
            }
        }
    }

    public PlaybackService()
    {
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateUiCallback(object? state)
    {
        if (IsPlaying && audioFileReader != null && waveOutDevice?.PlaybackState == NAudio.Wave.PlaybackState.Playing)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (audioFileReader == null) return; // VarispeedProvider removed

                // CurrentPosition is true file time as audio plays at 1.0x speed
                CurrentPosition = audioFileReader.CurrentTime;

                if (CurrentSong?.ActiveLoop != null)
                {
                    var activeLoop = CurrentSong.ActiveLoop;
                    var actualPlaybackTimeInFile = audioFileReader.CurrentTime;
                    if (actualPlaybackTimeInFile >= activeLoop.End && activeLoop.End > activeLoop.Start)
                    {
                        if (audioFileReader != null && CurrentSong?.ActiveLoop != null)
                        {
                            audioFileReader.CurrentTime = CurrentSong.ActiveLoop.Start;
                            // CurrentPosition updated to reflect loop jump, at 1.0x speed
                            CurrentPosition = CurrentSong.ActiveLoop.Start;
                        }
                    }
                }
            });
        }
    }

    private void InitializeNAudioPipeline(string filePath)
    {
        CleanUpPlaybackResources();

        audioFileReader = new AudioFileReader(filePath);
        // VarispeedSampleProvider related code removed
        // varispeedProvider = new VarispeedSampleProvider(audioFileReader, 100, new SoundTouchProfile(true, false, false));
        // varispeedProvider.PlaybackRate = _playbackRate;

        waveOutDevice = new WaveOutEvent();
        waveOutDevice.PlaybackStopped += OnPlaybackStopped;
        waveOutDevice.Init(audioFileReader); // Init with audioFileReader directly

        CurrentSongDuration = audioFileReader.TotalTime;
        CurrentPosition = TimeSpan.Zero;
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
            IsPlaying = false; CurrentSong = null; CurrentSongDuration = TimeSpan.Zero; CurrentPosition = TimeSpan.Zero;
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

        // varispeedProvider = null; // Removed
        audioFileReader?.Dispose();
        audioFileReader = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (IsPlaying || (waveOutDevice != null && waveOutDevice.PlaybackState == NAudio.Wave.PlaybackState.Stopped))
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
        if (!IsPlaying && CurrentSong != null)
        {
            // If VarispeedSampleProvider is not used, audioFileReader state is primary
            if (waveOutDevice == null || audioFileReader == null || waveOutDevice.PlaybackState == NAudio.Wave.PlaybackState.Stopped)
            {
                TimeSpan resumePosition = TimeSpan.Zero;
                if (audioFileReader != null) // Should generally be non-null if paused with a song
                {
                    resumePosition = audioFileReader.CurrentTime;
                }
                else if (CurrentPosition > TimeSpan.Zero) // Fallback, assuming _playbackRate was 1.0
                {
                    // If audioFileReader is null, we might have lost exact position.
                    // CurrentPosition is the last known true time.
                    resumePosition = CurrentPosition;
                }


                try
                {
                    InitializeNAudioPipeline(CurrentSong.FilePath);
                    if (audioFileReader != null)
                    {
                        audioFileReader.CurrentTime = resumePosition;
                        // CurrentPosition updated to reflect resume position, at 1.0x speed
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
        if (audioFileReader != null) // VarispeedProvider removed
        {
            var targetPosition = positionInTrueTime;
            if (targetPosition < TimeSpan.Zero) targetPosition = TimeSpan.Zero;
            if (targetPosition > audioFileReader.TotalTime) targetPosition = audioFileReader.TotalTime;

            audioFileReader.CurrentTime = targetPosition;
            // CurrentPosition updated to reflect seek position, at 1.0x speed
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