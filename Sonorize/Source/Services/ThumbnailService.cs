using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Sonorize.Models;
using TagLib; // Required for IPicture, CorruptFileException, UnsupportedFormatException

namespace Sonorize.Services;

public class ThumbnailService : IDisposable
{
    private Bitmap? _defaultThumbnail;
    private readonly ConcurrentQueue<ThumbnailRequest> _thumbnailQueue = new();
    private readonly SemaphoreSlim _thumbnailWorkers = new(4); // Limit concurrent thumbnail loads
    private Task? _processingTask;
    private CancellationTokenSource _cts = new();
    private readonly object _lock = new();


    private record ThumbnailRequest(Song SongToUpdate, Action<Song, Bitmap?> Callback);

    public ThumbnailService()
    {
        Debug.WriteLine("[ThumbnailService] Initialized.");
        // _defaultThumbnail will be created on first access via GetDefaultThumbnail()
    }

    public Bitmap? GetDefaultThumbnail()
    {
        _defaultThumbnail ??= CreateDefaultMusicalNoteIcon();
        return _defaultThumbnail;
    }

    public void QueueThumbnailRequest(Song song, Action<Song, Bitmap?> onThumbnailReadyCallback)
    {
        if (song == null) throw new ArgumentNullException(nameof(song));
        if (onThumbnailReadyCallback == null) throw new ArgumentNullException(nameof(onThumbnailReadyCallback));

        _thumbnailQueue.Enqueue(new ThumbnailRequest(song, onThumbnailReadyCallback));
        EnsureProcessingRunning();
    }

    private void EnsureProcessingRunning()
    {
        lock (_lock)
        {
            if (_processingTask == null || _processingTask.IsCompleted)
            {
                _cts = new CancellationTokenSource(); // Reset CTS if task was completed/faulted
                _processingTask = Task.Run(ProcessQueueAsync, _cts.Token);
                Debug.WriteLine("[ThumbnailService] Thumbnail processing task started.");
            }
        }
    }

    private async Task ProcessQueueAsync()
    {
        Debug.WriteLine("[ThumbnailService] ProcessQueueAsync loop started.");
        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                if (_thumbnailQueue.TryDequeue(out var request))
                {
                    await _thumbnailWorkers.WaitAsync(_cts.Token); // Wait for a free worker slot

                    // Fire-and-forget the actual loading for this request to not block the dequeuing loop
                    _ = Task.Run(async () =>
                    {
                        Bitmap? loadedThumbnail = null;
                        try
                        {
                            if (!_cts.Token.IsCancellationRequested)
                            {
                                loadedThumbnail = await LoadAlbumArtAsync(request.SongToUpdate.FilePath);
                                // Callback needs to be on UI thread as it might update UI-bound properties
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    request.Callback(request.SongToUpdate, loadedThumbnail);
                                }, DispatcherPriority.Background, _cts.Token);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine($"[ThumbnailService] Thumbnail loading cancelled for {request.SongToUpdate.Title}.");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ThumbnailService] Error loading thumbnail for {request.SongToUpdate.Title}: {ex.Message}");
                            // Invoke callback with null to signal completion, even on error
                            try
                            {
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    request.Callback(request.SongToUpdate, null);
                                }, DispatcherPriority.Background); // Don't use cancelled token for cleanup callback
                            }
                            catch (Exception callbackEx)
                            {
                                Debug.WriteLine($"[ThumbnailService] Error invoking callback after error for {request.SongToUpdate.Title}: {callbackEx.Message}");
                            }
                        }
                        finally
                        {
                            _thumbnailWorkers.Release();
                        }
                    }, _cts.Token);
                }
                else
                {
                    // Queue is empty, wait a bit before checking again or break if cancellation is requested
                    await Task.Delay(100, _cts.Token);
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.WriteLine("[ThumbnailService] ProcessQueueAsync task cancelled.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThumbnailService] Unhandled exception in ProcessQueueAsync: {ex}");
        }
        finally
        {
            Debug.WriteLine("[ThumbnailService] ProcessQueueAsync loop finished.");
            lock (_lock) // Ensure task is nulled out safely
            {
                _processingTask = null;
            }
        }
    }


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
                        // Load the original bitmap
                        using var originalBitmap = new Bitmap(ms);
                        // Define the target size for the thumbnail
                        var targetSize = new PixelSize(128, 128); // Example size, adjust as needed
                        // Create a scaled version of the bitmap
                        return originalBitmap.CreateScaledBitmap(targetSize, BitmapInterpolationMode.HighQuality);
                    }
                }
                return null; // No pictures found or picture data is empty
            });
        }
        catch (CorruptFileException) { Debug.WriteLine($"[ThumbnailService] Corrupt file: {Path.GetFileName(filePath)}"); }
        catch (UnsupportedFormatException) { Debug.WriteLine($"[ThumbnailService] Unsupported format: {Path.GetFileName(filePath)}"); }
        catch (Exception ex) { Debug.WriteLine($"[ThumbnailService] Error loading album art for {Path.GetFileName(filePath)}: {ex.Message}"); }
        return null; // Return null on any error
    }

    private Bitmap? CreateDefaultMusicalNoteIcon()
    {
        Debug.WriteLine("[ThumbnailService] CreateDefaultMusicalNoteIcon called.");
        try
        {
            var pixelSize = new PixelSize(96, 96); // Standard size for the default icon
            var dpi = new Vector(96, 96); // Standard DPI

            using var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
            using (DrawingContext context = renderTarget.CreateDrawingContext())
            {
                // Define background and foreground brushes
                var backgroundBrush = new SolidColorBrush(Colors.DimGray); // A neutral background
                var foregroundBrush = Brushes.WhiteSmoke; // A contrasting foreground for the icon
                var bounds = new Rect(new Size(pixelSize.Width, pixelSize.Height));

                // Fill background
                context.FillRectangle(backgroundBrush, bounds);

                // Create formatted text for the musical note icon
                var formattedText = new FormattedText(
                    "♫", // Musical note character
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default, // Use default typeface
                    pixelSize.Width / 2, // Make font size relative to icon size
                    foregroundBrush);

                // Calculate text origin to center it
                var textOrigin = new Point(
                    (bounds.Width - formattedText.Width) / 2,
                    (bounds.Height - formattedText.Height) / 2);

                // Draw the text
                context.DrawText(formattedText, textOrigin);
            }

            // Save to a memory stream and create Bitmap
            using var memoryStream = new MemoryStream();
            renderTarget.Save(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin); // Reset stream position

            // Return bitmap if stream is not empty
            return memoryStream.Length > 0 ? new Bitmap(memoryStream) : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ThumbnailService] CRITICAL EXCEPTION creating default icon: {ex}");
            return null; // Return null on error
        }
    }

    public void Dispose()
    {
        Debug.WriteLine("[ThumbnailService] Dispose called.");
        lock (_lock)
        {
            if (!_cts.IsCancellationRequested)
            {
                _cts.Cancel();
            }
        }

        // Wait for the processing task to complete, with a timeout
        _processingTask?.Wait(TimeSpan.FromSeconds(5));

        _cts.Dispose();
        _thumbnailWorkers.Dispose();
        _defaultThumbnail?.Dispose(); // Dispose the bitmap if it was created
        Debug.WriteLine("[ThumbnailService] Dispose finished.");
    }
}