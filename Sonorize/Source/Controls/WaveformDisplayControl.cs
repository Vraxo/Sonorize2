using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq; // Required for Enumerable.Empty
using Sonorize.Models; // For LoopRegion
using Sonorize.Services; // For WaveformPoint

namespace Sonorize.Controls;

public class WaveformDisplayControl : Control
{
    // Background Property
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<WaveformDisplayControl>();

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    // Corrected to use IEnumerable<WaveformPoint>
    public static readonly StyledProperty<IEnumerable<WaveformPoint>> WaveformPointsProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, IEnumerable<WaveformPoint>>(
            nameof(WaveformPoints),
            defaultValue: Enumerable.Empty<WaveformPoint>()); // Default to an empty enumerable

    public IEnumerable<WaveformPoint> WaveformPoints
    {
        get => GetValue(WaveformPointsProperty);
        set => SetValue(WaveformPointsProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> CurrentPositionProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, TimeSpan>(nameof(CurrentPosition));

    public TimeSpan CurrentPosition
    {
        get => GetValue(CurrentPositionProperty);
        set => SetValue(CurrentPositionProperty, value);
    }

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, TimeSpan>(nameof(Duration), TimeSpan.FromSeconds(1));

    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public static readonly StyledProperty<LoopRegion?> ActiveLoopProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, LoopRegion?>(nameof(ActiveLoop));

    public LoopRegion? ActiveLoop
    {
        get => GetValue(ActiveLoopProperty);
        set => SetValue(ActiveLoopProperty, value);
    }

    public static readonly StyledProperty<IBrush> WaveformBrushProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, IBrush>(nameof(WaveformBrush), Brushes.DodgerBlue);
    public IBrush WaveformBrush { get => GetValue(WaveformBrushProperty); set => SetValue(WaveformBrushProperty, value); }

    public static readonly StyledProperty<IBrush> PositionMarkerBrushProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, IBrush>(nameof(PositionMarkerBrush), Brushes.Red);
    public IBrush PositionMarkerBrush { get => GetValue(PositionMarkerBrushProperty); set => SetValue(PositionMarkerBrushProperty, value); }

    public static readonly StyledProperty<IBrush> LoopRegionBrushProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, IBrush>(nameof(LoopRegionBrush), new SolidColorBrush(Colors.Orange, 0.3));
    public IBrush LoopRegionBrush { get => GetValue(LoopRegionBrushProperty); set => SetValue(LoopRegionBrushProperty, value); }


    public event EventHandler<TimeSpan>? SeekRequested;

    static WaveformDisplayControl()
    {
        AffectsRender<WaveformDisplayControl>(BackgroundProperty, WaveformPointsProperty, CurrentPositionProperty, DurationProperty, ActiveLoopProperty, WaveformBrushProperty, PositionMarkerBrushProperty, LoopRegionBrushProperty);
    }

    public WaveformDisplayControl()
    {
        ClipToBounds = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Duration.TotalSeconds > 0 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            var position = e.GetPosition(this);
            var relativeX = position.X / Bounds.Width;
            var seekTime = TimeSpan.FromSeconds(relativeX * Duration.TotalSeconds);
            SeekRequested?.Invoke(this, seekTime);
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var width = Bounds.Width;
        var height = Bounds.Height;

        if (width <= 0 || height <= 0) return;

        // Draw Background
        if (Background != null)
        {
            context.FillRectangle(Background, Bounds);
        }

        var waveformPen = new Pen(WaveformBrush, 1);
        var positionPen = new Pen(PositionMarkerBrush, 1.5);

        // WaveformPoints is now IEnumerable<WaveformPoint>
        if (WaveformPoints != null && WaveformPoints.Any()) // Use .Any() for IEnumerable
        {
            // If we need Count or indexed access, we might need to ToList() it here,
            // but for simple iteration, this is fine.
            // For performance with potentially large IEnumerable, if Count is needed multiple times,
            // convert to List once.
            var pointsList = WaveformPoints as List<WaveformPoint> ?? WaveformPoints.ToList();
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
            else if (pointsList.Count == 1) // Draw a small vertical line for a single point
            {
                var point = pointsList[0];
                var x = point.X * width;
                var yPeakValue = point.YPeak * (height / 2);
                context.DrawLine(waveformPen, new Point(x, height / 2 - yPeakValue), new Point(x, height / 2 + yPeakValue));
            }
            else // No points but not null (e.g., empty collection)
            {
                context.DrawLine(waveformPen, new Point(0, height / 2), new Point(width, height / 2));
            }
        }
        else // WaveformPoints is null or empty
        {
            context.DrawLine(waveformPen, new Point(0, height / 2), new Point(width, height / 2));
        }

        if (ActiveLoop != null && Duration.TotalSeconds > 0)
        {
            var loopStartRatio = ActiveLoop.Start.TotalSeconds / Duration.TotalSeconds;
            var loopEndRatio = ActiveLoop.End.TotalSeconds / Duration.TotalSeconds;
            var loopStartX = loopStartRatio * width;
            var loopEndX = loopEndRatio * width;
            if (loopEndX > loopStartX)
            {
                context.FillRectangle(LoopRegionBrush, new Rect(loopStartX, 0, loopEndX - loopStartX, height));
            }
        }

        if (Duration.TotalSeconds > 0)
        {
            var currentX = (CurrentPosition.TotalSeconds / Duration.TotalSeconds) * width;
            currentX = Math.Clamp(currentX, 0, width);
            context.DrawLine(positionPen, new Point(currentX, 0), new Point(currentX, height));
        }
    }
}