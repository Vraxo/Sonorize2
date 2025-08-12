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
        IEnumerable<Song> songsToFilter = allSongs;

        // Priority:
        // 1. If a playlist is selected, show its songs.
        // 2. Else if an album is selected, filter by album (and its artist).
        // 3. Else if an artist is selected, filter by artist.
        // 4. Else if a search query is present, filter by query.
        // 5. Else, show all songs (after ordering).

        bool specificPlaylistSelected = selectedPlaylist is not null;
        bool specificAlbumSelected = selectedAlbum?.Title is not null && selectedAlbum.Artist is not null;
        bool specificArtistSelected = selectedArtist?.Name is not null;

        if (specificPlaylistSelected)
        {
            // Playlist songs are already in order. Don't sort them.
            return selectedPlaylist!.PlaylistModel.Songs;
        }
        else if (specificAlbumSelected)
        {
            songsToFilter = songsToFilter.Where(s =>
                (s.Album?.Equals(selectedAlbum!.Title, StringComparison.OrdinalIgnoreCase) ?? false) &&
                (s.Artist?.Equals(selectedAlbum!.Artist, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        else if (specificArtistSelected)
        {
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
