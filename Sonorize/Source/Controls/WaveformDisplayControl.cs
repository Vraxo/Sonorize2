using System;
using System.Collections.Generic;
using System.Linq; // Required for Enumerable.Empty
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Sonorize.Models; // For LoopRegion
using Sonorize.Services; // For WaveformPoint

namespace Sonorize.Controls;

public class WaveformDisplayControl : Control
{
    private readonly WaveformRenderer _renderer = new();

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

        _renderer.DrawBackground(context, Bounds, Background);
        _renderer.DrawWaveform(context, Bounds, WaveformPoints, WaveformBrush);
        _renderer.DrawLoopRegion(context, Bounds, ActiveLoop, Duration, LoopRegionBrush);
        _renderer.DrawPositionMarker(context, Bounds, CurrentPosition, Duration, PositionMarkerBrush);
    }
}