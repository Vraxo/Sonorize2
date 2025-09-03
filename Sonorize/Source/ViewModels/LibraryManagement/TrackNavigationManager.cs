using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Sonorize.Models;

namespace Sonorize.ViewModels.LibraryManagement;

public class TrackNavigationManager : ViewModelBase
{
    private readonly LibraryViewModel _libraryViewModel;

    public ICommand PreviousTrackCommand { get; }
    public ICommand NextTrackCommand { get; }

    public TrackNavigationManager(LibraryViewModel libraryViewModel)
    {
        _libraryViewModel = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));

        // Listen to changes in the source of truth (LibraryViewModel) to update command states.
        _libraryViewModel.FilteredSongs.CollectionChanged += (s, e) => RaiseCanExecuteChangedForAllCommands();
        _libraryViewModel.PropertyChanged += OnLibraryViewModelPropertyChanged;

        PreviousTrackCommand = new RelayCommand(ExecutePreviousTrack, CanExecutePreviousTrack);
        NextTrackCommand = new RelayCommand(ExecuteNextTrack, CanExecuteNextTrack);
    }

    private void OnLibraryViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LibraryViewModel.SelectedSong))
        {
            RaiseCanExecuteChangedForAllCommands();
        }
    }

    private void ExecutePreviousTrack(object? parameter)
    {
        var songs = _libraryViewModel.FilteredSongs;
        var current = _libraryViewModel.SelectedSong;
        if (current == null || !songs.Any()) return;

        int currentIndex = songs.IndexOf(current);
        if (currentIndex > 0)
        {
            _libraryViewModel.SelectedSong = songs[currentIndex - 1];
            Debug.WriteLine($"[TrackNavManager] Moved to previous track: {_libraryViewModel.SelectedSong.Title}");
        }
        else
        {
            Debug.WriteLine("[TrackNavManager] Already at the first track.");
        }
    }

    private bool CanExecutePreviousTrack(object? parameter)
    {
        var songs = _libraryViewModel.FilteredSongs;
        var current = _libraryViewModel.SelectedSong;
        if (current == null || !songs.Any()) return false;
        return songs.IndexOf(current) > 0;
    }

    private void ExecuteNextTrack(object? parameter)
    {
        var songs = _libraryViewModel.FilteredSongs;
        var current = _libraryViewModel.SelectedSong;
        if (current == null || !songs.Any()) return;

        int currentIndex = songs.IndexOf(current);
        if (currentIndex < songs.Count - 1 && currentIndex != -1)
        {
            _libraryViewModel.SelectedSong = songs[currentIndex + 1];
            Debug.WriteLine($"[TrackNavManager] Moved to next track: {_libraryViewModel.SelectedSong.Title}");
        }
        else if (currentIndex != -1)
        {
            Debug.WriteLine("[TrackNavManager] Already at the last track.");
        }
        else
        {
            Debug.WriteLine("[TrackNavManager] Selected song not found in filtered list.");
        }
    }

    private bool CanExecuteNextTrack(object? parameter)
    {
        var songs = _libraryViewModel.FilteredSongs;
        var current = _libraryViewModel.SelectedSong;
        if (current == null || !songs.Any()) return false;

        int currentIndex = songs.IndexOf(current);
        return currentIndex != -1 && currentIndex < songs.Count - 1;
    }

    private void RaiseCanExecuteChangedForAllCommands()
    {
        (PreviousTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}