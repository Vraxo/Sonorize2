using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NAudio.Wave;

namespace Sonorize.Services;

public class NAudioWaveformPointGenerator
{
    public List<WaveformPoint> Generate(string filePath, int targetPoints)
    {
        if (!WaveformProcessingLogic.ValidateInput(filePath, targetPoints, out var validationErrorPoints))
        {
            return validationErrorPoints;
        }

        Debug.WriteLine($"[NAudioWaveformPointGenerator] Generating waveform for \"{Path.GetFileName(filePath)}\". Target points: {targetPoints}.");

        try
        {
            using var reader = WaveformProcessingLogic.TryInitializeReader(filePath, out long totalSampleFrames);

            if (reader is null || totalSampleFrames == 0)
            {
                return [];
            }

            if (!WaveformProcessingLogic.CalculateProcessingParameters(totalSampleFrames,
                targetPoints,
                reader.WaveFormat.Channels,
                out int samplesPerFrameToProcessPerPoint,
                out int bufferSizeInSamples,
                out var paramErrorPoints))
            {
                return paramErrorPoints;
            }

            return WaveformProcessingLogic.ProcessAudioStream(reader, targetPoints, samplesPerFrameToProcessPerPoint, bufferSizeInSamples, filePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NAudioWaveformPointGenerator] CRITICAL Error generating waveform for \"{Path.GetFileName(filePath)}\": {ex.ToString()}");
            return [];
        }
    }
}