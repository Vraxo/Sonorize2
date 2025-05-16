using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Sonorize.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using TagLib; // For reading audio file metadata and embedded pictures
using Avalonia.Threading; // For Dispatcher

namespace Sonorize.Services;

public class MusicLibraryService
{
    private Bitmap? _defaultThumbnail; // Cache for the default musical note icon

    public MusicLibraryService()
    {
        Debug.WriteLine("[MusicLibService] Constructor called.");
    }

    // Method to create the nice default musical note icon
    private Bitmap? CreateDefaultMusicalNoteIcon()
    {
        Debug.WriteLine("[ThumbGen] CreateDefaultMusicalNoteIcon called.");
        try
        {
            var pixelSize = new Avalonia.PixelSize(64, 64); // Generate at a decent resolution
            var dpi = new Avalonia.Vector(96, 96);

            using var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
            using (DrawingContext context = renderTarget.CreateDrawingContext())
            {
                // Original intended colors for the default icon
                var backgroundBrush = new SolidColorBrush(Avalonia.Media.Colors.DimGray); // #FF696969
                var foregroundBrush = Avalonia.Media.Brushes.WhiteSmoke;                 // #FFF5F5F5

                var bounds = new Rect(new Size(pixelSize.Width, pixelSize.Height));
                context.FillRectangle(backgroundBrush, bounds);

                var formattedText = new FormattedText(
                    "♫", // U+266B MUSICAL NOTE
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default, // Using default system font
                    32, // Font size for 64x64 image
                    foregroundBrush);

                var textOrigin = new Avalonia.Point(
                    (bounds.Width - formattedText.Width) / 2,
                    (bounds.Height - formattedText.Height) / 2);
                context.DrawText(formattedText, textOrigin);
            }

            using var memoryStream = new MemoryStream();
            renderTarget.Save(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            if (memoryStream.Length > 0)
            {
                var bitmap = new Bitmap(memoryStream);
                Debug.WriteLine($"[ThumbGen] Default musical note icon created successfully. Size: {bitmap.PixelSize}");
                return bitmap;
            }
            Debug.WriteLine("[ThumbGen] CRITICAL ERROR: MemoryStream empty for default icon.");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThumbGen] CRITICAL EXCEPTION creating default icon: {ex.ToString()}");
            return null;
        }
    }

    // Helper method to get the cached default thumbnail or create it if it doesn't exist
    private Bitmap? GetDefaultThumbnail()
    {
        if (_defaultThumbnail == null)
        {
            Debug.WriteLine("[ThumbGen] _defaultThumbnail is null, attempting to create it.");
            _defaultThumbnail = CreateDefaultMusicalNoteIcon();
        }
        return _defaultThumbnail;
    }

    private Bitmap? LoadAlbumArt(string filePath)
    {
        try
        {
            using (var tagFile = TagLib.File.Create(filePath))
            {
                if (tagFile.Tag.Pictures.Length > 0)
                {
                    IPicture pic = tagFile.Tag.Pictures[0]; // Take the first picture
                    using (var ms = new MemoryStream(pic.Data.Data))
                    {
                        if (ms.Length > 0)
                        {
                            // Load the original bitmap from the stream
                            using (var originalBitmap = new Bitmap(ms))
                            {
                                // Define target size for thumbnails
                                var targetSize = new PixelSize(64, 64);

                                // Create a scaled bitmap
                                // This disposes the originalBitmap implicitly if it's the same object,
                                // but since CreateScaledBitmap returns a new instance, originalBitmap
                                // will be disposed by its own 'using' block.
                                var scaledBitmap = originalBitmap.CreateScaledBitmap(targetSize, BitmapInterpolationMode.HighQuality);

                                // Debug.WriteLine($"[AlbumArt] Loaded & Resized for {Path.GetFileName(filePath)}, Original: {originalBitmap.PixelSize}, Scaled: {scaledBitmap.PixelSize}");
                                return scaledBitmap;
                            }
                        }
                    }
                }
            }
        }
        catch (CorruptFileException) { /* Debug.WriteLine($"[AlbumArt] Corrupt file, cannot load metadata for {Path.GetFileName(filePath)}"); */ }
        catch (UnsupportedFormatException) { /* Debug.WriteLine($"[AlbumArt] Unsupported format for metadata {Path.GetFileName(filePath)}"); */ }
        catch (Exception ex) { Debug.WriteLine($"[AlbumArt] Error loading album art for {Path.GetFileName(filePath)}: {ex.Message}"); } // Log other exceptions
        return null;
    }

    public async Task LoadMusicFromDirectoriesAsync(
        IEnumerable<string> directories,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback)
    {
        Debug.WriteLine("[MusicLibService] LoadMusicFromDirectoriesAsync (incremental) called.");
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" }; // TagLib supports these and more

        // Ensure the default thumbnail is created once and cached (on the calling thread, which is fine for a small icon)
        Bitmap? defaultIcon = GetDefaultThumbnail();
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
                                      .ToList(); // ToList to avoid collection modified issues if dir changes (unlikely here)
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibScan] Error enumerating files in {dir}: {ex.Message}");
                await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Error scanning {Path.GetFileName(dir)}"));
                continue;
            }

            foreach (var file in filesInDir)
            {
                // LoadAlbumArt is relatively quick as it reads embedded data or fails.
                // The resizing is the new part, also generally quick for small images.
                Bitmap? thumbnail = LoadAlbumArt(file);

                var song = new Song
                {
                    FilePath = file,
                    Title = Path.GetFileNameWithoutExtension(file), // Default title
                    Artist = "Unknown Artist",                     // Default artist
                    Duration = TimeSpan.Zero,                      // Default duration
                    Thumbnail = thumbnail ?? defaultIcon           // Use album art or fallback to default icon
                };

                // Extract actual metadata using TagLib-Sharp
                try
                {
                    using (var tagFile = TagLib.File.Create(file))
                    {
                        if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title))
                            song.Title = tagFile.Tag.Title;

                        if (tagFile.Tag.Performers.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.Performers[0]))
                            song.Artist = tagFile.Tag.Performers[0];
                        else if (tagFile.Tag.AlbumArtists.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.AlbumArtists[0]))
                            song.Artist = tagFile.Tag.AlbumArtists[0]; // Fallback to album artist

                        if (tagFile.Properties.Duration > TimeSpan.Zero)
                            song.Duration = tagFile.Properties.Duration;
                    }
                }
                catch (Exception)
                {
                    // Debug.WriteLine($"[TagLib] Error reading metadata for {Path.GetFileName(file)}: {ex.Message}");
                    // Silently ignore metadata read errors for individual files to not stop the whole scan.
                    // The defaults for Title, Artist, Duration will be used.
                }

                // Marshall the addition of the song to the UI thread
                await Dispatcher.UIThread.InvokeAsync(() => songAddedCallback(song));
                filesProcessed++;

                if (filesProcessed % 20 == 0) // Update status periodically
                {
                    await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Loaded {filesProcessed} songs..."));
                }
            }
        }
        Debug.WriteLine($"[MusicLibService] Background file scanning complete. Processed {filesProcessed} songs in total.");
        // Final status update handled by ViewModel after this method completes
    }
}