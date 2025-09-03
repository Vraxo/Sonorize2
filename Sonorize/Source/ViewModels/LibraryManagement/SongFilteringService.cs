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
            // When a playlist is selected, search should apply to its contents.
            var playlistSongs = selectedPlaylist.PlaylistModel.Songs;
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                string query = searchQuery.Trim();
                return playlistSongs.Where(s =>
                    (s.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Artist?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (s.Album?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            }
            return playlistSongs;
        }

        IEnumerable<Song> filteredSongs = allSongs;

        // Apply hierarchical filters first.
        if (selectedAlbum != null)
        {
            filteredSongs = filteredSongs.Where(s =>
                (s.Album?.Equals(selectedAlbum.Title, StringComparison.OrdinalIgnoreCase) ?? false) &&
                (s.Artist?.Equals(selectedAlbum.Artist, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        else if (selectedArtist?.Name != null)
        {
            filteredSongs = filteredSongs.Where(s =>
                s.Artist?.Equals(selectedArtist.Name, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        // Apply search query to the result of the above filters (or to all songs if no filter is active).
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            string query = searchQuery.Trim();
            filteredSongs = filteredSongs.Where(s =>
                (s.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Artist?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (s.Album?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        return filteredSongs;
    }
}