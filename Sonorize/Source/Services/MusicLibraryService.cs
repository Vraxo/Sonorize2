using Avalonia.Media.Imaging;
using Sonorize.Models;
using System;
using System.Collections.Generic;
using System.Globalization; // For CultureInfo
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using TagLib;
using Avalonia.Threading;
using Avalonia.Media; // For Brushes, Colors
using Avalonia; // For PixelSize, Vector, Rect, Size, Point
using Avalonia.Platform; // For RenderTargetBitmap
using System.Collections.Concurrent; // For concurrent collection

namespace Sonorize.Services;

public class MusicLibraryService
{
    private Bitmap? _defaultThumbnail;
    private readonly LoopDataService _loopDataService;

    // Use ConcurrentBag or similar if list is truly concurrent. ObservableCollection needs UI thread for add/remove.
    // For background processing *of* items already in the list, a plain List might suffice if modifications are UI-thread only.
    // Let's assume the list of all songs is managed by the ViewModel (_allSongs), and this service processes that list.
    // A more robust approach would involve this service holding the master list and exposing it.
    // For this task, we'll modify the thumbnail loading to accept an IEnumerable of songs to process.


    public MusicLibraryService(LoopDataService loopDataService)
    {
        _loopDataService = loopDataService;
        Debug.WriteLine("[MusicLibService] Constructor called.");
        // Default thumbnail creation logic remains the same...
        if (_defaultThumbnail == null)
        {
            _defaultThumbnail = CreateDefaultMusicalNoteIcon();
            if (_defaultThumbnail == null)
            {
                Debug.WriteLine("[MusicLibService] CRITICAL: Failed to create default thumbnail in constructor.");
            }
            else
            {
                Debug.WriteLine("[MusicLibService] Default thumbnail created successfully in constructor.");
            }
        }
    }
    private Bitmap? CreateDefaultMusicalNoteIcon()
    {
        Debug.WriteLine("[ThumbGen] CreateDefaultMusicalNoteIcon called.");
        try
        {
            var pixelSize = new Avalonia.PixelSize(96, 96); // Increased size
            var dpi = new Avalonia.Vector(96, 96);

            using var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
            using (DrawingContext context = renderTarget.CreateDrawingContext())
            {
                var backgroundBrush = new SolidColorBrush(Avalonia.Media.Colors.DimGray);
                var foregroundBrush = Avalonia.Media.Brushes.WhiteSmoke;

                var bounds = new Rect(new Size(pixelSize.Width, pixelSize.Height));
                context.FillRectangle(backgroundBrush, bounds);

                var formattedText = new FormattedText(
                    "♫",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    pixelSize.Width / 2, // Font size scaled with icon size
                    foregroundBrush);

                // Center the text
                var textOrigin = new Avalonia.Point(
                    (bounds.Width - formattedText.Width) / 2,
                    (bounds.Height - formattedText.Height) / 2);
                context.DrawText(formattedText, textOrigin);
            }

            // Save to a memory stream
            using var memoryStream = new MemoryStream();
            renderTarget.Save(memoryStream); // Default is PNG
            memoryStream.Seek(0, SeekOrigin.Begin);

            if (memoryStream.Length > 0)
            {
                var bitmap = new Bitmap(memoryStream);
                Debug.WriteLine($"[ThumbGen] Default musical note icon created successfully. Size: {bitmap.PixelSize}");
                return bitmap;
            }
            Debug.WriteLine("[ThumbGen] CRITICAL ERROR: MemoryStream empty for default icon, returning null.");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThumbGen] CRITICAL EXCEPTION creating default icon: {ex.ToString()}, returning null.");
            return null;
        }
    }

    public Bitmap? GetDefaultThumbnail() => _defaultThumbnail;

    // Renamed to indicate it's for background loading
    private Bitmap? LoadAlbumArtForBackground(string filePath)
    {
        try
        {
            using (var tagFile = TagLib.File.Create(filePath))
            {
                if (tagFile.Tag.Pictures.Length > 0)
                {
                    IPicture pic = tagFile.Tag.Pictures[0];
                    using (var ms = new MemoryStream(pic.Data.Data))
                    {
                        if (ms.Length > 0)
                        {
                            using (var originalBitmap = new Bitmap(ms))
                            {
                                // Modified: Resize to a larger thumbnail for better quality when scaled down
                                var targetSize = new PixelSize(128, 128); // Increased from 96x96
                                var scaledBitmap = originalBitmap.CreateScaledBitmap(targetSize, BitmapInterpolationMode.HighQuality);
                                // Debug.WriteLine($"[AlbumArt] Loaded and scaled album art for {Path.GetFileName(filePath)} to {targetSize.Width}x{targetSize.Height}.");
                                return scaledBitmap;
                            }
                        }
                    }
                }
            }
        }
        catch (CorruptFileException) { /* Debug.WriteLine($"[AlbumArt] Corrupt file exception for {Path.GetFileName(filePath)}"); */ }
        catch (UnsupportedFormatException) { /* Debug.WriteLine($"[AlbumArt] Unsupported format exception for {Path.GetFileName(filePath)}"); */ }
        catch (Exception ex) { Debug.WriteLine($"[AlbumArt] Error loading album art for {Path.GetFileName(filePath)}: {ex.Message}"); }
        return null;
    }


    // Modified: This method now loads basic metadata quickly and queues thumbnail loading.
    public async Task LoadMusicFromDirectoriesAsync(
        IEnumerable<string> directories,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback,
        Action<List<Song>> thumbnailLoadingStartCallback) // New callback to pass the list for thumbnail loading
    {
        Debug.WriteLine("[MusicLibService] LoadMusicFromDirectoriesAsync called.");
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" }; // Common audio formats
        Bitmap? defaultIcon = GetDefaultThumbnail();
        int filesProcessed = 0;
        var allSongsList = new List<Song>(); // Collect songs here to pass to thumbnail loader

        foreach (var dir in directories)
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
                Debug.WriteLine($"[LibScan] Error enumerating files in {dir}: {ex.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Error scanning {Path.GetFileName(dir)}"));
                continue;
            }

            // Process files quickly without thumbnail loading
            await Task.Run(() =>
            {
                foreach (var file in filesInDir)
                {
                    var song = new Song
                    {
                        FilePath = file,
                        Title = Path.GetFileNameWithoutExtension(file), // Default title
                        Artist = "Unknown Artist",       // Default artist
                        Album = "Unknown Album",         // Default album
                        Duration = TimeSpan.Zero,        // Default duration
                        Thumbnail = defaultIcon // Use default thumbnail initially
                    };

                    try
                    {
                        using (var tagFile = TagLib.File.Create(file))
                        {
                            // Use await-free TagLib operations within Task.Run
                            if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title)) song.Title = tagFile.Tag.Title;
                            if (tagFile.Tag.Performers.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.Performers[0]))
                                song.Artist = tagFile.Tag.Performers[0];
                            else if (tagFile.Tag.AlbumArtists.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.AlbumArtists[0]))
                                song.Artist = tagFile.Tag.AlbumArtists[0]; // Fallback to album artist
                            if (!string.IsNullOrWhiteSpace(tagFile.Tag.Album)) song.Album = tagFile.Tag.Album; // Corrected variable name
                            if (tagFile.Properties.Duration > TimeSpan.Zero) song.Duration = tagFile.Properties.Duration;
                        }
                    }
                    catch (Exception) { /* Silently ignore metadata read errors for now */ }

                    var storedLoopData = _loopDataService.GetLoop(song.FilePath);
                    if (storedLoopData != null)
                    {
                        // Loop data is small, load it immediately
                        song.SavedLoop = new LoopRegion(storedLoopData.Start, storedLoopData.End);
                        song.IsLoopActive = storedLoopData.IsActive; // <-- SET IsLoopActive
                        // Debug.WriteLine($"[MusicLibService] Loaded persistent loop for {Path.GetFileName(song.FilePath)}: {song.SavedLoop.Start} - {storedLoopData.End}, Active: {song.IsLoopActive}");
                    }

                    // Add the song (with default thumbnail) to the UI thread collection quickly
                    Dispatcher.UIThread.InvokeAsync(() => songAddedCallback(song), DispatcherPriority.Background);

                    allSongsList.Add(song); // Add to the list for background thumbnail processing
                    filesProcessed++;
                    if (filesProcessed % 50 == 0) // Update status periodically
                    {
                        Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Loaded {filesProcessed} songs (metadata)..."), DispatcherPriority.Background);
                    }
                }
            }); // await Task.Run finishes the metadata reading for this directory

        }

        Debug.WriteLine($"[MusicLibService] Initial metadata scan complete. Processed {filesProcessed} songs. Starting background thumbnail loading.");
        await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Loaded {filesProcessed} songs metadata. Loading thumbnails..."), DispatcherPriority.Background);

        // Trigger background thumbnail loading *after* the main scan is done
        await Dispatcher.UIThread.InvokeAsync(() => thumbnailLoadingStartCallback(allSongsList)); // Pass the list of all songs

        Debug.WriteLine($"[MusicLibService] LoadMusicFromDirectoriesAsync complete.");
    }

    // New method to load thumbnails in the background
    public async Task LoadThumbnailsInBackgroundAsync(List<Song> songs)
    {
        if (_defaultThumbnail == null)
        {
            Debug.WriteLine("[MusicLibService] Cannot start background thumbnail loading: Default thumbnail is null.");
            return;
        }

        Debug.WriteLine($"[MusicLibService] Starting background thumbnail loading for {songs.Count} songs.");

        // Use Task.WhenAll to run multiple thumbnail loads concurrently
        // Limit concurrency if needed (e.g., using SemaphoreSlim)
        var thumbnailTasks = songs.Select(async song =>
        {
            Bitmap? loadedThumb = null;
            try
            {
                loadedThumb = await Task.Run(() => LoadAlbumArtForBackground(song.FilePath));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicLibService] Error in thumbnail loading task for {Path.GetFileName(song.FilePath)}: {ex.Message}");
                loadedThumb = null; // Ensure null on error
            }

            // Simplified condition: If a thumbnail was loaded, assign it.
            if (loadedThumb != null)
            {
                // Ensure the update happens on the UI thread
                // Use DispatcherPriority.Normal for thumbnail updates
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    // Double check loadedThumb is still not null after Dispatcher invoke
                    if (loadedThumb != null)
                    {
                        // Only update if the current thumbnail is the default, or if it's null.
                        // This prevents overwriting a valid thumbnail that might have been set
                        // by another process or if the user somehow changed it.
                        // We compare by reference because the default thumbnail is a single static instance.
                        if (song.Thumbnail == _defaultThumbnail || song.Thumbnail == null)
                        {
                            song.Thumbnail = loadedThumb;
                            // Debug.WriteLine($"[MusicLibService] Updated thumbnail for {Path.GetFileName(song.FilePath)}");
                        }
                        else // Song already had a non-default thumbnail
                        {
                            loadedThumb.Dispose(); // Dispose the newly loaded one as it's not needed
                                                   // Debug.WriteLine($"[MusicLibService] Song {Path.GetFileName(song.FilePath)} already had a non-default thumbnail. Disposing loaded thumbnail.");
                        }
                    }
                }, DispatcherPriority.Render); // Changed priority to Render
            }
            // No else needed if loadedThumb is null (stays default or whatever it was)
        });

        // Await all thumbnail loading tasks
        await Task.WhenAll(thumbnailTasks);

        Debug.WriteLine("[MusicLibService] Background thumbnail loading complete.");
    }
}