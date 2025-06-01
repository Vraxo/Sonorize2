using System;
using System.Diagnostics;
using Sonorize.Models;
using NAudio.Wave; // Required for StoppedEventArgs

namespace Sonorize.Services.Playback;

public class PlaybackCompletionHandler
{
    private readonly PlaybackSessionManager _sessionManager;
    private readonly ScrobblingService _scrobblingService;

    public PlaybackCompletionHandler(PlaybackSessionManager sessionManager, ScrobblingService scrobblingService)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
    }

    public void Handle(
        StoppedEventArgs eventArgs,
        Song? songThatJustStopped,
        TimeSpan actualStoppedPosition,
        TimeSpan actualStoppedSongDuration,
        bool wasExplicitlyStopped)
    {
        Debug.WriteLine($"[PlaybackCompletionHandler] Handling playback stop for: {songThatJustStopped?.Title ?? "No Song"}. ExplicitStop: {wasExplicitlyStopped}, Error: {eventArgs.Exception != null}");

        _sessionManager.StopUiUpdateMonitor();

        if (eventArgs.Exception != null)
        {
            Debug.WriteLine($"[PlaybackCompletionHandler] Playback stopped due to error: {eventArgs.Exception.Message}. Finalizing state to Stopped.");
            TryScrobble(songThatJustStopped, actualStoppedPosition);
            _sessionManager.FinalizeCurrentSong(null);
        }
        else
        {
            bool isNearEndOfFile = (actualStoppedSongDuration > TimeSpan.Zero) &&
                                   (actualStoppedPosition >= actualStoppedSongDuration - TimeSpan.FromMilliseconds(500));

            Debug.WriteLine($"[PlaybackCompletionHandler] Clean Stop. ExplicitStopReq: {wasExplicitlyStopped}. NearEnd: {isNearEndOfFile}. Pos: {actualStoppedPosition:mm\\:ss\\.ff}, Dur: {actualStoppedSongDuration:mm\\:ss\\.ff}");

            if (wasExplicitlyStopped)
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped by explicit user/app command. Finalizing.");
                TryScrobble(songThatJustStopped, actualStoppedPosition);
                _sessionManager.FinalizeCurrentSong(null);
            }
            else if (isNearEndOfFile)
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped naturally (end of file).");
                TryScrobble(songThatJustStopped, actualStoppedSongDuration);
                _sessionManager.UpdateStateForNaturalPlaybackEnd();
                _sessionManager.TriggerSessionEndedNaturally();
            }
            else
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped (not error, not explicit, not EOF). Scrobbling and stopping.");
                TryScrobble(songThatJustStopped, actualStoppedPosition);
                _sessionManager.FinalizeCurrentSong(null);
            }
        }

        if (_sessionManager.GetCurrentSongForCompletion() == null)
        {
            _sessionManager.SetPlaybackState(false, PlaybackStateStatus.Stopped);
        }

        _sessionManager.ResetExplicitStopFlag();
        Debug.WriteLine($"[PlaybackCompletionHandler] Handle finishes. CurrentSong after handling: {_sessionManager.GetCurrentSongForCompletion()?.Title ?? "null"}");
    }

    private async void TryScrobble(Song? song, TimeSpan playedPosition)
    {
        if (song == null) return;
        Debug.WriteLine($"[PlaybackCompletionHandler] TryScrobble called for '{song.Title}' at {playedPosition}.");
        if (_scrobblingService.ShouldScrobble(song, playedPosition))
        {
            await _scrobblingService.ScrobbleAsync(song, DateTime.UtcNow).ConfigureAwait(false);
        }
    }
}