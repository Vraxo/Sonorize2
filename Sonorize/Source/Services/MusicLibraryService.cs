using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Sonorize.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TagLib;

namespace Sonorize.Services;

public class MusicLibraryService
{
    private Bitmap? _defaultThumbnail;
    private readonly LoopDataService _loopDataService;
    private const int UI_UPDATE_BATCH_SIZE = 50;

    private readonly ConcurrentQueue<Song> _thumbnailQueue = new();
    private readonly SemaphoreSlim _thumbnailWorkers = new(4); // Limit concurrent thumbnail loads
    private bool _isThumbnailProcessingRunning = false;

    public MusicLibraryService(LoopDataService loopDataService)
    {
        _loopDataService = loopDataService;
        Debug.WriteLine("[MusicLibService] Constructor called.");
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

    private Bitmap? CreateDefaultMusicalNoteIcon()
    {
        Debug.WriteLine("[ThumbGen] CreateDefaultMusicalNoteIcon called.");
        try
        {
            var pixelSize = new PixelSize(96, 96);
            var dpi = new Vector(96, 96);

            using var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
            using (DrawingContext context = renderTarget.CreateDrawingContext())
            {
                var backgroundBrush = new SolidColorBrush(Colors.DimGray);
                var foregroundBrush = Brushes.WhiteSmoke;
                var bounds = new Rect(new Size(pixelSize.Width, pixelSize.Height));

                context.FillRectangle(backgroundBrush, bounds);

                var formattedText = new FormattedText(
                    "♫",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    pixelSize.Width / 2,
                    foregroundBrush);

                var textOrigin = new Point(
                    (bounds.Width - formattedText.Width) / 2,
                    (bounds.Height - formattedText.Height) / 2);

                context.DrawText(formattedText, textOrigin);
            }

            using var memoryStream = new MemoryStream();
            renderTarget.Save(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);

            return memoryStream.Length > 0 ? new Bitmap(memoryStream) : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThumbGen] CRITICAL EXCEPTION creating default icon: {ex}");
            return null;
        }
    }

    public Bitmap? GetDefaultThumbnail() => _defaultThumbnail;

    private async Task<Bitmap?> LoadAlbumArtAsync(string filePath)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var tagFile = TagLib.File.Create(filePath);
                if (tagFile.Tag.Pictures.Length > 0)
                {
                    IPicture pic = tagFile.Tag.Pictures[0];
                    using var ms = new MemoryStream(pic.Data.Data);
                    if (ms.Length > 0)
                    {
                        using var originalBitmap = new Bitmap(ms);
                        var targetSize = new PixelSize(128, 128);
                        return originalBitmap.CreateScaledBitmap(targetSize, BitmapInterpolationMode.HighQuality);
                    }
                }
                return null;
            });
        }
        catch (CorruptFileException) { Debug.WriteLine($"[AlbumArtAsync] Corrupt file: {Path.GetFileName(filePath)}"); }
        catch (UnsupportedFormatException) { Debug.WriteLine($"[AlbumArtAsync] Unsupported format: {Path.GetFileName(filePath)}"); }
        catch (Exception ex) { Debug.WriteLine($"[AlbumArtAsync] Error: {Path.GetFileName(filePath)} - {ex.Message}"); }
        return null;
    }

    public async Task LoadMusicFromDirectoriesAsync(
        IEnumerable<string> directories,
        Action<Song> songAddedCallback,
        Action<string> statusUpdateCallback)
    {
        Debug.WriteLine("[MusicLibService] LoadMusicFromDirectoriesAsync");
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" };
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
                    using var tagFile = TagLib.File.Create(file);
                    if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title)) song.Title = tagFile.Tag.Title;
                    if (tagFile.Tag.Performers.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.Performers[0]))
                        song.Artist = tagFile.Tag.Performers[0];
                    else if (tagFile.Tag.AlbumArtists.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.AlbumArtists[0]))
                        song.Artist = tagFile.Tag.AlbumArtists[0];
                    if (!string.IsNullOrWhiteSpace(tagFile.Tag.Album)) song.Album = tagFile.Tag.Album;
                    if (tagFile.Properties.Duration > TimeSpan.Zero) song.Duration = tagFile.Properties.Duration;
                }
                catch (Exception) { }

                var storedLoopData = _loopDataService.GetLoop(song.FilePath);
                if (storedLoopData != null)
                {
                    song.SavedLoop = new LoopRegion(storedLoopData.Start, storedLoopData.End);
                    song.IsLoopActive = storedLoopData.IsActive;
                }

                await Dispatcher.UIThread.InvokeAsync(() => songAddedCallback(song));

                _thumbnailQueue.Enqueue(song);
                StartThumbnailProcessing();

                filesProcessed++;
                if (filesProcessed % (UI_UPDATE_BATCH_SIZE * 2) == 0)
                {
                    await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Loaded {filesProcessed} songs..."));
                }
            }
        }

        await Dispatcher.UIThread.InvokeAsync(() => statusUpdateCallback($"Metadata scan complete. {filesProcessed} songs found. Loading thumbnails in background..."));
    }

    private void StartThumbnailProcessing()
    {
        if (_isThumbnailProcessingRunning)
            return;

        _isThumbnailProcessingRunning = true;

        Task.Run(async () =>
        {
            while (_thumbnailQueue.TryDequeue(out var song))
            {
                await _thumbnailWorkers.WaitAsync();

                _ = Task.Run(async () =>
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
                        Debug.WriteLine($"[MusicLibService] Thumbnail error for {song.Title}: {ex.Message}");
                    }
                    finally
                    {
                        _thumbnailWorkers.Release();
                    }
                });
            }

            _isThumbnailProcessingRunning = false;
        });
    }
}
