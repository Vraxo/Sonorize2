using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO; // Required for Path.GetFileName
using NAudio.Wave;

namespace Sonorize.Services;

public class NAudioWaveformPointGenerator
{
    public List<WaveformPoint> Generate(string filePath, int targetPoints)
    {
        if (string.IsNullOrEmpty(filePath) || targetPoints <= 0)
        {
            Debug.WriteLine($"[NAudioWaveformPointGenerator] Invalid input: filePath is null/empty or targetPoints <= 0. File: '{filePath}', Points: {targetPoints}");
            return [];
        }

        Debug.WriteLine($"[NAudioWaveformPointGenerator] Generating waveform for \"{Path.GetFileName(filePath)}\". Target points: {targetPoints}.");
        List<WaveformPoint> points = [];

        try
        {
            using var reader = new AudioFileReader(filePath);
            Debug.WriteLine($"[NAudioWaveformPointGeneratorReader] File: \"{Path.GetFileName(filePath)}\", TotalTime: {reader.TotalTime}, Channels: {reader.WaveFormat.Channels}, SampleRate: {reader.WaveFormat.SampleRate}, BitsPerSample: {reader.WaveFormat.BitsPerSample}, Encoding: {reader.WaveFormat.Encoding}, BlockAlign: {reader.WaveFormat.BlockAlign}, Length (bytes): {reader.Length}");

            if (reader.WaveFormat.BlockAlign == 0)
            {
                Debug.WriteLine($"[NAudioWaveformPointGeneratorReader] File \"{Path.GetFileName(filePath)}\" has BlockAlign = 0. Cannot calculate total sample frames.");
                return points;
            }

            long totalSampleFrames = reader.Length / reader.WaveFormat.BlockAlign;

            if (totalSampleFrames == 0)
            {
                Debug.WriteLine($"[NAudioWaveformPointGeneratorReader] File \"{Path.GetFileName(filePath)}\" has 0 sample frames (Length: {reader.Length}, BlockAlign: {reader.WaveFormat.BlockAlign}). Cannot generate waveform.");
                return points;
            }

            var samplesPerFrameToProcessPerPoint = (int)Math.Max(1, totalSampleFrames / targetPoints);
            var bufferSizeInSamples = samplesPerFrameToProcessPerPoint * reader.WaveFormat.Channels;

            if (bufferSizeInSamples == 0)
            {
                Debug.WriteLine($"[NAudioWaveformPointGeneratorReader] Calculated bufferSizeInSamples is 0 for \"{Path.GetFileName(filePath)}\". TotalSampleFrames: {totalSampleFrames}, TargetPoints: {targetPoints}, Channels: {reader.WaveFormat.Channels}, SamplesPerFrameToProcessPerPoint: {samplesPerFrameToProcessPerPoint}. Cannot generate.");
                return points;
            }

            var buffer = new float[bufferSizeInSamples];
            int samplesReadFromAudioFile;
            double currentX = 0;
            double xIncrement = 1.0 / targetPoints;
            int pointsGeneratedCount = 0;

            Debug.WriteLine($"[NAudioWaveformPointGeneratorReader] Processing \"{Path.GetFileName(filePath)}\": TotalSampleFrames: {totalSampleFrames}, TargetPoints: {targetPoints}, SamplesPerFrameToProcessPerPoint: {samplesPerFrameToProcessPerPoint}, BufferSizeInFloats: {bufferSizeInSamples}");

            for (int i = 0; i < targetPoints; i++)
            {
                float maxPeakInChunk = 0f;

                samplesReadFromAudioFile = reader.Read(buffer, 0, buffer.Length);

                if (samplesReadFromAudioFile == 0)
                {
                    Debug.WriteLine($"[NAudioWaveformPointGeneratorReader] Read 0 samples at waveform point index {i} (target: {targetPoints}) for \"{Path.GetFileName(filePath)}\". End of audio stream reached.");
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
                    Debug.WriteLine($"[NAudioWaveformPointGeneratorReader] Point {i}: X={currentX:F3}, Calculated YPeak={maxPeakInChunk:F4}, SamplesInThisChunk={samplesReadFromAudioFile}");
                }

                currentX += xIncrement;
                if (currentX > 1.0) currentX = 1.0;
            }
            Debug.WriteLine($"[NAudioWaveformPointGeneratorReader] Loop finished for \"{Path.GetFileName(filePath)}\". Total waveform points generated: {pointsGeneratedCount}. (Target was {targetPoints})");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NAudioWaveformPointGenerator] CRITICAL Error generating waveform for \"{Path.GetFileName(filePath)}\": {ex.ToString()}");
            return []; // Return empty list on error
        }

        return points;
    }
}