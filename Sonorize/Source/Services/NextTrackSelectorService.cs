using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Services;

public class NextTrackSelectorService(Random shuffleRandom)
{
    private readonly Random _shuffleRandom = shuffleRandom ?? throw new ArgumentNullException(nameof(shuffleRandom));

    public Song? GetNextSong(Song? currentSong, List<Song> currentList, RepeatMode repeatMode, bool shuffleEnabled)
    {
        if (currentSong is null || currentList.Count == 0)
        {
            Debug.WriteLine("[NextTrackSelector] No current song or list is empty. No next song.");
            return null;
        }

        if (repeatMode == RepeatMode.RepeatOne)
        {
            Debug.WriteLine($"[NextTrackSelector] Repeat Mode is RepeatOne. Next song is current: {currentSong.Title}");
            return currentSong;
        }

        if (repeatMode == RepeatMode.None)
        {
            Debug.WriteLine("[NextTrackSelector] Repeat Mode is None. No next song.");
            return null;
        }

        return shuffleEnabled
            ? GetNextSongShuffle(currentSong, currentList, repeatMode)
            : GetNextSongSequential(currentSong, currentList, repeatMode);
    }

    private Song? GetNextSongShuffle(Song currentSong, List<Song> currentList, RepeatMode repeatMode)
    {
        Debug.WriteLine("[NextTrackSelector] Shuffle is Enabled.");

        Song? nextSong = null;

        if (currentList.Count == 0)
        {
            Debug.WriteLine("[NextTrackSelector] Shuffle enabled, but list is empty.");
            return null;
        }

        List<Song> potentialNextSongs = currentList.Where(s => s != currentSong).ToList();

        if (potentialNextSongs.Count != 0)
        {
            int nextIndex = _shuffleRandom.Next(potentialNextSongs.Count);
            nextSong = potentialNextSongs[nextIndex];

            Debug.WriteLine($"[NextTrackSelector] Shuffle pick: {nextSong?.Title ?? "null"}");
        }
        else if (currentList.Count == 1)
        {
            if (repeatMode == RepeatMode.RepeatAll)
            {
                Debug.WriteLine($"[NextTrackSelector] Shuffle enabled, one song in list, RepeatAll active. Replaying: {currentSong.Title}");
                nextSong = currentSong;
            }
            else
            {
                Debug.WriteLine($"[NextTrackSelector] Shuffle enabled, only one song ({currentSong.Title}) in list, not RepeatAll. No next song.");
                nextSong = null;
            }
        }
        else
        {
            Debug.WriteLine("[NextTrackSelector] Shuffle enabled, logical error: no potential next songs from a multi-item list. No next song.");
            nextSong = null;
        }

        return nextSong;
    }

    private static Song? GetNextSongSequential(Song currentSong, List<Song> currentList, RepeatMode repeatMode)
    {
        Debug.WriteLine("[NextTrackSelector] Shuffle is Disabled (Sequential).");
        int currentIndex = currentList.IndexOf(currentSong);

        if (currentIndex == -1)
        {
            Debug.WriteLine("[NextTrackSelector] Sequential: Current song not found in list. No next song.");
            return null;
        }

        Song? nextSong;

        if (currentIndex < currentList.Count - 1)
        {
            nextSong = currentList[currentIndex + 1];
            Debug.WriteLine($"[NextTrackSelector] Sequential next: {nextSong?.Title ?? "null"}");
        }
        else
        {
            Debug.WriteLine("[NextTrackSelector] End of sequential list reached.");

            if (repeatMode == RepeatMode.RepeatAll && currentList.Count != 0)
            {
                nextSong = currentList.First(); // Wrap around
                Debug.WriteLine($"[NextTrackSelector] RepeatAll active, wrapping around to first: {nextSong.Title}");
            }
            else
            {
                Debug.WriteLine($"[NextTrackSelector] RepeatMode is {repeatMode} (not RepeatAll), end of list reached. No next song.");
                nextSong = null;
            }
        }

        return nextSong;
    }
}