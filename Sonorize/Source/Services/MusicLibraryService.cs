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
using TagLib;
using Avalonia.Threading; // For Dispatcher

namespace Sonorize.Services;

public class MusicLibraryService
{
    private Bitmap? _defaultThumbnail;

    public MusicLibraryService()
    {
        Debug.WriteLine("[MusicLibService] Constructor called.");
    }

    private Bitmap? CreateDefaultMusicalNoteIcon()
    {
        // ... (Keep the CreateDefaultMusicalNoteIcon method exactly as in the previous correct version) ...
        Debug.WriteLine("[ThumbGen] CreateDefaultMusicalNoteIcon called.");
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
                    Typeface.Default,
                    32,
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

    private Bitmap? GetDefaultThumbnail()
    {
        // ... (Keep the GetDefaultThumbnail method exactly as in the previous correct version) ...
        if (_defaultThumbnail == null)
        {
            Debug.WriteLine("[ThumbGen] _defaultThumbnail is null, attempting to create it.");
            _defaultThumbnail = CreateDefaultMusicalNoteIcon();
        }
        return _defaultThumbnail;
    }

    private Bitmap? LoadAlbumArt(string filePath)
    {
        // ... (Keep the LoadAlbumArt method exactly as in the previous correct version) ...
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
                            var avaloniaBitmap = new Bitmap(ms);
                            // Debug.WriteLine($"[AlbumArt] Loaded for {Path.GetFileName(filePath)}, Size: {avaloniaBitmap.PixelSize}");
                            return avaloniaBitmap;
                        }
                    }
                }
            }
        }
        catch (CorruptFileException) { /* Debug.WriteLine($"[AlbumArt] Corrupt file: {Path.GetFileName(filePath)}"); */ }
        catch (UnsupportedFormatException) { /* Debug.WriteLine($"[AlbumArt] Unsupported format: {Path.GetFileName(filePath)}"); */ }
        catch (Exception ex) { Debug.WriteLine($"[AlbumArt] Error loading album art for {Path.GetFileName(filePath)}: {ex.Message}"); }
        return null;
    }

    public async Task LoadMusicFromDirectoriesAsync(
        IEnumerable<string> directories,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback)
    {
        Debug.WriteLine("[MusicLibService] LoadMusicFromDirectoriesAsync (incremental) called.");
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" };
        Bitmap? defaultIcon = GetDefaultThumbnail();
        int filesProcessed = 0;

        // The main loop will run on a background thread thanks to Task.Run in the ViewModel
        // No need for another Task.Run here unless specific parts are exceptionally heavy
        // and can be further parallelized (which is not the case for typical file iteration).

        foreach (var dir in directories)
        {
            if (!Directory.Exists(dir))
            {
                Debug.WriteLine($"[LibScan] Directory not found: {dir}");
                await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Directory not found: {dir}"));
                continue;
            }

            await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Scanning: {Path.GetFileName(dir)}..."));

            // Get all files first to avoid issues with `Directory.EnumerateFiles` if we yield execution often
            // Though for simple processing like this, EnumerateFiles is usually fine.
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
                    Title = Path.GetFileNameWithoutExtension(file),
                    Artist = "Unknown Artist",
                    Duration = TimeSpan.Zero,
                    Thumbnail = thumbnail ?? defaultIcon
                };

                try
                {
                    using (var tagFile = TagLib.File.Create(file))
                    {
                        if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title))
                            song.Title = tagFile.Tag.Title;
                        if (tagFile.Tag.Performers.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.Performers[0]))
                            song.Artist = tagFile.Tag.Performers[0];
                        else if (tagFile.Tag.AlbumArtists.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.AlbumArtists[0]))
                            song.Artist = tagFile.Tag.AlbumArtists[0];

                        if (tagFile.Properties.Duration > TimeSpan.Zero)
                            song.Duration = tagFile.Properties.Duration;
                    }
                }
                catch (Exception) { /* Debug.WriteLine($"[TagLib] Error reading metadata for {file}: {ex.Message}"); */ }

                // Marshall the addition to the UI thread
                await Dispatcher.UIThread.InvokeAsync(() => songAddedCallback(song));
                filesProcessed++;

                if (filesProcessed % 20 == 0) // Update status every 20 files
                {
                    await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Loaded {filesProcessed} songs..."));
                }
            }
        }
        Debug.WriteLine($"[MusicLibService] Background file scanning complete. Processed {filesProcessed} songs in total.");
    }
}