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

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            Debug.WriteLine($"[LibraryPlaybackLinkService] Library.SelectedSong changed to: {_libraryViewModel.SelectedSong?.Title ?? "null"}. Instance: {_libraryViewModel.SelectedSong?.GetHashCode() ?? 0}");

            if (_libraryViewModel.SelectedSong is not null && _playbackService.CurrentSong != _libraryViewModel.SelectedSong)
            {
                Debug.WriteLine($"[LibraryPlaybackLinkService] Library.SelectedSong changed to a *different* song ({_libraryViewModel.SelectedSong.Title}) than PlaybackService.CurrentSong ({_playbackService.CurrentSong?.Title ?? "null"}). Calling PlaybackService.Play().");
                _playbackService.Play(_libraryViewModel.SelectedSong);
            }
            else if (_libraryViewModel.SelectedSong is not null && _playbackService.CurrentSong == _libraryViewModel.SelectedSong)
            {
                Debug.WriteLine($"[LibraryPlaybackLinkService] Library.SelectedSong changed but is the SAME song instance as PlaybackService.CurrentSong ({_libraryViewModel.SelectedSong.Title}). No Play call needed here.");
            }
            else if (_libraryViewModel.SelectedSong == null)
            {
                Debug.WriteLine("[LibraryPlaybackLinkService] Library.SelectedSong is null. No Play call needed here.");
            }
        });
    }

    private void OnPlaybackViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlaybackViewModel.HasCurrentSong)) // Or CurrentSong
        {
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_playbackViewModel.HasCurrentSong || _libraryViewModel.SelectedSong == null)
            {
                return;
            }

            Debug.WriteLine("[LibraryPlaybackLinkService] Playback has no current song. Clearing Library selection.");
            _libraryViewModel.SelectedSong = null;
        });
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