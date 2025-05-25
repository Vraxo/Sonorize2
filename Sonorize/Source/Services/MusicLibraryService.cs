using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform; // For RenderTargetBitmap
using Avalonia.Threading;
using Sonorize.Models;
// Sonorize.Services is the namespace, not for using statement here.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq; // Important for .Any()
using System.Threading; // For Interlocked
using System.Threading.Tasks;
using TagLib;

namespace Sonorize.Services;

public class MusicLibraryService
{
    private Bitmap? _defaultThumbnail;
    private readonly LoopDataService _loopDataService;
    private const int UI_UPDATE_BATCH_SIZE = 50; // Add songs to UI in batches of this size

    public MusicLibraryService(LoopDataService loopDataService)
    {
        _loopDataService = loopDataService;
        Debug.WriteLine("[MusicLibService] Constructor called.");
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

    private async Task<Bitmap?> LoadAlbumArtAsync(string filePath)
    {
        try
        {
            // Offload the file reading and image processing to a background thread
            return await Task.Run(() =>
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
                                    var targetSize = new PixelSize(128, 128);
                                    var scaledBitmap = originalBitmap.CreateScaledBitmap(targetSize, BitmapInterpolationMode.HighQuality);
                                    // Debug.WriteLine($"[AlbumArtAsync] Loaded and scaled album art for {Path.GetFileName(filePath)} to {targetSize.Width}x{targetSize.Height}.");
                                    return scaledBitmap;
                                }
                            }
                        }
                    }
                }
                return null; // Return null if no picture found or stream empty
            });
        }
        catch (CorruptFileException) { Debug.WriteLine($"[AlbumArtAsync] Corrupt file exception for {Path.GetFileName(filePath)}"); }
        catch (UnsupportedFormatException) { Debug.WriteLine($"[AlbumArtAsync] Unsupported format exception for {Path.GetFileName(filePath)}"); }
        catch (Exception ex) { Debug.WriteLine($"[AlbumArtAsync] Error loading album art for {Path.GetFileName(filePath)}: {ex.Message}"); }
        return null; // Return null on any exception
    }


    public async Task LoadMusicFromDirectoriesAsync(
        IEnumerable<string> directories,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback)
    {
        Debug.WriteLine("[MusicLibService] LoadMusicFromDirectoriesAsync (non-blocking thumbnail load).");
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" };
        Bitmap? defaultIcon = GetDefaultThumbnail();
        int filesProcessed = 0;
        List<Task> thumbnailLoadTasks = new();

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

            foreach (var file in filesInDir)
            {
                var song = new Song
                {
                    FilePath = file,
                    Title = Path.GetFileNameWithoutExtension(file),
                    Artist = "Unknown Artist",
                    Album = "Unknown Album",
                    Duration = TimeSpan.Zero,
                    Thumbnail = defaultIcon
                };

                try
                {
                    using (var tagFile = TagLib.File.Create(file))
                    {
                        if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title)) song.Title = tagFile.Tag.Title;
                        if (tagFile.Tag.Performers.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.Performers[0]))
                            song.Artist = tagFile.Tag.Performers[0];
                        else if (tagFile.Tag.AlbumArtists.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.AlbumArtists[0]))
                            song.Artist = tagFile.Tag.AlbumArtists[0];
                        if (!string.IsNullOrWhiteSpace(tagFile.Tag.Album)) song.Album = tagFile.Tag.Album;
                        if (tagFile.Properties.Duration > TimeSpan.Zero) song.Duration = tagFile.Properties.Duration;
                    }
                }
                catch (Exception) { /* Ignore metadata failures silently */ }

                var storedLoopData = _loopDataService.GetLoop(song.FilePath);
                if (storedLoopData != null)
                {
                    song.SavedLoop = new LoopRegion(storedLoopData.Start, storedLoopData.End);
                    song.IsLoopActive = storedLoopData.IsActive;
                }

                // UI update: add song immediately
                await Dispatcher.UIThread.InvokeAsync(() => songAddedCallback(song));

                // Begin loading thumbnail in background
                thumbnailLoadTasks.Add(AssignActualThumbnailAsync(song));

                filesProcessed++;
                if (filesProcessed % (UI_UPDATE_BATCH_SIZE * 2) == 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Loaded {filesProcessed} songs..."));
                }
            }
        }

        await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Metadata scan complete. {filesProcessed} songs found. Loading thumbnails in background..."));

        // Optional: Await all thumbnail tasks if you want to block until all thumbnails are loaded
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(thumbnailLoadTasks);
                await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"All thumbnails loaded."));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicLibService] Error in background thumbnail tasks: {ex.Message}");
            }
        });
    }
    private async Task AssignActualThumbnailAsync(Song song)
    {
        try
        {
            var actualThumbnail = await LoadAlbumArtAsync(song.FilePath);
            if (actualThumbnail != null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => song.Thumbnail = actualThumbnail);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MusicLibService] Error loading thumbnail for {song.Title}: {ex.Message}");
        }
    }

}