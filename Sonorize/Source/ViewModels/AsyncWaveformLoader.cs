using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels;

internal class AsyncWaveformLoader : ViewModelBase
{
    private readonly WaveformService _waveformService;
    private Song? _songCurrentlyLoadingOrLoaded; // Tracks the song for which the current/last load was initiated

    public ObservableCollection<WaveformPoint> WaveformRenderData { get; } = new();

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public AsyncWaveformLoader(WaveformService waveformService)
    {
        _waveformService = waveformService ?? throw new ArgumentNullException(nameof(waveformService));
    }

    public async Task RequestLoadAsync(Song songToLoad, int targetPoints, Song? currentDisplaySongContext, bool isPanelVisibleContext)
    {
        if (string.IsNullOrEmpty(songToLoad.FilePath))
        {
            Debug.WriteLine($"[AsyncWaveformLoader] Load request skipped: Invalid path for song '{songToLoad.Title}'.");
            if (_songCurrentlyLoadingOrLoaded == songToLoad) // If this invalid song was being "loaded"
            {
                IsLoading = false;
                WaveformRenderData.Clear();
                _songCurrentlyLoadingOrLoaded = null;
            }
            return;
        }

        // If already loading for this exact song instance, don't restart.
        if (IsLoading && _songCurrentlyLoadingOrLoaded == songToLoad)
        {
            Debug.WriteLine($"[AsyncWaveformLoader] Already loading waveform for '{songToLoad.Title}'. Skipping redundant load request.");
            return;
        }

        _songCurrentlyLoadingOrLoaded = songToLoad; // Set the song context for this load operation
        IsLoading = true;
        WaveformRenderData.Clear(); // Clear previous data before loading new

        try
        {
            Debug.WriteLine($"[AsyncWaveformLoader] Requesting waveform from service for: {songToLoad.Title}");
            var points = await _waveformService.GetWaveformAsync(songToLoad.FilePath, targetPoints);

            // Critical check: Ensure the context for display (current song and panel visibility)
            // still matches the song we just loaded data for.
            if (currentDisplaySongContext == songToLoad && isPanelVisibleContext)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    WaveformRenderData.Clear(); // Ensure it's empty before adding new points
                    foreach (var p in points)
                    {
                        WaveformRenderData.Add(p);
                    }
                    Debug.WriteLine($"[AsyncWaveformLoader] Waveform loaded and UI updated for: {songToLoad.Title}, {points.Count} points.");
                });
            }
            else
            {
                Debug.WriteLine($"[AsyncWaveformLoader] Waveform for '{songToLoad.Title}' loaded, but display context changed. Current display song: '{currentDisplaySongContext?.Title}', panel visible: {isPanelVisibleContext}. Discarding result.");
                // If context changed, ensure data for the (now irrelevant) loaded song is cleared if it wasn't already.
                // This path implies another load might be in progress or needed for the new context.
                if (WaveformRenderData.Any()) // If by some chance it got populated
                {
                    await Dispatcher.UIThread.InvokeAsync(() => WaveformRenderData.Clear());
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AsyncWaveformLoader] CRITICAL Error loading waveform for '{songToLoad.Title}': {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                WaveformRenderData.Clear(); // Clear on error
            });
        }
        finally
        {
            // Set IsLoading to false only if this load operation was for the song that's still
            // considered the "active" one for the loader, or if no song is active.
            // This prevents a completed load for an old song from incorrectly marking IsLoading as false
            // if a new load for a different song has already started.
            if (_songCurrentlyLoadingOrLoaded == songToLoad || currentDisplaySongContext == null)
            {
                IsLoading = false;
            }
            else
            {
                Debug.WriteLine($"[AsyncWaveformLoader] Load for '{songToLoad.Title}' finished, but a new song '{_songCurrentlyLoadingOrLoaded?.Title}' might be targeted. IsLoading state determined by newer operations.");
            }
        }
    }

    public void ClearDataAndState()
    {
        WaveformRenderData.Clear();
        IsLoading = false;
        _songCurrentlyLoadingOrLoaded = null;
        Debug.WriteLine("[AsyncWaveformLoader] Data and loading state cleared.");
    }
}