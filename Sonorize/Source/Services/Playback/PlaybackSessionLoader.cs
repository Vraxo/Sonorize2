using System;
using System.Diagnostics;
using System.IO;
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackSessionLoader
{
    private readonly PlaybackEngineCoordinator _playbackEngineCoordinator;
    private readonly PlaybackSessionState _sessionState;
    private readonly ScrobblingService _scrobblingService;

    public PlaybackSessionLoader(
        PlaybackEngineCoordinator playbackEngineCoordinator,
        PlaybackSessionState sessionState,
        ScrobblingService scrobblingService)
    {
        _playbackEngineCoordinator = playbackEngineCoordinator ?? throw new ArgumentNullException(nameof(playbackEngineCoordinator));
        _sessionState = sessionState ?? throw new ArgumentNullException(nameof(sessionState));
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
    }

    public bool LoadNewSession(Song song)
    {
        Debug.WriteLine($"[PlaybackSessionLoader] LoadNewSession for: {song.Title}");
        _sessionState.CurrentSong = song;
        _playbackEngineCoordinator.SetSong(song);

        try
        {
            if (!_playbackEngineCoordinator.Load(song.FilePath, _sessionState.PlaybackRate, _sessionState.PitchSemitones))
            {
                Debug.WriteLine($"[PlaybackSessionLoader] EngineCoordinator.Load failed for {song.Title}.");
                _sessionState.CurrentSong = null; // Clear song on failure
                return false;
            }
            UpdateSessionTimingsPostLoad();
            _playbackEngineCoordinator.Play(startMonitor: true);
            _sessionState.IsPlaying = true;
            _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Playing;
            _ = _scrobblingService.UpdateNowPlayingAsync(song);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackSessionLoader] Exception during LoadNewSession for '{song.FilePath}': {ex}");
            if (_sessionState.CurrentSong is not null) _playbackEngineCoordinator.Stop(); // Ensure engine is stopped if partially loaded
            _sessionState.CurrentSong = null;
            return false;
        }
    }

    public bool ReloadSession(Song song, TimeSpan position, bool shouldBePlaying)
    {
        Debug.WriteLine($"[PlaybackSessionLoader] ReloadSession for: {song.Title}, Position: {position}, ShouldPlay: {shouldBePlaying}");
        _sessionState.CurrentSong = song;
        _playbackEngineCoordinator.SetSong(song);

        try
        {
            if (!_playbackEngineCoordinator.Load(song.FilePath, _sessionState.PlaybackRate, _sessionState.PitchSemitones))
            {
                Debug.WriteLine($"[PlaybackSessionLoader] EngineCoordinator.Load failed during ReloadSession for {song.Title}.");
                _sessionState.CurrentSong = null;
                _sessionState.IsPlaying = false;
                _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
                return false;
            }

            UpdateSessionTimingsPostLoad(position);
            _playbackEngineCoordinator.Seek(position);

            if (shouldBePlaying)
            {
                _playbackEngineCoordinator.Play(startMonitor: true);
                _sessionState.IsPlaying = true;
                _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Playing;
                _ = _scrobblingService.UpdateNowPlayingAsync(song);
            }
            else
            {
                _sessionState.IsPlaying = false;
                _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Paused; // Or Stopped if preferred when not playing
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaybackSessionLoader] Exception during ReloadSession for '{song.FilePath}': {ex}");
            _sessionState.CurrentSong = null;
            _sessionState.IsPlaying = false;
            _sessionState.CurrentPlaybackStatus = PlaybackStateStatus.Stopped;
            return false;
        }
    }

    private void UpdateSessionTimingsPostLoad(TimeSpan? initialPosition = null)
    {
        _sessionState.CurrentPosition = initialPosition ?? TimeSpan.Zero;
        _sessionState.CurrentSongDuration = _playbackEngineCoordinator.CurrentSongDuration;
    }
}