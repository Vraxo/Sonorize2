using System;
using System.Diagnostics;
using Sonorize.Models;

namespace Sonorize.Services.Playback;

public class PlaybackResourceInterlockService
{
    private readonly PlaybackSessionManager _sessionManager;

    public PlaybackResourceInterlockService(PlaybackSessionManager sessionManager)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        Debug.WriteLine("[PlaybackResourceInterlockService] Initialized.");
    }

    public (bool WasPlaying, TimeSpan Position)? PrepareForExternalOperation(Song song)
    {
        if (song is null)
        {
            Debug.WriteLine("[InterlockService] PrepareForExternalOperation: Song is null. No action.");
            return null;
        }

        if (_sessionManager.CurrentSong != song)
        {
            Debug.WriteLine($"[InterlockService] PrepareForExternalOperation: Song '{song.Title}' is not the current song ('{_sessionManager.CurrentSong?.Title}'). No action taken.");
            return null;
        }

        bool wasPlaying = _sessionManager.IsPlaying;
        TimeSpan position = _sessionManager.CurrentPosition;

        Debug.WriteLine($"[InterlockService] Preparing for external operation on '{song.Title}'. WasPlaying: {wasPlaying}, Position: {position}. Forcing engine release.");
        _sessionManager.ForceReleaseEngineForCurrentSong();

        return (wasPlaying, position);
    }

    public bool ResumeAfterExternalOperation(Song song, TimeSpan position, bool play)
    {
        if (song is null)
        {
            Debug.WriteLine("[InterlockService] ResumeAfterExternalOperation: Song is null. Cannot resume.");
            return false;
        }

        Debug.WriteLine($"[InterlockService] Resuming after external operation for '{song.Title}'. Position: {position}, Play: {play}. Forcing reload.");
        return _sessionManager.ForceReloadAndPlayEngine(song, position, play);
    }
}