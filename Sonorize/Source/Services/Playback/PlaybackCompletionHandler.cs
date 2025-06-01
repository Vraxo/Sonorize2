using System;
using System.Diagnostics;
using Sonorize.Models;
using NAudio.Wave; // Required for StoppedEventArgs

namespace Sonorize.Services.Playback;

public class PlaybackCompletionHandler
{
    private readonly ScrobblingService _scrobblingService;

    public PlaybackCompletionHandler(ScrobblingService scrobblingService)
    {
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
    }

    public PlaybackCompletionResult Handle(
        StoppedEventArgs eventArgs,
        Song? songThatJustStopped,
        TimeSpan actualStoppedPosition,
        TimeSpan actualStoppedSongDuration,
        bool wasExplicitlyStopped)
    {
        Debug.WriteLine($"[PlaybackCompletionHandler] Handling playback stop for: {songThatJustStopped?.Title ?? "No Song"}. ExplicitStop: {wasExplicitlyStopped}, Error: {eventArgs.Exception != null}");

        bool stopUiUpdateMonitor = true; // Generally, stop monitor when playback stops.
        Song? nextSong = songThatJustStopped; // Default to current song, might be set to null or different one.
        bool shouldBePlaying = false;
        PlaybackStateStatus finalStatus = PlaybackStateStatus.Stopped;
        bool triggerSessionEndedNaturally = false;

        if (eventArgs.Exception != null)
        {
            Debug.WriteLine($"[PlaybackCompletionHandler] Playback stopped due to error: {eventArgs.Exception.Message}. Finalizing state to Stopped.");
            TryScrobble(songThatJustStopped, actualStoppedPosition);
            nextSong = null; // Clear current song on error
            // finalStatus remains Stopped, shouldBePlaying remains false
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
                nextSong = null; // Clear current song if explicitly stopped and not handled elsewhere (e.g. next track)
                // finalStatus remains Stopped, shouldBePlaying remains false
            }
            else if (isNearEndOfFile)
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped naturally (end of file).");
                TryScrobble(songThatJustStopped, actualStoppedSongDuration);
                // nextSong might be determined by a higher layer after SessionEndedNaturally is triggered.
                // For now, this handler doesn't determine the *next* song to play, only current state.
                // It sets current to stopped.
                nextSong = songThatJustStopped; // Keep current song context for now, PlaybackService will handle next
                triggerSessionEndedNaturally = true;
                // finalStatus remains Stopped, shouldBePlaying remains false
            }
            else
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped (not error, not explicit, not EOF). Scrobbling and stopping.");
                TryScrobble(songThatJustStopped, actualStoppedPosition);
                nextSong = null; // Clear current song
                // finalStatus remains Stopped, shouldBePlaying remains false
            }
        }

        // If the result is that no song should be current, set nextSong to null.
        // This might be redundant if logic above already does it, but ensures clarity.
        if (finalStatus == PlaybackStateStatus.Stopped && !triggerSessionEndedNaturally) // if fully stopping without natural end
        {
            // If explicitly stopped or error, nextSong should be null unless handled by a specific flow not visible here
            if (wasExplicitlyStopped || eventArgs.Exception != null)
            {
                nextSong = null;
            }
            // If it stopped for other reasons, nextSong = null is likely correct to clear current track.
        }


        Debug.WriteLine($"[PlaybackCompletionHandler] Handle determines: NextSong='{nextSong?.Title ?? "null"}', FinalStatus={finalStatus}, TriggerNaturalEnd={triggerSessionEndedNaturally}");
        return new PlaybackCompletionResult(nextSong, shouldBePlaying, finalStatus, triggerSessionEndedNaturally, stopUiUpdateMonitor);
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