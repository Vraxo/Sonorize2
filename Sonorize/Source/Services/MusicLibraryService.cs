using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Sonorize.Models;
using TagLib;

namespace Sonorize.Services;

public class MusicLibraryService
{
    private readonly LoopDataService _loopDataService;
    private readonly ThumbnailService _thumbnailService;
    private readonly SongFactory _songFactory; // Added SongFactory dependency
    private const int UI_UPDATE_BATCH_SIZE = 50;

    public event Action<Song>? SongThumbnailUpdated;


    public MusicLibraryService(LoopDataService loopDataService, ThumbnailService thumbnailService, SongFactory songFactory) // Modified constructor
    {
        _loopDataService = loopDataService ?? throw new ArgumentNullException(nameof(loopDataService));
        _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
        _songFactory = songFactory ?? throw new ArgumentNullException(nameof(songFactory)); // Store dependency
        Debug.WriteLine("[MusicLibService] Constructor called.");
    }

    // Delegated to ThumbnailService
    public Bitmap? GetDefaultThumbnail() => _thumbnailService.GetDefaultThumbnail();


    public async Task LoadMusicFromDirectoriesAsync(
        IEnumerable<string> directories,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback)
    {
        Debug.WriteLine("[MusicLibService] LoadMusicFromDirectoriesAsync");
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" };
        Bitmap? defaultIcon = GetDefaultThumbnail();
        int filesProcessed = 0;

        foreach (string dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Debug.WriteLine($"[LibScan] Directory not found: {dir}");
                await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Directory not found: {dir}"));
                continue;
            }

            await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Scanning: {Path.GetFileName(dir)}..."));

            List<string> filesInDir;
            try
            {
                filesInDir = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                    .Where(f => supportedExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibScan] Error enumerating files: {dir} - {ex.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Error scanning {Path.GetFileName(dir)}"));
                continue;
            }

            foreach (var file in filesInDir)
            {
                // Use SongFactory to create and populate the song object
                Song song = _songFactory.CreateSongFromFile(file, defaultIcon);

                await Dispatcher.UIThread.InvokeAsync(() => songAddedCallback(song));

                // Request thumbnail processing via ThumbnailService
                _thumbnailService.QueueThumbnailRequest(song, HandleThumbnailReady);

                filesProcessed++;
                if (filesProcessed % (UI_UPDATE_BATCH_SIZE * 2) == 0) // Increased batch size for status updates
                {
                    await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Loaded {filesProcessed} songs..."));
                }
            }
        }
        // Final status update after metadata scan, thumbnail loading is now managed by ThumbnailService
        await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Metadata scan complete. {filesProcessed} songs found. Thumbnails loading in background..."));
    }

    // Callback for when ThumbnailService has processed a thumbnail
    private void HandleThumbnailReady(Song song, Bitmap? loadedThumbnail)
    {
        // This callback is invoked on the UI thread by ThumbnailService
        if (loadedThumbnail != null)
        {
            song.Thumbnail = loadedThumbnail; // Update the song's thumbnail if a new one was loaded
        }
        // Even if loadedThumbnail is null (meaning no specific art found, or error),
        // the song.Thumbnail already holds the default icon.
        // We still invoke SongThumbnailUpdated to notify that processing for this song's thumbnail is complete.
        SongThumbnailUpdated?.Invoke(song);
    }
}