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
        string? searchQuery,
        SettingsService settingsService) // Pass SettingsService to check for configured directories
    {
        if (isLoadingLibrary)
        {
            // During loading, LibraryViewModel's LibraryStatusText is updated by MusicLibraryService callbacks.
            // This generator is primarily for post-loading or idle states.
            // However, if called during loading, we might return a generic loading message
            // or expect the caller (LibraryViewModel) to handle this case.
            // For now, assume this is called when not actively in the middle of the LoadMusicFromDirectoriesAsync song processing loop.
            return "Loading library..."; // Or whatever the current LibraryStatusText is if passed in.
        }

        if (allSongsCount == 0)
        {
            AppSettings settings = settingsService.LoadSettings();
            if (!settings.MusicDirectories.Any())
            {
                return "Library empty. Add directories via File menu.";
            }
            else
            {
                return "No songs found in configured directories.";
            }
        }

        // Album selection takes precedence for status text
        if (selectedAlbum?.Title != null && selectedAlbum.Artist != null)
        {
            return $"Showing songs from {selectedAlbum.Title} by {selectedAlbum.Artist}: {filteredSongsCount} of {allSongsCount} total songs.";
        }
        // Then artist selection
        else if (selectedArtist?.Name != null)
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