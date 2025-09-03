using System;
using System.Collections.Generic;
using System.Linq;
using Sonorize.Models;

namespace Sonorize.ViewModels.LibraryManagement;

public class SongFilteringService
{
    public IEnumerable<Song> ApplyFilter(
    IEnumerable<Song> allSongs,
    string? searchQuery,
    ArtistViewModel? selectedArtist,
    AlbumViewModel? selectedAlbum,
    PlaylistViewModel? selectedPlaylist)
    {
        // Priority 1: Playlist is a special case, it overrides all other filters and has its own sorting.
        if (selectedPlaylist != null)
        {
            return selectedPlaylist.PlaylistModel.Songs;
        }

        IEnumerable<Song> filteredSongs = allSongs;

        // Apply filters hierarchically. An album selection implies an artist selection.
        // A search query is the most general filter.

        // Priority 2: Album
        if (selectedAlbum != null)
        {
            filteredSongs = filteredSongs.Where(s =>
                (s.Album?.Equals(selectedAlbum.Title, StringComparison.OrdinalIgnoreCase) ?? false) &&
                (s.Artist?.Equals(selectedAlbum.Artist, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        // Priority 3: Artist
        else if (selectedArtist?.Name != null)
        {
            filteredSongs = filteredSongs.Where(s =>
                s.Artist?.Equals(selectedArtist.Name, StringComparison.OrdinalIgnoreCase) ?? false);
        }
        // Priority 4: Search Query
        else if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string query = searchQuery.Trim();
            // Use the more performant and correct string.Contains overload with StringComparison
            filteredSongs = filteredSongs.Where(s =>
                (s.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Artist?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Album?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        // If no filters were applied, filteredSongs is still the original 'allSongs' enumerable.
        // The caller (SongListManager) is responsible for sorting the final results.
        return filteredSongs;
    }
}
