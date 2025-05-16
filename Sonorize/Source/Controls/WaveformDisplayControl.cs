using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using Sonorize.Models; // For LoopRegion
using Sonorize.Services; // For WaveformPoint

namespace Sonorize.Controls;

public class WaveformDisplayControl : Control
{
    // Background Property (NEW)
    public static readonly StyledProperty<IBrush?> BackgroundProperty =
        Border.BackgroundProperty.AddOwner<WaveformDisplayControl>();

    public IBrush? Background
    {
        get => GetValue(BackgroundProperty);
        set => SetValue(BackgroundProperty, value);
    }

    public static readonly StyledProperty<List<WaveformPoint>> WaveformPointsProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, List<WaveformPoint>>(nameof(WaveformPoints), defaultValue: new List<WaveformPoint>());

    public List<WaveformPoint> WaveformPoints
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

        if (WaveformPoints != null && WaveformPoints.Count > 1)
        {
            for (int i = 0; i < WaveformPoints.Count; i++)
            {
                var point = WaveformPoints[i];
                var x = point.X * width;
                // Corrected: Use YPeak instead of Y
                var yPeakValue = point.YPeak * (height / 2);
                context.DrawLine(waveformPen, new Point(x, height / 2 - yPeakValue), new Point(x, height / 2 + yPeakValue));
            }
        }
        else
        {
            // Draw a flat line if no points or only one point
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