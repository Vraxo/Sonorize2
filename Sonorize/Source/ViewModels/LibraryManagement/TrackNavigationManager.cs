using System;
using System.Collections.ObjectModel; // For ObservableCollection
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using Sonorize.Models;

namespace Sonorize.ViewModels.LibraryManagement;

public class TrackNavigationManager : ViewModelBase // Inherit for RelayCommand's RaiseCanExecuteChanged if needed
{
    private readonly ObservableCollection<Song> _filteredSongs;
    private Song? _selectedSong;

    public Song? SelectedSong
    {
        get => _selectedSong;
        set
        {
            // This setter is crucial. It's what the LibraryViewModel will call.
            // Or, LibraryViewModel could expose its SelectedSong and FilteredSongs
            // and this manager could observe them. For direct control, LibraryViewModel calls this.
            if (SetProperty(ref _selectedSong, value))
            {
                RaiseCanExecuteChangedForAllCommands();
            }
        }
    }

    public ICommand PreviousTrackCommand { get; }
    public ICommand NextTrackCommand { get; }

    public TrackNavigationManager(ObservableCollection<Song> filteredSongs)
    {
        _filteredSongs = filteredSongs ?? throw new ArgumentNullException(nameof(filteredSongs));
        _filteredSongs.CollectionChanged += (s, e) => RaiseCanExecuteChangedForAllCommands();

        PreviousTrackCommand = new RelayCommand(ExecutePreviousTrack, CanExecutePreviousTrack);
        NextTrackCommand = new RelayCommand(ExecuteNextTrack, CanExecuteNextTrack);
    }

    // This method will be called by LibraryViewModel when its SelectedSong changes
    public void UpdateSelectedSong(Song? newSelectedSong)
    {
        if (_selectedSong != newSelectedSong)
        {
            _selectedSong = newSelectedSong; // Update internal state
            RaiseCanExecuteChangedForAllCommands(); // Update command states
        }
    }


    private void ExecutePreviousTrack(object? parameter)
    {
        if (_selectedSong == null || !_filteredSongs.Any()) return;
        int currentIndex = _filteredSongs.IndexOf(_selectedSong);
        if (currentIndex > 0)
        {
            SelectedSong = _filteredSongs[currentIndex - 1]; // This will trigger property changed & command updates
            Debug.WriteLine($"[TrackNavManager] Moved to previous track: {SelectedSong.Title}");
        }
        else
        {
            Debug.WriteLine("[TrackNavManager] Already at the first track.");
        }
    }

    private bool CanExecutePreviousTrack(object? parameter)
    {
        if (_selectedSong == null || !_filteredSongs.Any()) return false;
        return _filteredSongs.IndexOf(_selectedSong) > 0;
    }

    private void ExecuteNextTrack(object? parameter)
    {
        if (_selectedSong == null || !_filteredSongs.Any()) return;
        int currentIndex = _filteredSongs.IndexOf(_selectedSong);
        if (currentIndex < _filteredSongs.Count - 1 && currentIndex != -1)
        {
            SelectedSong = _filteredSongs[currentIndex + 1]; // This will trigger property changed & command updates
            Debug.WriteLine($"[TrackNavManager] Moved to next track: {SelectedSong.Title}");
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
        if (_selectedSong == null || !_filteredSongs.Any()) return false;
        int currentIndex = _filteredSongs.IndexOf(_selectedSong);
        return currentIndex != -1 && currentIndex < _filteredSongs.Count - 1;
    }

    private void RaiseCanExecuteChangedForAllCommands()
    {
        (PreviousTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    // Optional: Expose an event if LibraryViewModel needs to react to selection changes from this manager
    // public event Action<Song?>? ManagedSelectionChanged;
}