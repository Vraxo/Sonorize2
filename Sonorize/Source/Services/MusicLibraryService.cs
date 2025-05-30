using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Sonorize.Models;

namespace Sonorize.Services;

public class MusicLibraryService
{
    private readonly LoopDataService _loopDataService;
    private readonly ThumbnailService _thumbnailService;
    private readonly SongFactory _songFactory;
    private readonly MusicDirectoryScanner _directoryScanner; // Added MusicDirectoryScanner

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

    // Callback for when ThumbnailService has processed a thumbnail (called by MusicDirectoryScanner via ThumbnailService)
    private void HandleThumbnailReady(Song song, Bitmap? loadedThumbnail)
    {
        // This callback is invoked on the UI thread by ThumbnailService
        if (loadedThumbnail != null)
        {
            song.Thumbnail = loadedThumbnail;
        }
        SongThumbnailUpdated?.Invoke(song);
    }
}