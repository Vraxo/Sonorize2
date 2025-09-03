using System.Linq;
using Sonorize.Models;
using Sonorize.Services; // For AppSettings in case it's needed indirectly

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryStatusTextGenerator
{
    public string GenerateStatusText(
        bool isLoadingLibrary,
        int allSongsCount,
        int filteredSongsCount,
        ArtistViewModel? selectedArtist,
        AlbumViewModel? selectedAlbum,
        PlaylistViewModel? selectedPlaylist,
        string? searchQuery,
        SettingsService settingsService)
    {
        if (isLoadingLibrary)
        {
            return "Loading library...";
        }

        if (allSongsCount == 0)
        {
            AppSettings settings = settingsService.LoadSettings();
            if (!settings.General.MusicDirectories.Any())
            {
                return "Library empty. Add directories via File menu.";
            }
            else
            {
                return "No songs found in configured directories.";
            }
        }

        // Playlist selection takes precedence for status text
        if (selectedPlaylist?.Name is not null)
        {
            return $"Showing playlist '{selectedPlaylist.Name}': {filteredSongsCount} songs.";
        }
        // Album selection takes precedence for status text
        if (selectedAlbum?.Title is not null && selectedAlbum.Artist is not null)
        {
            return $"Showing songs from {selectedAlbum.Title} by {selectedAlbum.Artist}: {filteredSongsCount} of {allSongsCount} total songs.";
        }
        // Then artist selection
        else if (selectedArtist?.Name is not null)
        {
            return $"Showing songs by {selectedArtist.Name}: {filteredSongsCount} of {allSongsCount} total songs.";
        }
        else if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            return $"{filteredSongsCount} of {allSongsCount} songs matching search.";
        }
        else // No specific view, no search query - showing all songs
        {
            return $"{allSongsCount} songs in library.";
        }
    }
}