using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Sonorize.Models;

namespace Sonorize.Services;

public class MusicLibraryService
{
    private readonly LoopDataService _loopDataService;
    private readonly ThumbnailService _thumbnailService;
    private readonly SongFactory _songFactory;
    private readonly MusicDirectoryScanner _directoryScanner;
    private readonly PlaylistParserService _playlistParserService = new();

    public event Action<Song>? SongThumbnailUpdated;

    public MusicLibraryService(LoopDataService loopDataService, ThumbnailService thumbnailService, SongFactory songFactory)
    {
        _loopDataService = loopDataService ?? throw new ArgumentNullException(nameof(loopDataService));
        _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
        _songFactory = songFactory ?? throw new ArgumentNullException(nameof(songFactory));
        _directoryScanner = new MusicDirectoryScanner(_songFactory, _thumbnailService); // Initialize scanner
        Debug.WriteLine("[MusicLibService] Constructor called.");
    }

    public Bitmap? GetDefaultThumbnail() => _thumbnailService.GetDefaultThumbnail();

    public async Task LoadMusicFromDirectoriesAsync(
        IEnumerable<string> directories,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback)
    {
        Debug.WriteLine("[MusicLibService] LoadMusicFromDirectoriesAsync called, delegating to MusicDirectoryScanner.");
        Bitmap? defaultIcon = GetDefaultThumbnail();

        await _directoryScanner.ScanAsync(
            directories,
            songAddedCallback,
            statusUpdateCallback,
            defaultIcon,
            HandleThumbnailReady // Pass the local callback method
        );
        Debug.WriteLine("[MusicLibService] LoadMusicFromDirectoriesAsync delegation completed.");
    }

    public async Task<List<Playlist>> LoadPlaylistsAsync(IEnumerable<string> directories, IEnumerable<Song> allSongs)
    {
        return await Task.Run(() =>
        {
            var playlists = new List<Playlist>();
            var allSongsLookup = allSongs.ToDictionary(s => s.FilePath, StringComparer.OrdinalIgnoreCase);

            if (!allSongsLookup.Any())
            {
                Debug.WriteLine("[MusicLibService] No songs loaded, skipping playlist scan.");
                return playlists;
            }

            foreach (var dir in directories)
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        var m3uFiles = Directory.EnumerateFiles(dir, "*.m3u", SearchOption.AllDirectories);
                        foreach (var m3uFile in m3uFiles)
                        {
                            var playlist = _playlistParserService.Parse(m3uFile, allSongsLookup);
                            if (playlist.Songs.Any())
                            {
                                playlists.Add(playlist);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[MusicLibService] Error scanning for playlists in '{dir}': {ex.Message}");
                    }
                }
            }
            Debug.WriteLine($"[MusicLibService] Found {playlists.Count} playlists with songs.");
            return playlists.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
        });
    }

    // Callback for when ThumbnailService has processed a thumbnail (called by MusicDirectoryScanner via ThumbnailService)
    private void HandleThumbnailReady(Song song, Bitmap? loadedThumbnail)
    {
        // This callback is invoked on the UI thread by ThumbnailService
        if (loadedThumbnail is not null)
        {
            song.Thumbnail = loadedThumbnail;
        }
        SongThumbnailUpdated?.Invoke(song);
    }
}
