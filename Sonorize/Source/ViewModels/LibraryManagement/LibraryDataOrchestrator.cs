using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryDataOrchestrator
{
    private readonly MusicLibraryService _musicLibraryService;
    private readonly ArtistAlbumCollectionManager _artistAlbumManager;
    private readonly SettingsService _settingsService;
    private readonly AutoPlaylistManager _autoPlaylistManager;

    public LibraryDataOrchestrator(
        MusicLibraryService musicLibraryService,
        ArtistAlbumCollectionManager artistAlbumManager,
        SettingsService settingsService,
        AutoPlaylistManager autoPlaylistManager)
    {
        _musicLibraryService = musicLibraryService ?? throw new ArgumentNullException(nameof(musicLibraryService));
        _artistAlbumManager = artistAlbumManager ?? throw new ArgumentNullException(nameof(artistAlbumManager));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _autoPlaylistManager = autoPlaylistManager ?? throw new ArgumentNullException(nameof(autoPlaylistManager));
    }

    public async Task<(List<Song> Songs, List<Playlist> Playlists)> LoadAndProcessLibraryDataAsync(Action<string> statusUpdateCallback)
    {
        var rawSongs = new List<Song>();

        AppSettings settings = _settingsService.LoadSettings();
        if (!settings.MusicDirectories.Any())
        {
            await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback("No music directories configured."));
            return (rawSongs, new List<Playlist>());
        }

        try
        {
            // Phase 1: Load raw song metadata and thumbnails
            await _musicLibraryService.LoadMusicFromDirectoriesAsync(
                settings.MusicDirectories,
                song =>
                {
                    rawSongs.Add(song);
                },
                status => Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback(status))
            );

            // Phase 2: Load Playlists using the fully gathered rawSongs list
            await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Found {rawSongs.Count} songs. Scanning for playlists..."));
            var filePlaylists = await _musicLibraryService.LoadPlaylistsAsync(settings.MusicDirectories, rawSongs);

            // Phase 2.5: Generate Auto-Playlists
            var autoPlaylists = _autoPlaylistManager.GenerateInitialAutoPlaylists(rawSongs);

            // Combine all playlists, auto-playlists first
            var allPlaylists = autoPlaylists.Concat(filePlaylists).ToList();

            // Phase 3: Populate Artist and Album collections
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _artistAlbumManager.PopulateCollections(rawSongs);
            });

            return (rawSongs, allPlaylists);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LibraryDataOrchestrator] Error loading and processing library data: {ex}");
            await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback("Error loading music library."));
            return (new List<Song>(), new List<Playlist>());
        }
    }
}