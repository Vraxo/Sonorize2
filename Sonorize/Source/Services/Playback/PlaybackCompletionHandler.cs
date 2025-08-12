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
        Song? songThatJustStopped, // The song instance whose playback actually stopped
        TimeSpan actualStoppedPosition,
        TimeSpan actualStoppedSongDuration,
        bool wasExplicitlyStopped) // Flag indicating if StopSession(isExplicit: true) was the initiator
    {
        Debug.WriteLine($"[PlaybackCompletionHandler] Handling playback stop for: {songThatJustStopped?.Title ?? "No Song"}. ExplicitStopRequest: {wasExplicitlyStopped}, Error: {eventArgs.Exception != null}");

        if (eventArgs.Exception != null)
        {
            _sessionManager.StopUiUpdateMonitor(); // Stop monitor on error
            Debug.WriteLine($"[PlaybackCompletionHandler] Playback stopped due to error: {eventArgs.Exception.Message}. Finalizing state to Stopped.");
            TryScrobble(songThatJustStopped, actualStoppedPosition);
            _sessionManager.FinalizeCurrentSong(null);
            _sessionManager.SetPlaybackState(false, PlaybackStateStatus.Stopped);
        }
        else
        {
            bool isNearEndOfFile = (actualStoppedSongDuration > TimeSpan.Zero) &&
                                   (actualStoppedPosition >= actualStoppedSongDuration - TimeSpan.FromMilliseconds(500));

            Debug.WriteLine($"[PlaybackCompletionHandler] Clean Stop. ExplicitlyStoppedByRequest: {wasExplicitlyStopped}. NearEnd: {isNearEndOfFile}. Pos: {actualStoppedPosition:mm\\:ss\\.ff}, Dur: {actualStoppedSongDuration:mm\\:ss\\.ff}");

            if (wasExplicitlyStopped)
            {
                _sessionManager.StopUiUpdateMonitor(); // Stop monitor on explicit stop
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped by explicit user/app command. Finalizing.");
                TryScrobble(songThatJustStopped, actualStoppedPosition);
                _sessionManager.FinalizeCurrentSong(null);
                _sessionManager.SetPlaybackState(false, PlaybackStateStatus.Stopped);
            }
            else if (isNearEndOfFile)
            {
                Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped naturally (end of file).");
                TryScrobble(songThatJustStopped, actualStoppedSongDuration);
                _sessionManager.UpdateStateForNaturalPlaybackEnd();
                _sessionManager.TriggerSessionEndedNaturally();
            }
            else // Internal stop (not explicit, not EOF) - e.g., transitioning to a new song or re-clicking current.
            {
                Debug.WriteLine($"[PlaybackCompletionHandler] Playback stopped internally. Song that just stopped: '{songThatJustStopped?.Title}', Session's current song: '{_sessionManager.GetCurrentSongForCompletion()?.Title}'. Session IsPlaying: {_sessionManager.IsPlaying}");
                TryScrobble(songThatJustStopped, actualStoppedPosition);

                // If the SessionManager indicates that it's NOT currently playing ANYTHING,
                // then it's safe for this internal stop event (for whatever song it was)
                // to confirm the global state as Stopped. This can happen if the next song fails to load.
                if (!_sessionManager.IsPlaying)
                {
                    _sessionManager.StopUiUpdateMonitor(); // Stop monitor here as playback has failed.
                    Debug.WriteLine($"[PlaybackCompletionHandler] Session is NOT currently playing. Setting global state to Stopped due to internal stop of '{songThatJustStopped?.Title}'.");
                    _sessionManager.SetPlaybackState(false, PlaybackStateStatus.Stopped);
                }
                // ELSE: SessionManager IS IsPlaying. This means another playback instance (e.g., the next song,
                // or a re-clicked version of the current song) has already started and set the global state.
                // The stop event for this (now potentially old/superseded) song instance should NOT
                // override the global IsPlaying state OR THE MONITOR.
                else
                {
                    Debug.WriteLine($"[PlaybackCompletionHandler] Session IS currently playing (current song: '{_sessionManager.GetCurrentSongForCompletion()?.Title}'). " +
                                    $"The internal stop event for '{songThatJustStopped?.Title}' will NOT change global playback state or stop the monitor.");
                }
            }
        }

        _sessionManager.ResetExplicitStopFlag();
        Debug.WriteLine($"[PlaybackCompletionHandler] Handle finishes. Session Song: {_sessionManager.GetCurrentSongForCompletion()?.Title ?? "null"}, IsPlaying: {_sessionManager.IsPlaying}, Status: {_sessionManager.CurrentPlaybackStatus}");
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