using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Sonorize.Models; // For LoopRegion
using Sonorize.Services; // For WaveformPoint

namespace Sonorize.Controls
{
    public class WaveformRenderer
    {
        public void DrawBackground(DrawingContext context, Rect bounds, IBrush? backgroundBrush)
        {
            if (backgroundBrush != null)
            {
                context.FillRectangle(backgroundBrush, bounds);
            }
        }

        public void DrawWaveform(DrawingContext context, Rect bounds, IEnumerable<WaveformPoint> waveformPoints, IBrush waveformBrush)
        {
            var width = bounds.Width;
            var height = bounds.Height;
            var waveformPen = new Pen(waveformBrush, 1);

            if (waveformPoints != null && waveformPoints.Any())
            {
                var pointsList = waveformPoints as List<WaveformPoint> ?? waveformPoints.ToList();
                if (pointsList.Count > 1)
                {
                    for (int i = 0; i < pointsList.Count; i++)
                    {
                        var point = pointsList[i];
                        var x = point.X * width;
                        var yPeakValue = point.YPeak * (height / 2);
                        context.DrawLine(waveformPen, new Point(x, height / 2 - yPeakValue), new Point(x, height / 2 + yPeakValue));
                    }
                }
                else if (pointsList.Count == 1)
                {
                    var point = pointsList[0];
                    var x = point.X * width;
                    var yPeakValue = point.YPeak * (height / 2);
                    context.DrawLine(waveformPen, new Point(x, height / 2 - yPeakValue), new Point(x, height / 2 + yPeakValue));
                }
                else
                {
                    context.DrawLine(waveformPen, new Point(0, height / 2), new Point(width, height / 2));
                }
            }
            else
            {
                context.DrawLine(waveformPen, new Point(0, height / 2), new Point(width, height / 2));
            }
        }

        public void DrawLoopRegion(DrawingContext context, Rect bounds, LoopRegion? activeLoop, TimeSpan duration, IBrush loopRegionBrush)
        {
            if (activeLoop != null && duration.TotalSeconds > 0)
            {
                var width = bounds.Width;
                var height = bounds.Height;
                var loopStartRatio = activeLoop.Start.TotalSeconds / duration.TotalSeconds;
                var loopEndRatio = activeLoop.End.TotalSeconds / duration.TotalSeconds;
                var loopStartX = loopStartRatio * width;
                var loopEndX = loopEndRatio * width;
                if (loopEndX > loopStartX)
                {
                    context.FillRectangle(loopRegionBrush, new Rect(loopStartX, 0, loopEndX - loopStartX, height));
                }
            }
        }

        public void DrawPositionMarker(DrawingContext context, Rect bounds, TimeSpan currentPosition, TimeSpan duration, IBrush positionMarkerBrush)
        {
            if (duration.TotalSeconds > 0)
            {
                var width = bounds.Width;
                var height = bounds.Height;
                var positionPen = new Pen(positionMarkerBrush, 1.5);
                var currentX = (currentPosition.TotalSeconds / duration.TotalSeconds) * width;
                currentX = Math.Clamp(currentX, 0, width);
                context.DrawLine(positionPen, new Point(currentX, 0), new Point(currentX, height));
            }
        }
    }
}