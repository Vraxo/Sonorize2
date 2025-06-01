using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.Controls;

public class WaveformRenderer
{
    public static void DrawBackground(DrawingContext context, Rect bounds, IBrush? backgroundBrush)
    {
        if (backgroundBrush is null)
        {
            return;
        }

        context.FillRectangle(backgroundBrush, bounds);
    }

    public static void DrawWaveform(DrawingContext context, Rect bounds, IEnumerable<WaveformPoint> waveformPoints, IBrush waveformBrush)
    {
        double width = bounds.Width;
        double height = bounds.Height;
        var waveformPen = new Pen(waveformBrush, 1);

        var pointsList = waveformPoints?.ToList(); // Materialize once, handles null waveformPoints

        if (pointsList is not null && pointsList.Count > 0)
        {
            for (int i = 0; i < pointsList.Count; i++)
            {
                WaveformPoint point = pointsList[i];
                double x = point.X * width;
                double yPeakMagnitude = point.YPeak * (height / 2);
                double centerY = height / 2;

                context.DrawLine(waveformPen, new Point(x, centerY - yPeakMagnitude), new Point(x, centerY + yPeakMagnitude));
            }
        }
        else
        {
            context.DrawLine(waveformPen, new Point(0, height / 2), new Point(width, height / 2));
        }
    }

    public static void DrawLoopRegion(DrawingContext context, Rect bounds, LoopRegion? activeLoop, TimeSpan duration, IBrush loopRegionBrush)
    {
        if (activeLoop is null || duration.TotalSeconds <= 0)
        {
            return;
        }

        double width = bounds.Width;
        double height = bounds.Height;
        double loopStartRatio = activeLoop.Start.TotalSeconds / duration.TotalSeconds;
        double loopEndRatio = activeLoop.End.TotalSeconds / duration.TotalSeconds;
        double loopStartX = loopStartRatio * width;
        double loopEndX = loopEndRatio * width;

        if (loopEndX <= loopStartX)
        {
            return;
        }

        context.FillRectangle(loopRegionBrush, new(loopStartX, 0, loopEndX - loopStartX, height));
    }

    public static void DrawPositionMarker(DrawingContext context, Rect bounds, TimeSpan currentPosition, TimeSpan duration, IBrush positionMarkerBrush)
    {
        if (duration.TotalSeconds <= 0)
        {
            return;
        }

        double width = bounds.Width;
        double height = bounds.Height;
        Pen positionPen = new(positionMarkerBrush, 1.5);
        double currentX = (currentPosition.TotalSeconds / duration.TotalSeconds) * width;
        currentX = Math.Clamp(currentX, 0, width);

        context.DrawLine(positionPen, new(currentX, 0), new(currentX, height));
    }
}