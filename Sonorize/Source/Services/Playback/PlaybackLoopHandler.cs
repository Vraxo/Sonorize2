using Sonorize.Models;
using Sonorize.Services;
using System.Diagnostics;
using System;

namespace Sonorize.Services;

/// <summary>
/// Handles loop region logic for playback, including checking for loop boundaries
/// during playback and adjusting seek positions.
/// </summary>
public class PlaybackLoopHandler : IDisposable // Implementing IDisposable for consistency, though no explicit unmanaged resources are held here.
{
    private readonly PlaybackService _playbackService; // Reference back to the PlaybackService
    private Song? _currentSong; // Keep a reference to the current song

    public PlaybackLoopHandler(PlaybackService playbackService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        Debug.WriteLine("[LoopHandler] Constructor called.");
        // No need to subscribe to PlaybackService events here; PlaybackService pushes data via method calls.
    }

    /// <summary>
    /// Updates the internal reference to the current song.
    /// Called by PlaybackService when CurrentSong changes.
    /// </summary>
    /// <param name="song">The new current song, or null.</param>
    internal void UpdateCurrentSong(Song? song)
    {
        _currentSong = song;
        Debug.WriteLine($"[LoopHandler] CurrentSong updated to: {_currentSong?.Title ?? "null"}");
        // The handler doesn't need to manage song.IsLoopActive persistence; that's handled by the ViewModel.
        // It just needs to *read* the Song's SavedLoop and IsLoopActive properties.
    }

    /// <summary>
    /// Checks if the current position is within an active loop region and triggers a seek if the end is reached.
    /// Called periodically by the PlaybackService UI update timer.
    /// </summary>
    /// <param name="currentPosition">The current playback position.</param>
    /// <param name="totalDuration">The total duration of the song.</param>
    internal void CheckForLoopSeek(TimeSpan currentPosition, TimeSpan totalDuration)
    {
        // Ensure we have a song, it has a saved loop, and the loop is active
        if (_currentSong?.SavedLoop != null && _currentSong.IsLoopActive)
        {
            var loop = _currentSong.SavedLoop;

            // Ensure loop end is after loop start and valid within total time
            if (loop.End > loop.Start && loop.End <= totalDuration)
            {
                // Check if current position is at or past the loop end
                // Using a small tolerance (e.g., 50ms) to trigger seek slightly before the exact end,
                // but ensure it's not extremely close to the *total* song duration.
                TimeSpan seekThreshold = loop.End - TimeSpan.FromMilliseconds(50);
                if (currentPosition >= seekThreshold && currentPosition < totalDuration - TimeSpan.FromMilliseconds(200))
                {
                    Debug.WriteLine($"[LoopHandler] Loop active & end reached ({currentPosition:mm\\:ss\\.ff} >= {seekThreshold:mm\\:ss\\.ff}) within file ({totalDuration:mm\\:ss\\.ff}). Requesting seek to loop start: {loop.Start:mm\\:ss\\.ff}");
                    // Request seek back to the loop start via the PlaybackService
                    // This call will come back into PlaybackService.Seek, which will use GetAdjustedSeekPosition,
                    // but since the target is *exactly* loop.Start, GetAdjustedSeekPosition should return loop.Start.
                    _playbackService.Seek(loop.Start);
                    // Note: The Seek method itself has a tolerance to prevent seeking if already very close.
                    // If currentPosition is already at or very near loop.Start (e.g., due to seek tolerance issues),
                    // this check might not trigger a redundant seek.
                }
                // If currentPosition is >= loop.End but also very close to totalDuration,
                // we let the natural end-of-file event trigger (handled by PlaybackService).
            }
            else if (_currentSong.IsLoopActive)
            {
                Debug.WriteLine($"[LoopHandler] Loop active for {_currentSong.Title} but invalid region ({loop.Start:mm\\:ss\\.ff} - {loop.End:mm\\:ss\\.ff}). Loop will not function.");
            }
        }
    }

