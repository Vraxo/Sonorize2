using System;
using System.Diagnostics;
using System.Threading;
using Avalonia.Threading;
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackMonitor : IDisposable
{
    private readonly PlaybackService _playbackService;
    private readonly NAudioEngineController _engineController; // Changed to NAudioEngineController
    private Timer? _monitorTimer;
    private PlaybackLoopHandler? _currentTargetLoopHandler;
    private Song? _songBeingMonitored;

    private const int MonitorIntervalMilliseconds = 100;

    public PlaybackMonitor(PlaybackService playbackService, NAudioEngineController engineController)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _engineController = engineController ?? throw new ArgumentNullException(nameof(engineController));
        Debug.WriteLine("[PlaybackMonitor] Initialized.");
    }

    public void Start(PlaybackLoopHandler loopHandler, Song? songToMonitor) // Removed NAudioPlaybackEngine engine
    {
        Stop();

        _currentTargetLoopHandler = loopHandler ?? throw new ArgumentNullException(nameof(loopHandler));
        _songBeingMonitored = songToMonitor;

        if (_songBeingMonitored is null)
        {
            Debug.WriteLine("[PlaybackMonitor] Start called, but songToMonitor is null. Monitoring will not proceed effectively.");
        }

        _monitorTimer = new Timer(MonitorCallback, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(MonitorIntervalMilliseconds));
        Debug.WriteLine($"[PlaybackMonitor] Started monitoring for: {_songBeingMonitored?.Title ?? "No Song"}");
    }

    public void Stop()
    {
        if (_monitorTimer is not null)
        {
            _monitorTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _monitorTimer.Dispose();
            _monitorTimer = null;
            Debug.WriteLine($"[PlaybackMonitor] Stopped monitoring for: {_songBeingMonitored?.Title ?? "Previously Monitored Song"}");
        }

        _currentTargetLoopHandler = null;
        _songBeingMonitored = null;
    }

    private void MonitorCallback(object? state)
    {
        PlaybackLoopHandler? loopHandler = _currentTargetLoopHandler;
        Song? monitoredSong = _songBeingMonitored;

        if (loopHandler is null || monitoredSong is null || _playbackService.CurrentSong != monitoredSong)
        {
            Debug.WriteLineIf(loopHandler is null, "[PlaybackMonitor Callback] LoopHandler is null. Stopping.");
            Debug.WriteLineIf(monitoredSong == null, "[PlaybackMonitor Callback] MonitoredSong is null. Stopping.");
            Debug.WriteLineIf(_playbackService.CurrentSong != monitoredSong && monitoredSong is not null, $"[PlaybackMonitor Callback] Monitored song '{monitoredSong.Title}' differs from PlaybackService.CurrentSong ('{_playbackService.CurrentSong?.Title ?? "null"}'). Stopping.");

            Dispatcher.UIThread.InvokeAsync(Stop);
            return;
        }

        // Use _engineController for playback status
        if (_engineController.CurrentPlaybackStatus != PlaybackStateStatus.Playing)
        {
            Debug.WriteLine($"[PlaybackMonitor Callback] Engine not playing (State: {_engineController.CurrentPlaybackStatus}). Stopping monitoring for '{monitoredSong.Title}'.");
            Dispatcher.UIThread.InvokeAsync(Stop);
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_currentTargetLoopHandler != loopHandler || _playbackService.CurrentSong != monitoredSong || _engineController.CurrentPlaybackStatus != PlaybackStateStatus.Playing)
            {
                return;
            }

            var currentAudioTime = TimeSpan.Zero;
            var songDuration = TimeSpan.Zero;

            try
            {
                currentAudioTime = _engineController.CurrentPosition;
                songDuration = _engineController.CurrentSongDuration;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaybackMonitor] Error getting EngineController.CurrentPosition/Duration in timer callback for '{monitoredSong.Title}': {ex.Message}. Stopping timer.");
                Stop();
                return;
            }

            _playbackService.UpdatePlaybackPositionAndDuration(currentAudioTime, songDuration);
            loopHandler.CheckForLoopSeek(currentAudioTime, songDuration);
        });
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackMonitor] Dispose called.");
        Stop();
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackMonitor] Dispose finished.");
    }

    ~PlaybackMonitor()
    {
        Debug.WriteLine("[PlaybackMonitor] Finalizer called.");
        Dispose();
    }
}