using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace Sonorize.Services;

internal static class WaveformProcessingLogic
{
    internal static bool ValidateInput(string filePath, int targetPoints, out List<WaveformPoint> errorResult)
    {
        errorResult = [];

        if (string.IsNullOrEmpty(filePath) || targetPoints <= 0)
        {
            Debug.WriteLine($"[WaveformProcessingLogic] Invalid input: filePath is null/empty or targetPoints <= 0. File: '{filePath}', Points: {targetPoints}");
            return false;
        }

        return true;
    }

    internal static AudioFileReader? TryInitializeReader(string filePath, out long totalSampleFrames)
    {
        totalSampleFrames = 0;
        AudioFileReader? reader;

        try
        {
            reader = new(filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WaveformProcessingLogic] Failed to initialize AudioFileReader for \"{Path.GetFileName(filePath)}\": {ex.Message}");
            return null;
        }

        Debug.WriteLine($"[WaveformProcessingLogicReader] File: \"{Path.GetFileName(filePath)}\", TotalTime: {reader.TotalTime}, Channels: {reader.WaveFormat.Channels}, SampleRate: {reader.WaveFormat.SampleRate}, BitsPerSample: {reader.WaveFormat.BitsPerSample}, Encoding: {reader.WaveFormat.Encoding}, BlockAlign: {reader.WaveFormat.BlockAlign}, Length (bytes): {reader.Length}");

        if (reader.WaveFormat.BlockAlign == 0)
        {
            Debug.WriteLine($"[WaveformProcessingLogicReader] File \"{Path.GetFileName(filePath)}\" has BlockAlign = 0. Cannot calculate total sample frames.");
            reader.Dispose();
            return null;
        }

        totalSampleFrames = reader.Length / reader.WaveFormat.BlockAlign;

        if (totalSampleFrames == 0)
        {
            Debug.WriteLine($"[WaveformProcessingLogicReader] File \"{Path.GetFileName(filePath)}\" has 0 sample frames (Length: {reader.Length}, BlockAlign: {reader.WaveFormat.BlockAlign}). Cannot generate waveform.");
            reader.Dispose();
            return null;
        }

        return reader;
    }

    internal static bool CalculateProcessingParameters(
        long totalSampleFrames,
        int targetPoints,
        int channels,
        out int samplesPerFrameToProcessPerPoint,
        out int bufferSizeInSamples,
        out List<WaveformPoint> errorResult)
    {
        errorResult = [];
        samplesPerFrameToProcessPerPoint = (int)Math.Max(1, totalSampleFrames / targetPoints);
        bufferSizeInSamples = samplesPerFrameToProcessPerPoint * channels;

        if (bufferSizeInSamples == 0)
        {
            Debug.WriteLine($"[WaveformProcessingLogicReader] Calculated bufferSizeInSamples is 0. TotalSampleFrames: {totalSampleFrames}, TargetPoints: {targetPoints}, Channels: {channels}, SamplesPerFrameToProcessPerPoint: {samplesPerFrameToProcessPerPoint}. Cannot generate.");
            return false;
        }

        return true;
    }

    internal static List<WaveformPoint> ProcessAudioStream(AudioFileReader reader, int targetPoints, int samplesPerFrameToProcessPerPoint, int bufferSizeInSamples, string filePath)
    {
        List<WaveformPoint> points = [];
        float[] buffer = new float[bufferSizeInSamples]; // Corrected buffer initialization
        double currentX = 0;
        double xIncrement = 1.0 / targetPoints;
        int pointsGeneratedCount = 0;

        Debug.WriteLine($"[WaveformProcessingLogicReader] Processing \"{Path.GetFileName(filePath)}\": TargetPoints: {targetPoints}, SamplesPerFrameToProcessPerPoint: {samplesPerFrameToProcessPerPoint}, BufferSizeInFloats: {bufferSizeInSamples}");

        for (int i = 0; i < targetPoints; i++)
        {
            float maxPeakInChunk = 0f;
            int samplesReadFromAudioFile = reader.Read(buffer, 0, buffer.Length);

            if (samplesReadFromAudioFile == 0)
            {
                Debug.WriteLine($"[WaveformProcessingLogicReader] Read 0 samples at waveform point index {i} (target: {targetPoints}) for \"{Path.GetFileName(filePath)}\". End of audio stream reached.");
                break;
            }

            for (int n = 0; n < samplesReadFromAudioFile; n++)
            {
                maxPeakInChunk = float.Max(maxPeakInChunk, float.Abs(buffer[n]));
            }

            points.Add(new WaveformPoint(currentX, maxPeakInChunk));
            pointsGeneratedCount++;

            if (i < 5 || (i > 0 && i % Math.Max(1, (targetPoints / 10)) == 0) || i == targetPoints - 1) // Corrected int.Max to Math.Max
            {
                Debug.WriteLine($"[WaveformProcessingLogicReader] Point {i}: X={currentX:F3}, Calculated YPeak={maxPeakInChunk:F4}, SamplesInThisChunk={samplesReadFromAudioFile}");
            }

            currentX += xIncrement;

            if (currentX > 1.0)
            {
                currentX = 1.0;
            }
        }

        Debug.WriteLine($"[WaveformProcessingLogicReader] Loop finished for \"{Path.GetFileName(filePath)}\". Total waveform points generated: {pointsGeneratedCount}. (Target was {targetPoints})");
        return points;
    }
}