using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels;

public class WaveformDisplayViewModel : ViewModelBase
{
    private readonly PlaybackService _playbackService;
    private readonly WaveformService _waveformService;
    private Song? _currentSongForWaveform;
    private bool _isPanelVisible;

    public ObservableCollection<WaveformPoint> WaveformRenderData { get; } = new();
    
    public bool IsWaveformLoading
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public WaveformDisplayViewModel(PlaybackService playbackService, WaveformService waveformService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _waveformService = waveformService ?? throw new ArgumentNullException(nameof(waveformService));

        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;
        UpdateCurrentSongForWaveform(_playbackService.CurrentSong);
    }

    private async void PlaybackService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackService.CurrentSong))
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateCurrentSongForWaveform(_playbackService.CurrentSong);
            });
        }
    }

    private void UpdateCurrentSongForWaveform(Song? newSong)
    {
        if (_currentSongForWaveform == newSong)
        {
            // If the song is the same, and panel is visible, ensure waveform is loaded if not already
            if (_isPanelVisible && newSong != null && !WaveformRenderData.Any() && !IsWaveformLoading)
            {
                Debug.WriteLine($"[WaveformDisplayVM] Panel visible for same song '{newSong.Title}', but no waveform. Triggering load.");
                _ = LoadWaveformInternalAsync(newSong);
            }
            return;
        }

        _currentSongForWaveform = newSong;
        WaveformRenderData.Clear(); // Clear data for the old song
        OnPropertyChanged(nameof(WaveformRenderData));

        if (_currentSongForWaveform != null && _isPanelVisible)
        {
            Debug.WriteLine($"[WaveformDisplayVM] Current song changed to '{_currentSongForWaveform.Title}' and panel is visible. Triggering load.");
            _ = LoadWaveformInternalAsync(_currentSongForWaveform);
        }
        else if (_currentSongForWaveform == null)
        {
            IsWaveformLoading = false; // Ensure loading stops if song becomes null
            Debug.WriteLine("[WaveformDisplayVM] Current song is null. Cleared waveform and loading state.");
        }
        else
        {
            Debug.WriteLine($"[WaveformDisplayVM] Current song changed to '{_currentSongForWaveform.Title}', but panel not visible. Load deferred.");
        }
    }

    public void SetPanelVisibility(bool isVisible)
    {
        _isPanelVisible = isVisible;
        Debug.WriteLine($"[WaveformDisplayVM] Panel visibility set to: {isVisible}");
        if (_isPanelVisible && _currentSongForWaveform != null && !WaveformRenderData.Any() && !IsWaveformLoading)
        {
            Debug.WriteLine($"[WaveformDisplayVM] Panel now visible for song '{_currentSongForWaveform.Title}'. Triggering waveform load.");
            _ = LoadWaveformInternalAsync(_currentSongForWaveform);
        }
        else if (!_isPanelVisible)
        {
            // Optionally, clear waveform data or cancel loading when panel is hidden
            // WaveformRenderData.Clear();
            // OnPropertyChanged(nameof(WaveformRenderData));
            // IsWaveformLoading = false; // If a load was in progress, it will complete but not update UI if song changes.
            // For now, just stop triggering new loads.
            Debug.WriteLine($"[WaveformDisplayVM] Panel hidden. Waveform loading deferred if song changes or panel re-opens.");
        }
    }

    private async Task LoadWaveformInternalAsync(Song songToLoad)
    {
        if (string.IsNullOrEmpty(songToLoad.FilePath))
        {
            Debug.WriteLine($"[WaveformDisplayVM] LoadWaveformInternalAsync skipped: Invalid path for song '{songToLoad.Title}'.");
            return;
        }

        // Defensive check: If already loading for this exact song instance, don't restart.
        // This might happen if SetPanelVisibility and CurrentSong change rapidly.
        if (IsWaveformLoading && _currentSongForWaveform == songToLoad)
        {
            Debug.WriteLine($"[WaveformDisplayVM] Already loading waveform for '{songToLoad.Title}'. Skipping redundant load request.");
            return;
        }

        IsWaveformLoading = true;
        WaveformRenderData.Clear(); // Clear previous data before loading new
        OnPropertyChanged(nameof(WaveformRenderData));

        try
        {
            Debug.WriteLine($"[WaveformDisplayVM] Requesting waveform from service for: {songToLoad.Title}");
            // Target points for the waveform control
            int targetPoints = 1000;
            var points = await _waveformService.GetWaveformAsync(songToLoad.FilePath, targetPoints);

            // Critical check: Ensure the song context hasn't changed *during* the async load
            // and that the panel is still meant to be visible.
            if (_currentSongForWaveform == songToLoad && _isPanelVisible)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    WaveformRenderData.Clear(); // Ensure it's empty before adding new points
                    foreach (var p in points)
                    {
                        WaveformRenderData.Add(p);
                    }
                    OnPropertyChanged(nameof(WaveformRenderData));
                    Debug.WriteLine($"[WaveformDisplayVM] Waveform loaded and UI updated for: {songToLoad.Title}, {points.Count} points.");
                });
            }
            else
            {
                Debug.WriteLine($"[WaveformDisplayVM] Waveform for '{songToLoad.Title}' loaded, but context changed (current song: '{_currentSongForWaveform?.Title}', panel visible: {_isPanelVisible}). Discarding result.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WaveformDisplayVM] CRITICAL Error loading waveform for '{songToLoad.Title}': {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                WaveformRenderData.Clear(); // Clear on error
                OnPropertyChanged(nameof(WaveformRenderData));
            });
        }
        finally
        {
            // Ensure IsWaveformLoading is set to false only if this load operation was for the *still current* song
            // or if no song is current anymore.
            if (_currentSongForWaveform == songToLoad || _currentSongForWaveform == null)
            {
                IsWaveformLoading = false;
            }
            else
            {
                Debug.WriteLine($"[WaveformDisplayVM] Waveform load finished for '{songToLoad.Title}', but current song is '{_currentSongForWaveform?.Title}'. IsWaveformLoading might be true due to a new load.");
            }
        }
    }
}