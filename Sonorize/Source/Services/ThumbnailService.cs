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
// TagLib is no longer directly used by ThumbnailService

namespace Sonorize.Services;

public class ThumbnailService : IDisposable
{
    private Bitmap? _defaultThumbnail;
    private readonly ConcurrentQueue<ThumbnailRequest> _thumbnailQueue = new();
    private readonly SemaphoreSlim _thumbnailWorkers = new(4); // Limit concurrent thumbnail loads
    private Task? _processingTask;
    private CancellationTokenSource _cts = new();
    private readonly object _lock = new();
    private readonly DefaultIconGenerator _defaultIconGenerator;
    private readonly AlbumArtLoader _albumArtLoader;


    private record ThumbnailRequest(Song SongToUpdate, Action<Song, Bitmap?> Callback);

    public ThumbnailService(DefaultIconGenerator defaultIconGenerator, AlbumArtLoader albumArtLoader) // Constructor for DI
    {
        _defaultIconGenerator = defaultIconGenerator ?? throw new ArgumentNullException(nameof(defaultIconGenerator));
        _albumArtLoader = albumArtLoader ?? throw new ArgumentNullException(nameof(albumArtLoader));
        Debug.WriteLine("[ThumbnailService] Initialized.");
        // _defaultThumbnail will be created on first access via GetDefaultThumbnail()
    }

    public Bitmap? GetDefaultThumbnail()
    {
        _defaultThumbnail ??= _defaultIconGenerator.CreateMusicalNoteIcon();
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
                                // Delegate loading to AlbumArtLoader
                                loadedThumbnail = await _albumArtLoader.LoadFromFileAsync(request.SongToUpdate.FilePath);

                                // Callback needs to be on UI thread as it might update UI-bound properties
                                await Dispatcher.UIThread.InvokeAsync(() =>
                                {
                                    request.Callback(request.SongToUpdate, loadedThumbnail);
                                }, DispatcherPriority.Background, _cts.Token);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            Debug.WriteLine($"[ThumbnailService] Thumbnail loading task (via AlbumArtLoader) cancelled for {request.SongToUpdate.Title}.");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[ThumbnailService] Error during thumbnail processing for {request.SongToUpdate.Title}: {ex.Message}");
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
            Debug.WriteLine("[ThumbnailService] ProcessQueueAsync task itself cancelled.");
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