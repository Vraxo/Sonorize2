using System;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace Sonorize.Services.Playback;

internal class NAudioPipeline : IDisposable
{
    public AudioFileReader AudioReader { get; private set; }
    public NAudioEffectsProcessor EffectsProcessor { get; private set; }
    public IWavePlayer OutputDevice { get; private set; }

    private readonly EventHandler<StoppedEventArgs> _enginePlaybackStoppedHandler;

    public NAudioPipeline(string filePath, float initialRate, float initialPitch, EventHandler<StoppedEventArgs> enginePlaybackStoppedHandler)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        _enginePlaybackStoppedHandler = enginePlaybackStoppedHandler ?? throw new ArgumentNullException(nameof(enginePlaybackStoppedHandler));
        
        try
        {
            AudioReader = new AudioFileReader(filePath);
            Debug.WriteLine($"[Pipeline] Loaded AudioFileReader. Channels: {AudioReader.WaveFormat.Channels}, SampleRate: {AudioReader.WaveFormat.SampleRate}, Duration: {AudioReader.TotalTime}");

            EffectsProcessor = new NAudioEffectsProcessor();
            EffectsProcessor.Initialize(AudioReader.ToSampleProvider());
            EffectsProcessor.Tempo = initialRate;
            EffectsProcessor.PitchSemitones = initialPitch;
            Debug.WriteLine($"[Pipeline] Effects Processor initialized. Tempo: {EffectsProcessor.Tempo}, Pitch: {EffectsProcessor.PitchSemitones}");

            OutputDevice = new WaveOutEvent();
            OutputDevice.PlaybackStopped += OnOutputDevicePlaybackStopped; // Subscribe to the actual device
            OutputDevice.Init(EffectsProcessor.OutputProvider.ToWaveProvider());
            Debug.WriteLine($"[Pipeline] NAudio pipeline created successfully for: {Path.GetFileName(filePath)}.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Pipeline] CRITICAL ERROR during NAudio pipeline creation for {Path.GetFileName(filePath)}: {ex.ToString()}");
            Dispose(); // Clean up if constructor fails partially
            throw; // Re-throw to allow NAudioPlaybackEngine to handle
        }
    }

    private void OnOutputDevicePlaybackStopped(object? sender, StoppedEventArgs e)
    {
        // Forward the event to the handler provided by NAudioPlaybackEngine
        _enginePlaybackStoppedHandler.Invoke(this, e); // Pass 'this' (NAudioPipeline) as sender, or OutputDevice
    }

    public void Dispose()
    {
        if (OutputDevice != null)
        {
            OutputDevice.PlaybackStopped -= OnOutputDevicePlaybackStopped;
            OutputDevice.Dispose();
        }
        EffectsProcessor?.Dispose();
        AudioReader?.Dispose();
        Debug.WriteLine("[Pipeline] Disposed NAudioPipeline resources.");
    }
}