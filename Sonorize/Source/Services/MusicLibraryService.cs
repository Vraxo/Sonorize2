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
using Avalonia.Controls; // For Design.IsDesignMode

namespace Sonorize.Services;

public class MusicLibraryService
{
    private Bitmap? _defaultThumbnail;
    private readonly LoopDataService _loopDataService;

    public MusicLibraryService(LoopDataService loopDataService)
    {
        _loopDataService = loopDataService;
        Debug.WriteLine("[MusicLibService] Constructor called.");
        // Default thumbnail creation logic remains the same...
        if (_defaultThumbnail == null)
        {
            // Check for design mode moved into CreateDefaultMusicalNoteIcon
            _defaultThumbnail = CreateDefaultMusicalNoteIcon();
            if (_defaultThumbnail == null && !Design.IsDesignMode) // Log error only if not in design mode and it failed
            {
                Debug.WriteLine("[MusicLibService] CRITICAL: Failed to create default thumbnail in constructor.");
            }
            else if (_defaultThumbnail != null)
            {
                Debug.WriteLine("[MusicLibService] Default thumbnail created successfully in constructor.");
            }
            else if (Design.IsDesignMode)
            {
                Debug.WriteLine("[MusicLibService] Design Mode: Default thumbnail not created as intended.");
            }
        }
    }
    private Bitmap? CreateDefaultMusicalNoteIcon()
    {
        Debug.WriteLine("[ThumbGen] CreateDefaultMusicalNoteIcon called.");
        if (Design.IsDesignMode) // Check if running in design mode
        {
            Debug.WriteLine("[ThumbGen] Design Mode: Skipping RenderTargetBitmap creation for default icon. Returning null.");
            return null;
        }

        try
        {
            var pixelSize = new Avalonia.PixelSize(64, 64);
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
                    Typeface.Default, // Consider a more specific font if default doesn't have the note
                    32, // Font size
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
    private Bitmap? LoadAlbumArt(string filePath)
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
                            // It's good practice to check if the bitmap can be created
                            // and handle potential exceptions here too.
                            using (var originalBitmap = new Bitmap(ms))
                            {
                                // Resize to a smaller thumbnail
                                var targetSize = new PixelSize(64, 64); // Example size
                                var scaledBitmap = originalBitmap.CreateScaledBitmap(targetSize, BitmapInterpolationMode.HighQuality);
                                return scaledBitmap;
                            }
                        }
                    }
                }
            }
        }
        catch (CorruptFileException) { /* Optional: Debug.WriteLine for tracking */ }
        catch (UnsupportedFormatException) { /* Optional: Debug.WriteLine */ }
        catch (Exception ex) { Debug.WriteLine($"[AlbumArt] Error loading album art for {Path.GetFileName(filePath)}: {ex.Message}"); }
        return null;
    }


    public async Task LoadMusicFromDirectoriesAsync(
        IEnumerable<string> directories,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback)
    {
        Debug.WriteLine("[MusicLibService] LoadMusicFromDirectoriesAsync called.");
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" }; // Common audio formats
        Bitmap? defaultIcon = GetDefaultThumbnail(); // This will be null in design mode if CreateDefaultMusicalNoteIcon returns null
        int filesProcessed = 0;

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
                Bitmap? thumbnail = LoadAlbumArt(file);
                var song = new Song
                {
                    FilePath = file,
                    Title = Path.GetFileNameWithoutExtension(file), // Default title
                    Artist = "Unknown Artist",       // Default artist
                    Album = "Unknown Album",         // Default album
                    Duration = TimeSpan.Zero,        // Default duration
                    Thumbnail = thumbnail ?? defaultIcon // If defaultIcon is null (design mode), thumbnail might be null.
                };

                try
                {
                    using (var tagFile = TagLib.File.Create(file))
                    {
                        if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title)) song.Title = tagFile.Tag.Title;
                        if (tagFile.Tag.Performers.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.Performers[0]))
                            song.Artist = tagFile.Tag.Performers[0];
                        else if (tagFile.Tag.AlbumArtists.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.AlbumArtists[0]))
                            song.Artist = tagFile.Tag.AlbumArtists[0]; // Fallback to album artist
                        if (!string.IsNullOrWhiteSpace(tagFile.Tag.Album)) song.Album = tagFile.Tag.Album;
                        if (tagFile.Properties.Duration > TimeSpan.Zero) song.Duration = tagFile.Properties.Duration;
                    }
                }
                catch (Exception) { /* Silently ignore metadata read errors for now */ }

                var storedLoopData = _loopDataService.GetLoop(song.FilePath);
                if (storedLoopData != null)
                {
                    song.SavedLoop = new LoopRegion(storedLoopData.Start, storedLoopData.End);
                    song.IsLoopActive = storedLoopData.IsActive; // <-- SET IsLoopActive
                    Debug.WriteLine($"[MusicLibService] Loaded persistent loop for {Path.GetFileName(song.FilePath)}: {song.SavedLoop.Start} - {song.SavedLoop.End}, Active: {song.IsLoopActive}");
                }

                await Dispatcher.UIThread.InvokeAsync(() => songAddedCallback(song));
                filesProcessed++;
                if (filesProcessed % 20 == 0) // Update status periodically
                {
                    await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Loaded {filesProcessed} songs..."));
                }
            }
        }
        Debug.WriteLine($"[MusicLibService] Background file scanning complete. Processed {filesProcessed} songs.");
    }
}