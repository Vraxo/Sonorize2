using System;
using System.Diagnostics;
using Sonorize.Models;

namespace Sonorize.Services;

public class SongLoopService
{
    private readonly LoopDataService _loopDataService;

    public SongLoopService(LoopDataService loopDataService)
    {
        _loopDataService = loopDataService ?? throw new ArgumentNullException(nameof(loopDataService));
        Debug.WriteLine("[SongLoopService] Initialized.");
    }

    public void SaveLoop(Song song, TimeSpan start, TimeSpan end, bool activate)
    {
        if (song == null)
        {
            Debug.WriteLine("[SongLoopService] SaveLoop: Song is null.");
            return;
        }

        song.SavedLoop = new LoopRegion(start, end, "User Loop");
        song.IsLoopActive = activate; // This will raise PropertyChanged on the Song model

        _loopDataService.SetLoop(song.FilePath, start, end, activate);
        Debug.WriteLine($"[SongLoopService] Loop saved and persisted for {song.Title}. Start: {start}, End: {end}, Active: {activate}");
    }

    public void ClearLoop(Song song)
    {
        if (song == null)
        {
            Debug.WriteLine("[SongLoopService] ClearLoop: Song is null.");
            return;
        }

        var filePath = song.FilePath;
        // Update model first
        song.IsLoopActive = false;
        song.SavedLoop = null; // This will raise PropertyChanged on the Song model

        if (!string.IsNullOrEmpty(filePath))
        {
            _loopDataService.ClearLoop(filePath);
        }
        Debug.WriteLine($"[SongLoopService] Loop cleared and persistence updated for {song.Title}.");
    }

    public void SetLoopActiveState(Song song, bool isActive)
    {
        if (song == null)
        {
            Debug.WriteLine("[SongLoopService] SetLoopActiveState: Song is null.");
            return;
        }

        if (song.SavedLoop == null && isActive)
        {
            Debug.WriteLine($"[SongLoopService] SetLoopActiveState: Cannot activate loop for {song.Title} as no loop is defined. No change made.");
            return; // Do not change IsLoopActive if no loop is defined and trying to activate.
        }

        if (song.IsLoopActive == isActive) // No change needed
        {
            return;
        }

        song.IsLoopActive = isActive; // This will raise PropertyChanged on the Song model

        // Persist only if a loop actually exists
        if (song.SavedLoop != null)
        {
            _loopDataService.UpdateLoopActiveState(song.FilePath, isActive);
            Debug.WriteLine($"[SongLoopService] Loop active state for {song.Title} set to {isActive} and persisted.");
        }
        else
        {
            // This case should ideally be caught by the check above, 
            // but as a safeguard: if IsLoopActive was true and loop became null, ensure it's set to false.
            if (isActive) // Should not happen if SavedLoop is null
            {
                song.IsLoopActive = false; // Correct the model state
                Debug.WriteLine($"[SongLoopService] Corrected IsLoopActive to false for {song.Title} as SavedLoop is null.");
            }
            // No persistence needed if SavedLoop is null, IsLoopActive should be false.
        }
    }
}