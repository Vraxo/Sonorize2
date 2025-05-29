using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Sonorize.Models;

namespace Sonorize.ViewModels.LibraryManagement;

public class SongListManager : ViewModelBase
{
    private readonly SongFilteringService _songFilteringService;
    private List<Song> _allSongs = new(); // Master list

    public ObservableCollection<Song> FilteredSongs { get; } = new();

    public Song? SelectedSong
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                // Consumers (like TrackNavigationManager via LibraryViewModel)
                // will be notified of this change.
            }
        }
    }

    public SongListManager(SongFilteringService songFilteringService)
    {
        _songFilteringService = songFilteringService ?? throw new ArgumentNullException(nameof(songFilteringService));
    }

    public void SetAllSongs(IEnumerable<Song> songs)
    {
        _allSongs = new List<Song>(songs);
        // Typically, after setting all songs, a filter application would follow.
        // This can be triggered by the caller (LibraryViewModel).
        OnPropertyChanged(nameof(AllSongsCount)); // For any internal/debug use
    }

    public void ClearAllSongs()
    {
        _allSongs.Clear();
        FilteredSongs.Clear();
        SelectedSong = null; // This will notify
        OnPropertyChanged(nameof(AllSongsCount));
    }

    public int AllSongsCount => _allSongs.Count;

    public void ApplyFilter(string? searchQuery, ArtistViewModel? selectedArtist, AlbumViewModel? selectedAlbum)
    {
        var currentSelectedSongBeforeFilter = SelectedSong;

        FilteredSongs.Clear();
        var filtered = _songFilteringService.ApplyFilter(
            _allSongs,
            searchQuery,
            selectedArtist,
            selectedAlbum);

        foreach (var song in filtered)
        {
            FilteredSongs.Add(song);
        }

        // Preserve selection if possible
        if (currentSelectedSongBeforeFilter != null && FilteredSongs.Contains(currentSelectedSongBeforeFilter))
        {
            SelectedSong = currentSelectedSongBeforeFilter; // No change notification if it's the same instance
        }
        else if (currentSelectedSongBeforeFilter != null)
        {
            Debug.WriteLine($"[SongListManager] Selected song '{currentSelectedSongBeforeFilter.Title}' is no longer in the filtered list. Clearing selection.");
            SelectedSong = null; // Notify that selection is cleared
        }
        else if (SelectedSong != null) // If there was a selection but it's no longer valid (e.g. currentSelectedSongBeforeFilter was null, but SelectedSong somehow had a value from previous invalid state)
        {
            SelectedSong = null; // Ensure selection is cleared and notified
        }
        // If currentSelectedSongBeforeFilter was null and SelectedSong is also null, no change, no notification.

        OnPropertyChanged(nameof(FilteredSongs)); // Notify that the collection content has changed (though individual adds also notify)
    }

    // Provides access to the master list for services like ArtistAlbumCollectionManager
    public IReadOnlyList<Song> GetAllSongsReadOnly() => _allSongs.AsReadOnly();
}