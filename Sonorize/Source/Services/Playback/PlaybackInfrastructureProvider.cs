using System;
using System.Diagnostics;

namespace Sonorize.Services.Playback;

public class PlaybackInfrastructureProvider : IDisposable
{
    public NAudioPlaybackEngine EngineController { get; } // Changed type
    public PlaybackMonitor Monitor { get; }
    public PlaybackEngineCoordinator Coordinator { get; }

    private bool _disposed = false;

    public PlaybackInfrastructureProvider(PlaybackLoopHandler loopHandler)
    {
        ArgumentNullException.ThrowIfNull(loopHandler);

        EngineController = new NAudioPlaybackEngine(); // Changed instantiation
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
            // EngineController is disposed by Coordinator if Coordinator owns it,
            // or needs to be disposed here if not fully owned by Coordinator.
            // Given Coordinator takes it, Coordinator should dispose it.
            // If EngineController was created and owned here, then: EngineController?.Dispose();
            // Current Coordinator.Dispose() disposes its _engineController.
        }

        _disposed = true;

        Debug.WriteLine("[PlaybackInfrastructureProvider] Dispose finished.");
    }

    ~PlaybackInfrastructureProvider()
    {
        Dispose(false);
    }
}