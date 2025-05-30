using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Sonorize.Models;

namespace Sonorize.Services;

public class MusicDirectoryScanner
{
    private readonly SongFactory _songFactory;
    private readonly ThumbnailService _thumbnailService;

    public MusicDirectoryScanner(SongFactory songFactory, ThumbnailService thumbnailService)
    {
        _songFactory = songFactory ?? throw new ArgumentNullException(nameof(songFactory));
        _thumbnailService = thumbnailService ?? throw new ArgumentNullException(nameof(thumbnailService));
    }

    public async Task ScanAsync(
        IEnumerable<string> directories,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback,
        Bitmap? defaultIcon,
        Action<Song, Bitmap?> thumbnailReadyCallback)
    {
        Debug.WriteLine("[MusicDirScanner] ScanAsync started.");
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" };
        int totalFilesProcessed = 0;
        int directoriesScanned = 0;

        foreach (string dir in directories)
        {
            int filesProcessedInDir = await ProcessDirectoryAsync(
                dir,
                supportedExtensions,
                songAddedCallback,
                statusUpdateCallback,
                defaultIcon,
                thumbnailReadyCallback
            );

            totalFilesProcessed += filesProcessedInDir;
            directoriesScanned++;

            // Batch update for UI responsiveness, especially with many small directories
            if (directoriesScanned % 5 == 0 || filesProcessedInDir > 0) // Update status if files were found or after every 5 dirs
            {
                await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Processed {totalFilesProcessed} songs from {directoriesScanned} directories..."));
            }
        }

        await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Metadata scan complete. {totalFilesProcessed} songs found. Thumbnails loading in background..."));
        Debug.WriteLine("[MusicDirScanner] ScanAsync finished.");
    }

    private async Task<int> ProcessDirectoryAsync(
        string directoryPath,
        string[] supportedExtensions,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback,
        Bitmap? defaultIcon,
        Action<Song, Bitmap?> thumbnailReadyCallback)
    {
        if (!Directory.Exists(directoryPath))
        {
            Debug.WriteLine($"[MusicDirScanner] Directory not found: {directoryPath}");
            await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Directory not found: {directoryPath}"));
            return 0;
        }

        await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Scanning: {Path.GetFileName(directoryPath)}..."));

        List<string> filesInDir;
        try
        {
            filesInDir = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(f => supportedExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicDirScanner] Error enumerating files in {directoryPath}: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Error scanning {Path.GetFileName(directoryPath)}"));
            return 0;
        }

        int filesProcessedInThisDirectory = 0;
        foreach (var file in filesInDir)
        {
            await ProcessMusicFileAsync(file, defaultIcon, songAddedCallback, thumbnailReadyCallback);
            filesProcessedInThisDirectory++;
        }
        return filesProcessedInThisDirectory;
    }

    private async Task ProcessMusicFileAsync(
        string filePath,
        Bitmap? defaultIcon,
        Action<Song> songAddedCallback,
        Action<Song, Bitmap?> thumbnailReadyCallback)
    {
        Song song = _songFactory.CreateSongFromFile(filePath, defaultIcon);
        await Dispatcher.UIThread.InvokeAsync(() => songAddedCallback(song));
        _thumbnailService.QueueThumbnailRequest(song, thumbnailReadyCallback);
    }
}