using System;
using System.Diagnostics;
using System.IO;
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackSessionCommandExecutor
{
    private readonly PlaybackEngineCoordinator _playbackEngineCoordinator;
    private readonly PlaybackSessionLoader _sessionLoader;
    private readonly PlaybackSessionState _sessionState;
    private readonly ScrobblingService _scrobblingService;
    private readonly Action<bool> _setExplicitStopRequestedAction;

    public PlaybackSessionCommandExecutor(
        PlaybackEngineCoordinator playbackEngineCoordinator,
        PlaybackSessionLoader sessionLoader,
        PlaybackSessionState sessionState,
        ScrobblingService scrobblingService,
        Action<bool> setExplicitStopRequestedAction)
    {
        _playbackEngineCoordinator = playbackEngineCoordinator ?? throw new ArgumentNullException(nameof(playbackEngineCoordinator));
        _sessionLoader = sessionLoader ?? throw new ArgumentNullException(nameof(sessionLoader));
        _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        _setExplicitStopRequestedAction = setExplicitStopRequestedAction ?? throw new ArgumentNullException(nameof(setExplicitStopRequestedAction));
        Debug.WriteLine("[PlaybackSessionCommandExecutor] Initialized.");
    }

    public bool ExecuteStartNewSession(Song song)
    {
        Debug.WriteLine($"[CommandExecutor] ExecuteStartNewSession for: {(song?.Title ?? "null song")}");

        if (_sessionState.CurrentPlaybackStatus != PlaybackStateStatus.Stopped && _sessionState.CurrentSong != null && _sessionState.CurrentSong != song)
        {
            _playbackEngineCoordinator.Stop(); // Stop previous different song
        }
        else if (_sessionState.CurrentPlaybackStatus != PlaybackStateStatus.Stopped && _sessionState.CurrentSong == song)
        {
            _playbackEngineCoordinator.Stop(); // Stop previous instance of the same song
        }

        if (song == null || string.IsNullOrEmpty(song.FilePath) || !File.Exists(song.FilePath))
        {
            Debug.WriteLine("[CommandExecutor] New song is null or invalid. Stopping current playback if any.");
            if (_sessionState.CurrentPlaybackStatus != PlaybackStateStatus.Stopped) _playbackEngineCoordinator.Stop();
            _sessionState.CurrentSong = null;
            _sessionState.IsPlaying = false;
            _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            return false;
        }

        _setExplicitStopRequestedAction(false);
        return _sessionLoader.LoadNewSession(song);
    }

    public void ExecutePauseSession()
    {
        if (_sessionState.IsPlaying)
        {
            _playbackEngineCoordinator.Pause();
            _sessionState.IsPlaying = false;
            _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Paused;
        }
    }

    public void ExecuteResumeSession()
    {
        if (_sessionState.CurrentSong == null) return;

        if (_sessionState.CurrentPlaybackStatus == PlaybackStateStatus.Paused)
        {
            _playbackEngineCoordinator.Resume(startMonitor: true);
            _sessionState.IsPlaying = true;
            _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            _ = _scrobblingService.UpdateNowPlayingAsync(_sessionState.CurrentSong);
        }
        else if (_sessionState.CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
        {
            // If stopped, reload and play from current position
            _sessionLoader.ReloadSession(_sessionState.CurrentSong, _sessionState.CurrentPosition, true);
        }
    }

    public void ExecuteStopSession(bool isExplicit, Action onAlreadyStoppedAndExplicit)
    {
        _setExplicitStopRequestedAction(isExplicit);
        if (_sessionState.CurrentPlaybackStatus != PlaybackStateStatus.Stopped)
        {
            _playbackEngineCoordinator.Stop(); // This will trigger OnEngineCoordinatorPlaybackStopped
        }
        else if (isExplicit) // If already stopped but the stop request is explicit
        {
            onAlreadyStoppedAndExplicit?.Invoke();
        }
    }

    public void ExecuteSeekSession(TimeSpan requestedPosition)
    {
        if (_sessionState.CurrentSong == null || _sessionState.CurrentSongDuration == TimeSpan.Zero) return;
        _playbackEngineCoordinator.Seek(requestedPosition);
    }

    public void ExecuteForceReleaseEngineForCurrentSong()
    {
        if (_sessionState.CurrentSong == null)
        {
            Debug.WriteLine("[CommandExecutor] ForceReleaseEngine: No current song.");
            return;
        }
        Debug.WriteLine($"[CommandExecutor] ForceReleaseEngine for: {_sessionState.CurrentSong.Title}");
        _playbackEngineCoordinator.DisposeCurrentEngineInternals();
        _sessionState.IsPlaying = false;
        _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
        Debug.WriteLine($"[CommandExecutor] Engine resources released for {_sessionState.CurrentSong.Title}.");
    }

    public bool ExecuteForceReloadAndPlayEngine(Song song, TimeSpan position, bool shouldBePlaying)
    {
        Debug.WriteLine($"[CommandExecutor] ForceReloadAndPlayEngine for: {song.Title}, Pos: {position}, Play: {shouldBePlaying}");
        _setExplicitStopRequestedAction(false);
        return _sessionLoader.ReloadSession(song, position, shouldBePlaying);
    }
}