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
        bool wasExplicitlyStopped, // Flag indicating if StopSession(isExplicit: true) was the initiator
        bool isInternalStopForSongChange)
    {
        Debug.WriteLine($"[PlaybackCompletionHandler] Handling playback stop for: {songThatJustStopped?.Title ?? "No Song"}. ExplicitStop: {wasExplicitlyStopped}, InternalChange: {isInternalStopForSongChange}, Error: {eventArgs.Exception != null}, Pos: {actualStoppedPosition:mm\\:ss\\.ff}, Dur: {actualStoppedSongDuration:mm\\:ss\\.ff}");

        if (eventArgs.Exception != null)
        {
            _sessionManager.StopUiUpdateMonitor(); // Stop monitor on error
            Debug.WriteLine($"[PlaybackCompletionHandler] Playback stopped due to error: {eventArgs.Exception.Message}. Finalizing state to Stopped.");
            _sessionManager.FinalizeCurrentSong(null);
            _sessionManager.SetPlaybackState(false, PlaybackStateStatus.Stopped);
        }
        else if (wasExplicitlyStopped)
        {
            // This is a "hard stop" initiated by the user or app logic intending to end the session.
            _sessionManager.StopUiUpdateMonitor();
            Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped by explicit user/app command. Finalizing.");
            _sessionManager.FinalizeCurrentSong(null);
            _sessionManager.SetPlaybackState(false, PlaybackStateStatus.Stopped);
        }
        else if (isInternalStopForSongChange)
        {
            // This is an internal, programmatic stop that occurred because a new song is being loaded.
            // The SessionManager is already handling the transition, so there's nothing for the completion handler to do.
            Debug.WriteLine($"[PlaybackCompletionHandler] Playback stopped internally for song change. No action needed here.");
        }
        else // Not explicit, not an internal change, not an error - must be natural completion.
        {
            Debug.WriteLine("[PlaybackCompletionHandler] Playback stopped naturally (end of file).");
            _sessionManager.UpdateStateForNaturalPlaybackEnd();
            _sessionManager.TriggerSessionEndedNaturally(); // This will kick off next track logic.
        }

        _sessionManager.ResetExplicitStopFlag();
        Debug.WriteLine($"[PlaybackCompletionHandler] Handle finishes. Session Song: {_sessionManager.GetCurrentSongForCompletion()?.Title ?? "null"}, IsPlaying: {_sessionManager.IsPlaying}, Status: {_sessionManager.CurrentPlaybackStatus}");
    }
}
