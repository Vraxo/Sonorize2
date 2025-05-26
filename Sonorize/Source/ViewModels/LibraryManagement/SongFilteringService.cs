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
        AlbumViewModel? selectedAlbum)
    {
        IEnumerable<Song> songsToFilter = allSongs;

        // Priority:
        // 1. If an album is selected, filter by album (and its artist).
        // 2. Else if an artist is selected, filter by artist.
        // 3. Else if a search query is present, filter by query.
        // 4. Else, show all songs (after ordering).

        bool specificAlbumSelected = selectedAlbum?.Title != null && selectedAlbum.Artist != null;
        bool specificArtistSelected = selectedArtist?.Name != null;

        if (specificAlbumSelected)
        {
            // When an album is selected, the searchQuery is often set to the album title.
            // We should primarily filter by the album's identity.
            songsToFilter = songsToFilter.Where(s =>
                (s.Album?.Equals(selectedAlbum!.Title, StringComparison.OrdinalIgnoreCase) ?? false) &&
                (s.Artist?.Equals(selectedAlbum!.Artist, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        else if (specificArtistSelected)
        {
            // When an artist is selected, the searchQuery is often set to the artist name.
            // Filter by the artist's identity.
            songsToFilter = songsToFilter.Where(s =>
                s.Artist?.Equals(selectedArtist!.Name, StringComparison.OrdinalIgnoreCase) ?? false);
        }
        else if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string query = searchQuery.ToLowerInvariant().Trim();
            songsToFilter = songsToFilter.Where(s =>
                (s.Title?.ToLowerInvariant().Contains(query, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                (s.Artist?.ToLowerInvariant().Contains(query, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                (s.Album?.ToLowerInvariant().Contains(query, StringComparison.InvariantCultureIgnoreCase) ?? false));
        }

        return songsToFilter.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);
    }
}