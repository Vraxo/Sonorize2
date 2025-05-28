using System;
using System.Diagnostics;
using Sonorize.Models;
using NAudio.Wave; // Required for StoppedEventArgs

namespace Sonorize.Services.Playback; // Corrected namespace

public class PlaybackCompletionHandler
{
    private readonly PlaybackService _playbackService; // Used to call the "internal" methods
    private readonly ScrobblingService _scrobblingService;

    public PlaybackCompletionHandler(PlaybackService playbackService, ScrobblingService scrobblingService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
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

        _playbackService.StopUiUpdateTimerInternal(); // Calls SessionManager's method

        if (eventArgs.Exception != null)
        {
            Debug.WriteLine($"[PlaybackCompletionHandler] Playback stopped due to error: {eventArgs.Exception.Message}. Finalizing state to Stopped.");
            TryScrobble(songThatJustStopped, actualStoppedPosition);
            _playbackService.SetCurrentSongInternal(null); // Calls SessionManager's method
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
                _playbackService.SetCurrentSongInternal(null); // Calls SessionManager's method
            }
            else if (isNearEndOfFile)
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped naturally (end of file).");
                TryScrobble(songThatJustStopped, actualStoppedSongDuration);
                _playbackService.UpdateStateForNaturalPlaybackEndInternal(); // Calls SessionManager's method
                _playbackService.InvokePlaybackEndedNaturallyInternal();    // Calls SessionManager's method
            }
            else
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped (not error, not explicit, not EOF). Scrobbling and stopping.");
                TryScrobble(songThatJustStopped, actualStoppedPosition);
                _playbackService.SetCurrentSongInternal(null); // Calls SessionManager's method
            }
        }

        // Ensure playback state is updated if song is now null
        if (_playbackService.GetCurrentSongInternal() == null) // Calls SessionManager's method
        {
            _playbackService.SetPlaybackStateInternal(false, PlaybackStateStatus.Stopped); // Calls SessionManager's method
        }

        _playbackService.ResetExplicitStopRequestInternal(); // Calls SessionManager's method
        Debug.WriteLine($"[PlaybackCompletionHandler] Handle finishes. CurrentSong after handling: {_playbackService.GetCurrentSongInternal()?.Title ?? "null"}");
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