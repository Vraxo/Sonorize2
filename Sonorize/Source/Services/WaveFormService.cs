using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO; // Required for Path.GetFileName

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
        {
            Debug.WriteLine($"[WaveformService] Invalid input: filePath is null/empty or targetPoints <= 0. File: '{filePath}', Points: {targetPoints}");
            return [];
        }

        // For debugging, temporarily disable cache to ensure fresh generation
        // if (_waveformCache.ContainsKey(filePath)) _waveformCache.Remove(filePath);

        if (_waveformCache.TryGetValue(filePath, out var cachedData))
        {
            Debug.WriteLine($"[WaveformService] Returning cached waveform for \"{Path.GetFileName(filePath)}\". Points: {cachedData.Count}");
            return cachedData;
        }

        Debug.WriteLine($"[WaveformService] Generating waveform for \"{Path.GetFileName(filePath)}\". Target points: {targetPoints}.");
        List<WaveformPoint> points = [];

        try
        {
            await Task.Run(() =>
            {
                using var reader = new AudioFileReader(filePath);
                Debug.WriteLine($"[WaveformServiceReader] File: \"{Path.GetFileName(filePath)}\", TotalTime: {reader.TotalTime}, Channels: {reader.WaveFormat.Channels}, SampleRate: {reader.WaveFormat.SampleRate}, BitsPerSample: {reader.WaveFormat.BitsPerSample}, Encoding: {reader.WaveFormat.Encoding}, BlockAlign: {reader.WaveFormat.BlockAlign}, Length (bytes): {reader.Length}");

                if (reader.WaveFormat.BlockAlign == 0)
                {
                    Debug.WriteLine($"[WaveformServiceReader] File \"{Path.GetFileName(filePath)}\" has BlockAlign = 0. Cannot calculate total sample frames.");
                    return;
                }

                long totalSampleFrames = reader.Length / reader.WaveFormat.BlockAlign;

                if (totalSampleFrames == 0)
                {
                    Debug.WriteLine($"[WaveformServiceReader] File \"{Path.GetFileName(filePath)}\" has 0 sample frames (Length: {reader.Length}, BlockAlign: {reader.WaveFormat.BlockAlign}). Cannot generate waveform.");
                    return;
                }

                var samplesPerFrameToProcessPerPoint = (int)Math.Max(1, totalSampleFrames / targetPoints);
                var bufferSizeInSamples = samplesPerFrameToProcessPerPoint * reader.WaveFormat.Channels;

                if (bufferSizeInSamples == 0)
                {
                    Debug.WriteLine($"[WaveformServiceReader] Calculated bufferSizeInSamples is 0 for \"{Path.GetFileName(filePath)}\". TotalSampleFrames: {totalSampleFrames}, TargetPoints: {targetPoints}, Channels: {reader.WaveFormat.Channels}, SamplesPerFrameToProcessPerPoint: {samplesPerFrameToProcessPerPoint}. Cannot generate.");
                    return;
                }

                var buffer = new float[bufferSizeInSamples];
                int samplesReadFromAudioFile;
                double currentX = 0;
                double xIncrement = 1.0 / targetPoints;
                int pointsGeneratedCount = 0;

                Debug.WriteLine($"[WaveformServiceReader] Processing \"{Path.GetFileName(filePath)}\": TotalSampleFrames: {totalSampleFrames}, TargetPoints: {targetPoints}, SamplesPerFrameToProcessPerPoint: {samplesPerFrameToProcessPerPoint}, BufferSizeInFloats: {bufferSizeInSamples}");

                for (int i = 0; i < targetPoints; i++)
                {
                    float maxPeakInChunk = 0f;

                    samplesReadFromAudioFile = reader.Read(buffer, 0, buffer.Length);

                    if (samplesReadFromAudioFile == 0)
                    {
                        Debug.WriteLine($"[WaveformServiceReader] Read 0 samples at waveform point index {i} (target: {targetPoints}) for \"{Path.GetFileName(filePath)}\". End of audio stream reached.");
                        break;
                    }

                    for (int n = 0; n < samplesReadFromAudioFile; n++)
                    {
                        maxPeakInChunk = Math.Max(maxPeakInChunk, Math.Abs(buffer[n]));
                    }

                    points.Add(new WaveformPoint(currentX, maxPeakInChunk));
                    pointsGeneratedCount++;

                    if (i < 5 || (i > 0 && i % (targetPoints / 10) == 0) || i == targetPoints - 1)
                    {
                        Debug.WriteLine($"[WaveformServiceReader] Point {i}: X={currentX:F3}, Calculated YPeak={maxPeakInChunk:F4}, SamplesInThisChunk={samplesReadFromAudioFile}");
                    }

                    currentX += xIncrement;
                    if (currentX > 1.0) currentX = 1.0;
                }
                Debug.WriteLine($"[WaveformServiceReader] Loop finished for \"{Path.GetFileName(filePath)}\". Total waveform points generated: {pointsGeneratedCount}. (Target was {targetPoints})");
            });

            if (points.Any())
            {
                _waveformCache[filePath] = points;
                Debug.WriteLine($"[WaveformService] Waveform generated and cached for \"{Path.GetFileName(filePath)}\", {points.Count} points. First point YPeak: {points[0].YPeak:F4}. Approx mid point YPeak: {points[points.Count / 2].YPeak:F4}. Last point YPeak: {points.Last().YPeak:F4}");
            }
            else
            {
                Debug.WriteLine($"[WaveformService] No points generated for \"{Path.GetFileName(filePath)}\". It might be too short, silent, or an issue with reading audio data.");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WaveformService] CRITICAL Error generating waveform for \"{Path.GetFileName(filePath)}\": {ex.ToString()}");
            return [];
        }
        return points;
    }

    public void ClearCache()
    {
        _waveformCache.Clear();
        Debug.WriteLine("[WaveformService] Cache cleared.");
    }
}