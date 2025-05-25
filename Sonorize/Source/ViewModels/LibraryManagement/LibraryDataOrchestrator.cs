using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // Required for ObservableCollection
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryDataOrchestrator
{
    private readonly MusicLibraryService _musicLibraryService;
    private readonly ArtistAlbumCollectionManager _artistAlbumManager;
    private readonly SettingsService _settingsService;

    public LibraryDataOrchestrator(
        MusicLibraryService musicLibraryService,
        ArtistAlbumCollectionManager artistAlbumManager,
        SettingsService settingsService)
    {
        _musicLibraryService = musicLibraryService ?? throw new ArgumentNullException(nameof(musicLibraryService));
        _artistAlbumManager = artistAlbumManager ?? throw new ArgumentNullException(nameof(artistAlbumManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    public async Task<List<Song>> LoadAndProcessLibraryDataAsync(
        Action<string> statusUpdateCallback,
        Action<Song> songAddedToRawListCallback)
    {
        var rawSongs = new List<Song>(); // Temporary list to gather songs from MusicLibraryService

        AppSettings settings = _settingsService.LoadSettings();
        if (!settings.MusicDirectories.Any())
        {
            await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback("No music directories configured."));
            return rawSongs; // Return empty list
        }

        try
        {
            // Phase 1: Load raw song metadata and thumbnails (thumbnails load in background via MusicLibraryService)
            await _musicLibraryService.LoadMusicFromDirectoriesAsync(
                settings.MusicDirectories,
                song =>
                {
                    // This callback is invoked on UI thread by MusicLibraryService
                    rawSongs.Add(song);
                    songAddedToRawListCallback(song); // Notify caller (LibraryViewModel) to add to its _allSongs
                },
                status => Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback(status)) // Pass status updates
            );

            // Phase 2: Populate Artist and Album collections using the fully gathered rawSongs list
            // This is done after all songs are initially processed by MusicLibraryService.
            // The ArtistAlbumCollectionManager operates on the collections passed to its constructor,
            // so this call effectively updates the Artists and Albums collections in LibraryViewModel.
            // Ensure this runs on UI thread if ArtistAlbumCollectionManager modifies UI-bound collections directly.
            // ArtistAlbumCollectionManager is designed to populate ObservableCollections, which should be UI thread safe if modified there.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _artistAlbumManager.PopulateCollections(rawSongs);
            });

            return rawSongs; // Return the populated list of all songs
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LibraryDataOrchestrator] Error loading and processing library data: {ex}");
            await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback("Error loading music library."));
            return new List<Song>(); // Return empty list on error
        }
    }
}