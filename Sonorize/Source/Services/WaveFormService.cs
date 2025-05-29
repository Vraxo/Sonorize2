using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sonorize.Services;

public record WaveformPoint(double X, double YPeak);

public class WaveformService
{
    private readonly Dictionary<string, List<WaveformPoint>> _waveformCache = new();
    private readonly NAudioWaveformPointGenerator _pointGenerator;

    public WaveformService()
    {
        _pointGenerator = new NAudioWaveformPointGenerator();
    }

    public async Task<List<WaveformPoint>> GetWaveformAsync(string filePath, int targetPoints)
    {
        if (string.IsNullOrEmpty(filePath) || targetPoints <= 0)
        {
            Debug.WriteLine($"[WaveformService] Invalid input: filePath is null/empty or targetPoints <= 0. File: '{filePath}', Points: {targetPoints}");
            return [];
        }

        if (_waveformCache.TryGetValue(filePath, out var cachedData))
        {
            Debug.WriteLine($"[WaveformService] Returning cached waveform for \"{Path.GetFileName(filePath)}\". Points: {cachedData.Count}");
            return cachedData;
        }

        Debug.WriteLine($"[WaveformService] Requesting waveform generation for \"{Path.GetFileName(filePath)}\". Target points: {targetPoints}.");

        List<WaveformPoint> points = await Task.Run(() 
            => _pointGenerator.Generate(filePath, targetPoints));

        if (points.Count != 0)
        {
            _waveformCache[filePath] = points;
            Debug.WriteLine($"[WaveformService] Waveform generated and cached for \"{Path.GetFileName(filePath)}\", {points.Count} points. First point YPeak: {points[0].YPeak:F4}. Approx mid point YPeak: {points[points.Count / 2].YPeak:F4}. Last point YPeak: {points.Last().YPeak:F4}");
        }
        else
        {
            Debug.WriteLine($"[WaveformService] No points generated for \"{Path.GetFileName(filePath)}\". It might be too short, silent, or an issue with reading audio data.");
        }

        return points;
    }

    public void ClearCache()
    {
        _waveformCache.Clear();
        Debug.WriteLine("[WaveformService] Cache cleared.");
    }
}