    /// <summary>
    /// Adjusts a requested seek position based on the currently active loop region.
    /// Called by PlaybackService before performing a seek.
    /// </summary>
    /// <param name="requestedPosition">The position requested by the caller (e.g., UI slider, previous/next logic).</param>
    /// <param name="totalDuration">The total duration of the song.</param>
    /// <returns>The adjusted position, potentially snapped to the loop start.</returns>
    internal TimeSpan GetAdjustedSeekPosition(TimeSpan requestedPosition, TimeSpan totalDuration)
    {
        TimeSpan targetPosition = requestedPosition;

        // Apply loop region constraints if an active loop is defined for the current song.
        // If seeking *into* an active loop from *outside* its start or after its end, snap to start.
        // If seeking *within* an active loop, allow it.
        if (_currentSong?.SavedLoop != null && _currentSong.IsLoopActive)
        {
            var loop = _currentSong.SavedLoop;
            Debug.WriteLine($"[LoopHandler] GetAdjustedSeekPosition: Active loop detected [{loop.Start:mm\\:ss\\.ff}-{loop.End:mm\\:ss\\.ff}). Requested: {requestedPosition:mm\\:ss\\.ff}");

            // Ensure loop end is after loop start and valid within total time
            if (loop.End > loop.Start && loop.End <= totalDuration)
            {
                // If the target position is outside the loop's bounds [loop.Start, loop.End),
                // snap the target position to the loop's start time.
                if (targetPosition < loop.Start || targetPosition >= loop.End)
                {
                    Debug.WriteLine($"[LoopHandler] GetAdjustedSeekPosition: Target {targetPosition:mm\\:ss\\.ff} is outside loop bounds. Snapping to loop start: {loop.Start:mm\\:ss\\.ff}.");
                    targetPosition = loop.Start;
                }
                // If targetPosition is within [loop.Start, loop.End), allow normal seek within the loop.
                else
                {
                    Debug.WriteLine($"[LoopHandler] GetAdjustedSeekPosition: Target {targetPosition:mm\\:ss\\.ff} is within loop bounds. Allowing seek.");
                }
            }
            else if (_currentSong.IsLoopActive)
            {
                Debug.WriteLine($"[LoopHandler] GetAdjustedSeekPosition: Loop active but invalid region ({loop.Start:mm\\:ss\\.ff} - {loop.End:mm\\:ss\\.ff}). Not applying loop seek constraints.");
            }
        }
        else
        {
            Debug.WriteLine("[LoopHandler] GetAdjustedSeekPosition: No active loop. No adjustment needed.");
        }

        return targetPosition;
    }

    /// <summary>
    /// Determines the initial playback position when a new song is loaded.
    /// Returns the loop start if a loop is active, otherwise returns TimeSpan.Zero.
    /// Called by PlaybackService.Play().
    /// </summary>
    /// <param name="totalDuration">The total duration of the song.</param>
    /// <returns>The initial playback position.</returns>
    internal TimeSpan GetInitialPlaybackPosition(TimeSpan totalDuration)
    {
        if (_currentSong?.SavedLoop != null && _currentSong.IsLoopActive)
        {
            var loop = _currentSong.SavedLoop;
            // Ensure loop start is valid before returning it
            if (loop.Start >= TimeSpan.Zero && loop.Start < totalDuration)
            {
                Debug.WriteLine($"[LoopHandler] GetInitialPlaybackPosition: Active loop found. Starting at loop start: {loop.Start:mm\\:ss\\.ff}");
                return loop.Start;
            }
            else
            {
                Debug.WriteLine($"[LoopHandler] GetInitialPlaybackPosition: Active loop found, but loop start is invalid ({loop.Start >= totalDuration}). Starting from beginning.");
                return TimeSpan.Zero;
            }
        }
        Debug.WriteLine("[LoopHandler] GetInitialPlaybackPosition: No active loop. Starting from beginning.");
        return TimeSpan.Zero; // Start from the beginning if no active loop
    }


    public void Dispose()
    {
        Debug.WriteLine("[LoopHandler] Dispose() called.");
        // This class doesn't currently hold any resources that need explicit disposal.
        // Nullifying the song reference for cleanliness.
        _currentSong = null;
        Debug.WriteLine("[LoopHandler] Dispose() completed.");
    }

    // Finalizer not strictly needed as no unmanaged resources are held,
    // but included for consistency if needed later.
    ~PlaybackLoopHandler()
    {
        Debug.WriteLine("[LoopHandler] Finalizer called.");
        Dispose(false);
        Debug.WriteLine("[LoopHandler] Finalizer completed.");
    }
    // Protected virtual Dispose is standard pattern, though not strictly needed if no unmanaged resources
    protected virtual void Dispose(bool disposing)
    {
        // No specific managed or unmanaged resources to free here.
    }
}