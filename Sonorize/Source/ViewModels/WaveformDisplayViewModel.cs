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
    private readonly AsyncWaveformLoader _waveformLoader; // New helper class instance
    private Song? _currentSongForWaveform; // Song currently targeted for display
    private bool _isPanelVisible;

    // Properties now proxy to AsyncWaveformLoader
    public ObservableCollection<WaveformPoint> WaveformRenderData => _waveformLoader.WaveformRenderData;
    public bool IsWaveformLoading => _waveformLoader.IsLoading;

    public WaveformDisplayViewModel(PlaybackService playbackService, WaveformService waveformService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _waveformLoader = new AsyncWaveformLoader(waveformService ?? throw new ArgumentNullException(nameof(waveformService)));

        // Subscribe to PropertyChanged on the loader to forward IsLoading changes
        _waveformLoader.PropertyChanged += WaveformLoader_PropertyChanged;

        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;
        UpdateCurrentSongForWaveform(_playbackService.CurrentSong); // Initial sync
    }

    private void WaveformLoader_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AsyncWaveformLoader.IsLoading))
        {
            OnPropertyChanged(nameof(IsWaveformLoading)); // Notify that our proxied property changed
        }
        // WaveformRenderData is an ObservableCollection, changes within it will propagate automatically to bindings.
        // If the entire collection instance on the loader were to change (it doesn't in this design),
        // then we'd need to forward that too.
    }

    private async void PlaybackService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackService.CurrentSong))
        {
            // Ensure UI-related updates from service are dispatched if not already on UI thread
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
            // This handles cases where the panel might have been hidden and then reshown for the same song.
            if (_isPanelVisible && newSong is not null && !WaveformRenderData.Any() && !IsWaveformLoading)
            {
                Debug.WriteLine($"[WaveformDisplayVM] Panel visible for same song '{newSong.Title}', but no waveform. Triggering load.");
                _ = TryLoadWaveformAsync(newSong);
            }
            return;
        }

        _currentSongForWaveform = newSong;

        if (_currentSongForWaveform is not null && _isPanelVisible)
        {
            Debug.WriteLine($"[WaveformDisplayVM] Current song changed to '{_currentSongForWaveform.Title}' and panel is visible. Triggering load.");
            _ = TryLoadWaveformAsync(_currentSongForWaveform);
        }
        else
        {
            // If no song, or panel not visible, clear current waveform data and stop any loading.
            _waveformLoader.ClearDataAndState();
            OnPropertyChanged(nameof(WaveformRenderData)); // Ensure UI updates if collection was cleared
            Debug.WriteLine($"[WaveformDisplayVM] Current song is '{_currentSongForWaveform?.Title ?? "null"}' and panel visible: {_isPanelVisible}. Load deferred or data cleared.");
        }
    }

    public void SetPanelVisibility(bool isVisible)
    {
        if (_isPanelVisible == isVisible) return; // No change

        _isPanelVisible = isVisible;
        Debug.WriteLine($"[WaveformDisplayVM] Panel visibility set to: {isVisible}");

        if (_isPanelVisible && _currentSongForWaveform is not null)
        {
            // Panel became visible for the current song. If no data and not loading, start load.
            if (!WaveformRenderData.Any() && !IsWaveformLoading)
            {
                Debug.WriteLine($"[WaveformDisplayVM] Panel now visible for song '{_currentSongForWaveform.Title}'. Triggering waveform load.");
                _ = TryLoadWaveformAsync(_currentSongForWaveform);
            }
        }
        else if (!_isPanelVisible)
        {
            // Panel hidden. Existing logic in UpdateCurrentSongForWaveform handles clearing data if song also changes.
            // If only visibility changes, current design keeps data but won't load new.
            // Optionally, could clear data: _waveformLoader.ClearDataAndState(); OnPropertyChanged(nameof(WaveformRenderData));
            Debug.WriteLine($"[WaveformDisplayVM] Panel hidden. Waveform loading deferred if song changes or panel re-opens.");
        }
    }

    private async Task TryLoadWaveformAsync(Song songToLoad)
    {
        // The context passed to RequestLoadAsync uses the current state of the ViewModel
        await _waveformLoader.RequestLoadAsync(songToLoad, 1000, _currentSongForWaveform, _isPanelVisible);
    }

    // Dispose method if AsyncWaveformLoader needs disposal or to unsubscribe from its events
    // For now, only unsubscribing from _waveformLoader.PropertyChanged.
    // If AsyncWaveformLoader implemented IDisposable, call _waveformLoader.Dispose() here.
    protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsWaveformLoading) && IsWaveformLoading == false && _playbackService.CurrentSong != _currentSongForWaveform)
        {
            // A previous load finished, but the song context has since changed.
            // Trigger a new load for the actual current song if conditions are met.
            if (_currentSongForWaveform is not null && _isPanelVisible)
            {
                Debug.WriteLine($"[WaveformDisplayVM] Post-load check: Song context changed from '{_playbackService.CurrentSong?.Title}' to '{_currentSongForWaveform.Title}'. Re-evaluating load for {_currentSongForWaveform.Title}.");
                _ = TryLoadWaveformAsync(_currentSongForWaveform);
            }
        }
    }

    public void Dispose()
    {
        _playbackService.PropertyChanged -= PlaybackService_PropertyChanged;
        _waveformLoader.PropertyChanged -= WaveformLoader_PropertyChanged;
        // If _waveformLoader were IDisposable: _waveformLoader.Dispose();
    }
}