using Avalonia;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Sonorize.Services;

// Represents a simplified data point for the waveform
public record WaveformPoint(double X, double YPeak);

public class WaveformService
{
    // Cache for waveform data to avoid reprocessing
    private readonly Dictionary<string, List<WaveformPoint>> _waveformCache = new();

    public async Task<List<WaveformPoint>> GetWaveformAsync(string filePath, int targetPoints)
    {
        if (string.IsNullOrEmpty(filePath) || targetPoints <= 0)
            return new List<WaveformPoint>();

        if (_waveformCache.TryGetValue(filePath, out var cachedData))
        {
            // Potentially resample cached data if targetPoints differs significantly,
            // or just return cached if good enough. For simplicity, returning cached.
            Debug.WriteLine($"[WaveformService] Returning cached waveform for {filePath}");
            return cachedData;
        }

        Debug.WriteLine($"[WaveformService] Generating waveform for {filePath} with {targetPoints} points.");
        List<WaveformPoint> points = new List<WaveformPoint>();

        try
        {
            await Task.Run(() =>
            {
                using (var reader = new AudioFileReader(filePath))
                {
                    var samples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
                    var samplesPerPoint = (int)Math.Max(1, samples / targetPoints / reader.WaveFormat.Channels);

                    if (samplesPerPoint == 0 || samples == 0)
                    {
                        Debug.WriteLine($"[WaveformService] Not enough samples or zero samplesPerPoint for {filePath}. Samples: {samples}, SPP: {samplesPerPoint}");
                        return; // Not enough data or too many target points
                    }

                    var buffer = new float[samplesPerPoint * reader.WaveFormat.Channels];
                    int samplesRead;
                    double currentX = 0;
                    double xIncrement = 1.0 / targetPoints;

                    for (int i = 0; i < targetPoints; i++)
                    {
                        float max = 0;
                        long currentSampleIndex = (long)i * samplesPerPoint * reader.WaveFormat.Channels;

                        // Ensure reader is positioned correctly if seeking is needed (not ideal for full scan)
                        // For a sequential read, this direct approach is okay.
                        // reader.Position = currentSampleIndex * (reader.WaveFormat.BitsPerSample / 8);

                        samplesRead = reader.Read(buffer, 0, buffer.Length);
                        if (samplesRead == 0) break;

                        for (int n = 0; n < samplesRead; n++)
                        {
                            max = Math.Max(max, Math.Abs(buffer[n]));
                        }
                        points.Add(new WaveformPoint(currentX, max)); // Y is normalized peak (0 to 1)
                        currentX += xIncrement;
                        if (currentX > 1.0) currentX = 1.0; // Cap at 1.0
                    }
                }
            });

            if (points.Any())
            {
                _waveformCache[filePath] = points; // Cache the result
                Debug.WriteLine($"[WaveformService] Waveform generated for {filePath}, {points.Count} points.");
            }
            else
            {
                Debug.WriteLine($"[WaveformService] No points generated for {filePath}. It might be too short or an issue with reading.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WaveformService] Error generating waveform for {filePath}: {ex.Message}");
            // Return empty list on error
            return new List<WaveformPoint>();
        }
        return points;
    }

    public void ClearCache()
    {
        _waveformCache.Clear();
        Debug.WriteLine("[WaveformService] Cache cleared.");
    }
}