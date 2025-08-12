using System;
using Avalonia;
using Avalonia.Logging;
using NAudio.MediaFoundation; // Required for MediaFoundationApi

namespace Sonorize;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Initialize Media Foundation
        try
        {
            MediaFoundationApi.Startup();
            Console.WriteLine("[Program] MediaFoundationApi.Startup() called successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Program] CRITICAL ERROR: MediaFoundationApi.Startup() failed: {ex.Message}");
            // Optionally, prevent the app from starting or notify the user,
            // as MF features will likely not work.
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Shutdown Media Foundation
            try
            {
                MediaFoundationApi.Shutdown();
                Console.WriteLine("[Program] MediaFoundationApi.Shutdown() called successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Program] ERROR: MediaFoundationApi.Shutdown() failed: {ex.Message}");
            }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace(level: LogEventLevel.Warning);
}