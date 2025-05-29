using System;
using System.Diagnostics;

namespace Sonorize.Services.Playback;

public class PlaybackInfrastructureProvider : IDisposable
{
    public NAudioEngineController EngineController { get; }
    public PlaybackMonitor Monitor { get; }
    public PlaybackEngineCoordinator Coordinator { get; }

    private bool _disposed = false;

    public PlaybackInfrastructureProvider(PlaybackLoopHandler loopHandler)
    {
        ArgumentNullException.ThrowIfNull(loopHandler);

        EngineController = new NAudioEngineController();
        Monitor = new PlaybackMonitor(EngineController, loopHandler);
        Coordinator = new PlaybackEngineCoordinator(EngineController, loopHandler, Monitor);
        
        Debug.WriteLine("[PlaybackInfrastructureProvider] Initialized and components created.");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Debug.WriteLine("[PlaybackInfrastructureProvider] Disposing components.");
            Coordinator?.Dispose();
        }

        _disposed = true;

        Debug.WriteLine("[PlaybackInfrastructureProvider] Dispose finished.");
    }

    ~PlaybackInfrastructureProvider()
    {
        Dispose(false);
    }
}