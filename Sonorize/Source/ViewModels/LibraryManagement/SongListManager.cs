using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using Sonorize.Models;
using Sonorize.ViewModels.LibraryManagement;

namespace Sonorize.ViewModels.LibraryManagement;

public enum SortProperty { Title, Artist, Album, Duration, DateAdded, PlayCount }
public enum SortDirection { Ascending, Descending }

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
        Debug.WriteLine($"[SongListManager] SetAllSongs completed. Master list now contains {_allSongs.Count} songs.");
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

    public void ApplyFilter(string? searchQuery, ArtistViewModel? selectedArtist, AlbumViewModel? selectedAlbum, PlaylistViewModel? selectedPlaylist, SortProperty sortBy, SortDirection sortDirection, LibraryViewOptionsViewModel viewOptions)
    {
        Debug.WriteLine($"[SongListManager.ApplyFilter] Starting filter. AllSongs count: {AllSongsCount}, Query: '{searchQuery}', Artist: '{selectedArtist?.Name}', Album: '{selectedAlbum?.Title}'");
        var currentSelectedSongBeforeFilter = SelectedSong;

        FilteredSongs.Clear();
        var filteredResults = _songFilteringService.ApplyFilter(
            _allSongs,
            searchQuery,
            selectedArtist,
            selectedAlbum,
            selectedPlaylist);

        int filteredCount = filteredResults.Count();
        Debug.WriteLine($"[SongListManager.ApplyFilter] After initial filter: {filteredCount} songs.");

        var sortedResults = SortSongs(filteredResults, sortBy, sortDirection);

        Debug.WriteLine($"[SongListManager.ApplyFilter] After sorting: {sortedResults.Count} songs. Now populating FilteredSongs collection.");

        // Set the index and view options for each song in the view for performant bindings.
        for (int i = 0; i < sortedResults.Count; i++)
        {
            sortedResults[i].IndexInView = i;
            sortedResults[i].ViewOptions = viewOptions;
        }

        foreach (var song in sortedResults)
        {
            FilteredSongs.Add(song);
        }

        Debug.WriteLine($"[SongListManager.ApplyFilter] Finished populating. FilteredSongs.Count: {FilteredSongs.Count}");


        // Preserve selection if possible
        if (currentSelectedSongBeforeFilter is not null && FilteredSongs.Contains(currentSelectedSongBeforeFilter))
        {
            SelectedSong = currentSelectedSongBeforeFilter; // No change notification if it's the same instance
        }
        else if (currentSelectedSongBeforeFilter is not null)
        {
            Debug.WriteLine($"[SongListManager] Selected song '{currentSelectedSongBeforeFilter.Title}' is no longer in the filtered list. Clearing selection.");
            SelectedSong = null; // Notify that selection is cleared
        }
        else if (SelectedSong is not null) // If there was a selection but it's no longer valid (e.g. currentSelectedSongBeforeFilter was null, but SelectedSong somehow had a value from previous invalid state)
        {
            SelectedSong = null; // Ensure selection is cleared and notified
        }
        // If currentSelectedSongBeforeFilter was null and SelectedSong is also null, no change, no notification.
        OnPropertyChanged(nameof(FilteredSongs)); // Notify that the collection content has changed (though individual adds also notify)
    }

    private List<Song> SortSongs(IEnumerable<Song> songs, SortProperty sortBy, SortDirection sortDirection)
    {
        IOrderedEnumerable<Song> ordered;
        if (sortDirection == SortDirection.Ascending)
        {
            ordered = sortBy switch
            {
                SortProperty.Artist => songs.OrderBy(s => s.Artist, StringComparer.OrdinalIgnoreCase),
                SortProperty.Album => songs.OrderBy(s => s.Album, StringComparer.OrdinalIgnoreCase),
                SortProperty.Duration => songs.OrderBy(s => s.Duration),
                SortProperty.DateAdded => songs.OrderBy(s => s.DateAdded),
                SortProperty.PlayCount => songs.OrderBy(s => s.PlayCount),
                _ => songs.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase),
            };
        }
        else // Descending
        {
            ordered = sortBy switch
            {
                SortProperty.Artist => songs.OrderByDescending(s => s.Artist, StringComparer.OrdinalIgnoreCase),
                SortProperty.Album => songs.OrderByDescending(s => s.Album, StringComparer.OrdinalIgnoreCase),
                SortProperty.Duration => songs.OrderByDescending(s => s.Duration),
                SortProperty.DateAdded => songs.OrderByDescending(s => s.DateAdded),
                SortProperty.PlayCount => songs.OrderByDescending(s => s.PlayCount),
                _ => songs.OrderByDescending(s => s.Title, StringComparer.OrdinalIgnoreCase),
            };
        }
        // Add secondary sort for consistency when primary keys are equal
        return ordered.ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }


    // Provides access to the master list for services like ArtistAlbumCollectionManager
    public IReadOnlyList<Song> GetAllSongsReadOnly() => _allSongs.AsReadOnly();
}