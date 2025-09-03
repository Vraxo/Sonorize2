using System;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Threading;
using Sonorize.ViewModels;

namespace Sonorize.Services;

public class LibraryPlaybackLinkService : IDisposable
{
    private readonly LibraryViewModel _libraryViewModel;
    private readonly PlaybackService _playbackService;
    private readonly PlaybackViewModel _playbackViewModel; // To observe HasCurrentSong easily

    public LibraryPlaybackLinkService(
        LibraryViewModel libraryViewModel,
        PlaybackService playbackService,
        PlaybackViewModel playbackViewModel)
    {
        _libraryViewModel = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _playbackViewModel = playbackViewModel ?? throw new ArgumentNullException(nameof(playbackViewModel));

        SubscribeToEvents();
        Debug.WriteLine("[LibraryPlaybackLinkService] Initialized and subscribed to events.");
    }

    private void SubscribeToEvents()
    {
        _libraryViewModel.PropertyChanged += OnLibraryViewModelPropertyChanged;
        _playbackViewModel.PropertyChanged += OnPlaybackViewModelPropertyChanged;
    }

    private void UnsubscribeFromEvents()
    {
        _libraryViewModel.PropertyChanged -= OnLibraryViewModelPropertyChanged;
        _playbackViewModel.PropertyChanged -= OnPlaybackViewModelPropertyChanged;
    }

    private void OnLibraryViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LibraryViewModel.SelectedSong))
        {
            return;
        }

        // Capture the value IMMEDIATELY from the source of the event.
        var selectedSongAtEventTime = (sender as LibraryViewModel)?.SelectedSong;

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Use the captured value, not the potentially changed value from the view model property.
            var songToPlay = selectedSongAtEventTime;

            Debug.WriteLine($"[LibraryPlaybackLinkService] Library.SelectedSong changed event processed. Captured song: {songToPlay?.Title ?? "null"}. (Current VM song is: {_libraryViewModel.SelectedSong?.Title ?? "null"})");

            if (songToPlay is not null && _playbackService.CurrentSong != songToPlay)
            {
                Debug.WriteLine($"[LibraryPlaybackLinkService] Playing captured song '{songToPlay.Title}'.");
                _playbackService.Play(songToPlay);
            }
            else if (songToPlay is not null && _playbackService.CurrentSong == songToPlay)
            {
                Debug.WriteLine($"[LibraryPlaybackLinkService] Captured song '{songToPlay.Title}' is already playing. No action needed.");
            }
            else if (songToPlay == null)
            {
                Debug.WriteLine("[LibraryPlaybackLinkService] Captured song was null. No Play call needed.");
            }
        });
    }

    private void OnPlaybackViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlaybackViewModel.HasCurrentSong)) // Or CurrentSong
        {
            return;
        }

        // This logic was causing a race condition. When a new song was selected,
        // the old song would stop, HasCurrentSong would briefly become false,
        // and this would immediately nullify the new selection before it could play.
        // By removing it, the UI selection is now independent of the playback state, which is more robust.
    }

    public void Dispose()
    {
        UnsubscribeFromEvents();
        Debug.WriteLine("[LibraryPlaybackLinkService] Disposed and unsubscribed from events.");
        GC.SuppressFinalize(this);
    }

    ~LibraryPlaybackLinkService()
    {
        Dispose();
    }
}