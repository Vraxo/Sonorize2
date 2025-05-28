using System;
using System.Diagnostics;
using System.Threading;
using Avalonia.Threading;
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackMonitor : IDisposable
{
    private readonly NAudioEngineController _engineController;
    private readonly PlaybackLoopHandler _loopHandler;
    private Timer? _monitorTimer;
    private Song? _songBeingMonitored;
    private Action<TimeSpan, TimeSpan>? _positionUpdateAction;

    private const int MonitorIntervalMilliseconds = 100;

    public PlaybackMonitor(NAudioEngineController engineController, PlaybackLoopHandler loopHandler)
    {
        _engineController = engineController ?? throw new ArgumentNullException(nameof(engineController));
        _loopHandler = loopHandler ?? throw new ArgumentNullException(nameof(loopHandler));
        Debug.WriteLine("[PlaybackMonitor] Initialized.");
    }

    public void Start(Song? songToMonitor, Action<TimeSpan, TimeSpan> positionUpdateAction)
    {
        Stop();

        _songBeingMonitored = songToMonitor;
        _positionUpdateAction = positionUpdateAction ?? throw new ArgumentNullException(nameof(positionUpdateAction));

        if (_songBeingMonitored is null)
        {
            Debug.WriteLine("[PlaybackMonitor] Start called, but songToMonitor is null. Monitoring will not proceed effectively.");
            // It might still run the timer but the callback will likely stop it.
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
        _songBeingMonitored = null; // Clear the song being monitored
        _positionUpdateAction = null; // Clear the callback
    }

    private void MonitorCallback(object? state)
    {
        Song? localSongBeingMonitored = _songBeingMonitored; // Capture for thread safety
        Action<TimeSpan, TimeSpan>? localPositionUpdateAction = _positionUpdateAction; // Capture for thread safety

        if (localSongBeingMonitored is null || localPositionUpdateAction is null)
        {
            Debug.WriteLine("[PlaybackMonitor Callback] Song or position update action is null. Stopping timer.");
            Dispatcher.UIThread.InvokeAsync(Stop); // Stop on UI thread to ensure proper disposal if needed
            return;
        }

        // This check should be done on the UI thread if it involves UI-bound properties like PlaybackService.CurrentSong
        // However, here we are just checking against the _songBeingMonitored which was set at Start()
        // The critical part is that the PlaybackEngineController is still for this song.
        // PlaybackService's CurrentSong might change, causing this monitor to be stopped externally.

        if (_engineController.CurrentPlaybackStatus != PlaybackStateStatus.Playing)
        {
            // If not playing, stop the monitor.
            Debug.WriteLine($"[PlaybackMonitor Callback] Engine not playing (State: {_engineController.CurrentPlaybackStatus}). Stopping monitoring for '{localSongBeingMonitored.Title}'.");
            Dispatcher.UIThread.InvokeAsync(Stop);
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Re-check conditions on UI thread before acting, in case state changed during invoke
            if (_songBeingMonitored != localSongBeingMonitored || _positionUpdateAction != localPositionUpdateAction || _engineController.CurrentPlaybackStatus != PlaybackStateStatus.Playing)
            {
                // If the song being monitored has changed since the callback was scheduled, or monitor was stopped.
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
                Debug.WriteLine($"[PlaybackMonitor] Error getting EngineController.CurrentPosition/Duration in timer callback for '{localSongBeingMonitored.Title}': {ex.Message}. Stopping timer.");
                Stop(); // Stop self
                return;
            }

            localPositionUpdateAction(currentAudioTime, songDuration);
            _loopHandler.CheckForLoopSeek(currentAudioTime, songDuration);
        });
    }

    public void Dispose()
    {
        Debug.WriteLine("[PlaybackMonitor] Dispose called.");
        Stop(); // Ensure timer is stopped and disposed
        GC.SuppressFinalize(this);
        Debug.WriteLine("[PlaybackMonitor] Dispose finished.");
    }

    ~PlaybackMonitor()
    {
        Debug.WriteLine("[PlaybackMonitor] Finalizer called.");
        Dispose(); // Call the same dispose logic
    }
}