using System;
using System.Diagnostics;
using Sonorize.Models;
using NAudio.Wave; // Required for StoppedEventArgs

namespace Sonorize.Services.Playback;

public class PlaybackCompletionHandler
{
    private readonly PlaybackService _playbackService;
    private readonly ScrobblingService _scrobblingService;

    public PlaybackCompletionHandler(PlaybackService playbackService, ScrobblingService scrobblingService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
    }

    public void Handle(
        NAudioPlaybackEngine? engine,
        StoppedEventArgs eventArgs,
        Song? songThatJustStopped,
        TimeSpan actualStoppedPosition,
        TimeSpan actualStoppedSongDuration,
        bool wasExplicitlyStopped)
    {
        Debug.WriteLine($"[PlaybackCompletionHandler] Handling playback stop for: {songThatJustStopped?.Title ?? "No Song"}. ExplicitStop: {wasExplicitlyStopped}, Error: {eventArgs.Exception != null}");

        _playbackService.StopUiUpdateTimerInternal();

        if (engine != null)
        {
            engine.PlaybackStopped -= _playbackService.GetEnginePlaybackStoppedHandler();
            Debug.WriteLine("[PlaybackCompletionHandler] Detached handler from the stopping engine instance.");
        }

        if (eventArgs.Exception != null)
        {
            Debug.WriteLine($"[PlaybackCompletionHandler] Playback stopped due to error: {eventArgs.Exception.Message}. Finalizing state to Stopped.");
            TryScrobble(songThatJustStopped, actualStoppedPosition);
            _playbackService.SetCurrentSongInternal(null); // This will also update IsPlaying and Status via its setter chain
        }
        else
        {
            // isNearEndOfFile should use the duration of the song that actually played.
            bool isNearEndOfFile = (actualStoppedSongDuration > TimeSpan.Zero) &&
                                   (actualStoppedPosition >= actualStoppedSongDuration - TimeSpan.FromMilliseconds(500));

            Debug.WriteLine($"[PlaybackCompletionHandler] Clean Stop. ExplicitStopReq: {wasExplicitlyStopped}. NearEnd: {isNearEndOfFile}. Pos: {actualStoppedPosition:mm\\:ss\\.ff}, Dur: {actualStoppedSongDuration:mm\\:ss\\.ff}");

            if (wasExplicitlyStopped)
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped by explicit user/app command. Finalizing.");
                TryScrobble(songThatJustStopped, actualStoppedPosition);
                _playbackService.SetCurrentSongInternal(null);
            }
            else if (isNearEndOfFile)
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped naturally (end of file).");
                TryScrobble(songThatJustStopped, actualStoppedSongDuration); // Scrobble with full duration for natural end
                _playbackService.UpdateStateForNaturalPlaybackEndInternal();
                _playbackService.InvokePlaybackEndedNaturallyInternal();
            }
            else
            {
                // This case might occur if the engine stops for an unknown reason not classified as an error
                // or if the "near end of file" logic isn't perfectly aligned with engine behavior.
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped (not error, not explicit, not EOF). Scrobbling and stopping.");
                TryScrobble(songThatJustStopped, actualStoppedPosition);
                _playbackService.SetCurrentSongInternal(null);
            }
        }

        // Ensure IsPlaying and CurrentPlaybackStatus are also set correctly if CurrentSong becomes null
        // This is typically handled by SetCurrentSongInternal's chain reaction, but good to be mindful.
        if (_playbackService.GetCurrentSongInternal() == null)
        {
            _playbackService.SetPlaybackStateInternal(false, PlaybackStateStatus.Stopped);
        }

        _playbackService.ResetExplicitStopRequestInternal();
        Debug.WriteLine($"[PlaybackCompletionHandler] Handle finishes. CurrentSong after handling: {_playbackService.GetCurrentSongInternal()?.Title ?? "null"}");
    }

    private async void TryScrobble(Song? song, TimeSpan playedPosition)
    {
        if (song == null) return;
        Debug.WriteLine($"[PlaybackCompletionHandler] TryScrobble called for '{song.Title}' at {playedPosition}.");
        if (_scrobblingService.ShouldScrobble(song, playedPosition))
        {
            // Ensure ScrobbleAsync is awaited if it's truly async and we need to wait for it,
            // or fire-and-forget if that's acceptable.
            await _scrobblingService.ScrobbleAsync(song, DateTime.UtcNow).ConfigureAwait(false);
        }
    }
}