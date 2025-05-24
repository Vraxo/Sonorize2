using Avalonia.Threading;
using NAudio.Wave;
using Sonorize.Models;
using Sonorize.ViewModels;
using System.Diagnostics;
using System.Threading;
using System;
using Avalonia.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Sonorize.Models;
using Sonorize.ViewModels;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Avalonia.Animation;

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
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                IsPlaying = false;
                CurrentPosition = TimeSpan.Zero;
                CurrentSongDuration = TimeSpan.Zero;
                StopUiUpdateTimer();
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
        private set
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

    private NAudioPlaybackEngine? _playbackEngine;
    private Timer? uiUpdateTimer;

    private volatile bool _explicitStopRequested = false;

    private float _playbackRate = 1.0f;
    public float PlaybackRate
    {
        get => _playbackRate;
        set
        {
            if (SetProperty(ref _playbackRate, value))
            {
                if (_playbackEngine != null) _playbackEngine.PlaybackRate = value;
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
                if (_playbackEngine != null) _playbackEngine.PitchSemitones = value;
            }
        }
    }

    public event EventHandler? PlaybackEndedNaturally;

    public PlaybackService()
    {
        Debug.WriteLine("[PlaybackService] Constructor called.");
        uiUpdateTimer = new Timer(UpdateUiCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    private void UpdateUiCallback(object? state)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_playbackEngine != null && _playbackEngine.CurrentPlaybackStatus == PlaybackStateStatus.Playing && CurrentSong != null)
            {
                TimeSpan currentAudioTime = TimeSpan.Zero;
                try
                {
                    currentAudioTime = _playbackEngine.CurrentPosition;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PlaybackService] Error getting Engine.CurrentPosition in timer callback: {ex.Message}. Stopping timer.");
                    StopUiUpdateTimer();
                    return;
                }

                this.CurrentPosition = currentAudioTime;

                if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
                {
                    var loop = CurrentSong.SavedLoop;
                    TimeSpan songDuration = _playbackEngine.CurrentSongDuration;

                    if (loop.End > loop.Start && loop.End <= songDuration)
                    {
                        TimeSpan seekThreshold = loop.End - TimeSpan.FromMilliseconds(50);
                        if (currentAudioTime >= seekThreshold && currentAudioTime < songDuration - TimeSpan.FromMilliseconds(200))
                        {
                            Debug.WriteLine($"[PlaybackService] Loop active & end reached ({currentAudioTime:mm\\:ss\\.ff} >= {seekThreshold:mm\\:ss\\.ff}) within file ({songDuration:mm\\:ss\\.ff}). Seeking to loop start: {loop.Start:mm\\:ss\\.ff}");
                            Seek(loop.Start);
                        }
                    }
                    else if (CurrentSong.IsLoopActive)
                    {
                        Debug.WriteLine($"[PlaybackService] Loop active but invalid region ({loop.Start:mm\\:ss\\.ff} - {loop.End:mm\\:ss\\.ff}). Loop will not function.");
                    }
                }
            }
            else
            {
                if (_playbackEngine == null || _playbackEngine.CurrentPlaybackStatus != PlaybackStateStatus.Playing)
                {
                    Debug.WriteLine($"[PlaybackService] Timer callback found engine state is not Playing ({_playbackEngine?.CurrentPlaybackStatus}). Stopping timer.");
                    StopUiUpdateTimer();
                }
            }
        });
    }

    public void Play(Song song)
    {
        Debug.WriteLine($"[PlaybackService] Play requested for: {(song?.Title ?? "null song")}");

        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[PlaybackService] Play called with null/invalid/missing file song. Stopping.");
            Stop();
            return;
        }

        if (_playbackEngine != null)
        {
            _playbackEngine.Stop();
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
            Debug.WriteLine("[PlaybackService] Disposed existing playback engine.");
        }

        CurrentSong = song;
        _explicitStopRequested = false;

        try
        {
            _playbackEngine = new NAudioPlaybackEngine();
            _playbackEngine.PlaybackStopped += OnEnginePlaybackStopped;

            _playbackEngine.PlaybackRate = PlaybackRate;
            _playbackEngine.PitchSemitones = PitchSemitones;

            _playbackEngine.Load(song.FilePath);

            CurrentSongDuration = _playbackEngine.CurrentSongDuration;
            this.CurrentPosition = TimeSpan.Zero;

            if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null && CurrentSong.SavedLoop.Start >= TimeSpan.Zero && CurrentSong.SavedLoop.Start < _playbackEngine.CurrentSongDuration)
            {
                Debug.WriteLine($"[PlaybackService] New song has active loop. Seeking to loop start: {CurrentSong.SavedLoop.Start:mm\\:ss\\.ff} before playing.");
                _playbackEngine.Seek(CurrentSong.SavedLoop.Start);
            }
            else if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
            {
                Debug.WriteLine($"[PlaybackService] New song has active loop, but loop start invalid ({CurrentSong.SavedLoop.Start >= _playbackEngine.CurrentSongDuration}). Starting from beginning.");
            }
            else
            {
                Debug.WriteLine("[PlaybackService] New song has no active loop. Starting from beginning.");
            }


            _playbackEngine.Play();

            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
            Debug.WriteLine($"[PlaybackService] Playback started for: {CurrentSong.Title}. State: {CurrentPlaybackStatus}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL ERROR during playback initiation for {Path.GetFileName(song.FilePath)}: {ex.ToString()}");
            Stop();
        }
    }

    public void Pause()
    {
        Debug.WriteLine($"[PlaybackService] Pause requested. Current state: {CurrentPlaybackStatus}");
        if (_playbackEngine != null && IsPlaying)
        {
            _playbackEngine.Pause();
            IsPlaying = false;
            CurrentPlaybackStatus = PlaybackStateStatus.Paused;
            StopUiUpdateTimer();
        }
    }

    public void Resume()
    {
        Debug.WriteLine($"[PlaybackService] Resume requested. Current Status: {CurrentPlaybackStatus}, HasSong: {HasCurrentSong}");

        if (CurrentSong == null)
        {
            Debug.WriteLine("[PlaybackService] Resume requested but no CurrentSong is set. Cannot resume.");
            return;
        }

        if (_playbackEngine != null && CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Paused state. Resuming engine.");
            _playbackEngine.Play();
            IsPlaying = true;
            CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            StartUiUpdateTimer();
        }
        else if (_playbackEngine != null && CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            Debug.WriteLine("[PlaybackService] Resume requested from Stopped state. Re-playing current song via engine.");
            Play(CurrentSong);
        }
        else
        {
            Debug.WriteLine($"[PlaybackService] Resume requested but conditions not met. Current Status: {CurrentPlaybackStatus}. Doing nothing.");
        }
    }

    public void Stop()
    {
        Debug.WriteLine("[PlaybackService] Public Stop() called.");
        _explicitStopRequested = true;
        if (_playbackEngine != null)
        {
            _playbackEngine.Stop();
        }
    }

    public void Seek(TimeSpan requestedPosition)
    {
        if (_playbackEngine == null || CurrentSong == null)
        {
            Debug.WriteLine($"[PlaybackService] Seek ignored: No active engine or current song. Engine: {_playbackEngine != null}, Song: {CurrentSong != null}");
            return;
        }

        TimeSpan targetPosition = requestedPosition;
        TimeSpan songDuration = _playbackEngine.CurrentSongDuration;

        if (CurrentSong.IsLoopActive && CurrentSong.SavedLoop != null)
        {
            var loop = CurrentSong.SavedLoop;
            if (loop.End > loop.Start && loop.End <= songDuration)
            {
                if (targetPosition < loop.Start || targetPosition >= loop.End)
                {
                    Debug.WriteLine($"[PlaybackService] Seek: Loop active, target {targetPosition:mm\\:ss\\.ff} is outside loop [{loop.Start:mm\\:ss\\.ff}-{loop.End:mm\\:ss\\.ff}). Snapping to loop start: {loop.Start:mm\\:ss\\.ff}.");
                    targetPosition = loop.Start;
                }
            }
            else if (CurrentSong.IsLoopActive)
            {
                Debug.WriteLine($"[PlaybackService] Seek: Loop active but invalid region ({loop.Start:mm\\:ss\\.ff} - {loop.End:mm\\:ss\\.ff}). Not applying loop seek constraints.");
            }
        }

        var totalMs = songDuration.TotalMilliseconds;
        var seekMarginMs = totalMs > 200 ? 100 : (totalMs > 0 ? Math.Min(totalMs / 2, 50) : 0);
        var maxSeekablePosition = TimeSpan.FromMilliseconds(totalMs - seekMarginMs);
        if (maxSeekablePosition < TimeSpan.Zero) maxSeekablePosition = TimeSpan.Zero;

        targetPosition = TimeSpan.FromSeconds(Math.Clamp(targetPosition.TotalSeconds, 0, maxSeekablePosition.TotalSeconds));

        double positionToleranceSeconds = 0.3;

        try
        {
            TimeSpan currentAudioTimeForToleranceCheck = _playbackEngine.CurrentPosition;

            if (Math.Abs(currentAudioTimeForToleranceCheck.TotalSeconds - targetPosition.TotalSeconds) < positionToleranceSeconds)
            {
                Debug.WriteLine($"[PlaybackService] Seek target {targetPosition:mm\\:ss\\.ff} is very close to current position {currentAudioTimeForToleranceCheck:mm\\:ss\\.ff} (within {positionToleranceSeconds}s), ignoring seek.");
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] Error checking current position for seek tolerance: {ex.Message}. Proceeding with seek.");
        }

        Debug.WriteLine($"[PlaybackService] Seeking engine to: {targetPosition:mm\\:ss\\.ff}");
        try
        {
            _playbackEngine.Seek(targetPosition);
            this.CurrentPosition = _playbackEngine.CurrentPosition;
            Debug.WriteLine($"[PlaybackService] Seek executed. Engine Time after seek: {_playbackEngine.CurrentPosition:mm\\:ss\\.ff}. VM Position: {this.CurrentPosition:mm\\:ss\\.ff}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackService] CRITICAL Error during Engine Seek to {targetPosition:mm\\:ss\\.ff}: {ex.Message}");
            if (_playbackEngine != null)
            {
                try { this.CurrentPosition = _playbackEngine.CurrentPosition; }
                catch (Exception readEx) { Debug.WriteLine($"[PlaybackService] Error reading position after failed seek: {readEx.Message}"); }
            }
            else
            {
                this.CurrentPosition = TimeSpan.Zero;
            }
        }
    }

    private void OnEnginePlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Debug.WriteLine($"[PlaybackService] === OnEnginePlaybackStopped START === (UI Thread)");
            Debug.WriteLine($"[PlaybackService] Exception: {e.Exception?.Message ?? "None"}");
            Debug.WriteLine($"[PlaybackService] _explicitStopRequested: {_explicitStopRequested}");

            StopUiUpdateTimer();

            if (e.Exception != null)
            {
                Debug.WriteLine($"[PlaybackService] Playback stopped due to error: {e.Exception.Message}. Finalizing state to Stopped.");
                IsPlaying = false;
                CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                this.CurrentPosition = TimeSpan.Zero;
                CurrentSongDuration = TimeSpan.Zero;
                CurrentSong = null;
                _explicitStopRequested = false;
                Debug.WriteLine("[PlaybackService] State set to Stopped by error handler.");
            }
            else
            {
                bool isNearEndOfFile = CurrentSongDuration.TotalSeconds > 0 && (_playbackEngine?.CurrentPosition ?? TimeSpan.Zero) >= CurrentSongDuration - TimeSpan.FromMilliseconds(200);
                Debug.WriteLine($"[PlaybackService] Clean Stop. Was Explicit Stop: {_explicitStopRequested}. Is Near End of File: {isNearEndOfFile}.");


                if (_explicitStopRequested)
                {
                    Debug.WriteLine("[PlaybackService] Playback stopped by explicit user command (event). Finalizing state to Stopped.");
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    this.CurrentPosition = TimeSpan.Zero;
                    CurrentSongDuration = TimeSpan.Zero;
                    CurrentSong = null;
                    _explicitStopRequested = false;
                    Debug.WriteLine("[PlaybackService] State set to Stopped by explicit stop handler.");
                }
                else if (isNearEndOfFile)
                {
                    Debug.WriteLine("[PlaybackService] Playback stopped naturally (event). Raising PlaybackEndedNaturally event.");
                    this.CurrentPosition = TimeSpan.Zero;
                    IsPlaying = false;
                    CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                    PlaybackEndedNaturally?.Invoke(this, EventArgs.Empty);
                    Debug.WriteLine("[PlaybackService] State set to Stopped by natural end handler, event raised.");
                }
                else
                {
                    Debug.WriteLine("Playback stopped by interruption (likely Play called). State managed by calling Play() method.");
                }
            }
            Debug.WriteLine("[PlaybackService] OnEnginePlaybackStopped handler finishes.");
        });
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

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackService] Dispose() called.");
        uiUpdateTimer?.Dispose();
        uiUpdateTimer = null;

        if (_playbackEngine != null)
        {
            _playbackEngine.PlaybackStopped -= OnEnginePlaybackStopped;
            _playbackEngine.Dispose();
            _playbackEngine = null;
            Debug.WriteLine("[PlaybackService] Disposed playback engine during service dispose.");
        }

        _explicitStopRequested = false;
        CurrentSong = null;
        CurrentSongDuration = TimeSpan.Zero;
        this.CurrentPosition = TimeSpan.Zero;
        IsPlaying = false;
        CurrentPlaybackStatus = PlaybackStateStatus.Stopped;

        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackService] Dispose() completed.");
    }

    ~PlaybackService()
    {
        Debug.WriteLine("[PlaybackService] Finalizer called for PlaybackService.");
        Dispose();
        Debug.WriteLine("[PlaybackService] Finalizer completed for PlaybackService.");
    }
}