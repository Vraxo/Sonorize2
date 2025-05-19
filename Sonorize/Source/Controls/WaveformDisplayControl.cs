using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Sonorize.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Diagnostics;
using Sonorize.Services;

namespace Sonorize.Controls;

// This control is intended to be used within a ViewModel's DataContext.
// It expects its DataContext to be the ViewModel or have access to the ViewModel's properties.
// Bindings for properties like Data, Position, etc., should be set in XAML
// and will use the DataContext (MainWindowViewModel in this case) to resolve.
public class WaveformDisplayControl : Control
{
    // Define AvaloniaProperties for the bindable properties
    public static readonly StyledProperty<ObservableCollection<WaveformPoint>?> DataProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, ObservableCollection<WaveformPoint>?>(nameof(Data));

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, TimeSpan>(nameof(Duration));

    public static readonly StyledProperty<TimeSpan> PositionProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, TimeSpan>(nameof(Position));

    public static readonly StyledProperty<TimeSpan?> LoopStartProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, TimeSpan?>(nameof(LoopStart));

    public static readonly StyledProperty<TimeSpan?> LoopEndProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, TimeSpan?>(nameof(LoopEnd));

    public static readonly StyledProperty<TimeSpan?> NewLoopStartCandidateProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, TimeSpan?>(nameof(NewLoopStartCandidate));

    public static readonly StyledProperty<TimeSpan?> NewLoopEndCandidateProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, TimeSpan?>(nameof(NewLoopEndCandidate));

    public static readonly StyledProperty<IBrush> WaveformBrushProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, IBrush>(nameof(WaveformBrush), Brushes.DodgerBlue);

    public static readonly StyledProperty<IBrush> PositionMarkerBrushProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, IBrush>(nameof(PositionMarkerBrush), Brushes.Red);

    public static readonly StyledProperty<IBrush> LoopRegionBrushProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, IBrush>(nameof(LoopRegionBrush), Brushes.DarkOrange);

    public static readonly StyledProperty<bool> CanSeekProperty =
        AvaloniaProperty.Register<WaveformDisplayControl, bool>(nameof(CanSeek), true);

    // CLR properties wrapping the AvaloniaProperties
    public ObservableCollection<WaveformPoint>? Data
    {
        get => GetValue(DataProperty);
        set => SetValue(DataProperty, value);
    }

    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    public TimeSpan Position
    {
        get => GetValue(PositionProperty);
        set => SetValue(PositionProperty, value);
    }

    public TimeSpan? LoopStart
    {
        get => GetValue(LoopStartProperty);
        set => SetValue(LoopStartProperty, value);
    }

    public TimeSpan? LoopEnd
    {
        get => GetValue(LoopEndProperty);
        set => SetValue(LoopEndProperty, value);
    }

    public TimeSpan? NewLoopStartCandidate
    {
        get => GetValue(NewLoopStartCandidateProperty);
        set => SetValue(NewLoopStartCandidateProperty, value);
    }

    public TimeSpan? NewLoopEndCandidate
    {
        get => GetValue(NewLoopEndCandidateProperty);
        set => SetValue(NewLoopEndCandidateProperty, value);
    }

    public IBrush WaveformBrush
    {
        get => GetValue(WaveformBrushProperty)!;
        set => SetValue(WaveformBrushProperty, value);
    }

    public IBrush PositionMarkerBrush
    {
        get => GetValue(PositionMarkerBrushProperty)!;
        set => SetValue(PositionMarkerBrushProperty, value);
    }

    public IBrush LoopRegionBrush
    {
        get => GetValue(LoopRegionBrushProperty)!;
        set => SetValue(LoopRegionBrushProperty, value);
    }

    public bool CanSeek
    {
        get => GetValue(CanSeekProperty);
        set => SetValue(CanSeekProperty, value);
    }

    // Event for seek requests
    public event EventHandler<TimeSpan>? SeekRequested;

    public WaveformDisplayControl()
    {
        // Invalidate visual when any relevant property changes
        AffectsRender<WaveformDisplayControl>(
            DataProperty, DurationProperty, PositionProperty,
            LoopStartProperty, LoopEndProperty, NewLoopStartCandidateProperty,
            NewLoopEndCandidateProperty, WaveformBrushProperty, PositionMarkerBrushProperty,
            LoopRegionBrushProperty);

        // Subscribe to collection changes for Data
        this.GetObservable(DataProperty).Subscribe(data =>
        {
            if (data != null)
            {
                data.CollectionChanged += Data_CollectionChanged;
            }
            // Note: Proper handling of unsubscribing from old collection is needed
            // if the collection instance can be replaced.
            InvalidateVisual();
        });
    }

    private void Data_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Invalidate visual on collection changes (e.g., items added/removed/cleared)
        Dispatcher.UIThread.InvokeAsync(InvalidateVisual, DispatcherPriority.Background);
    }


    // Override Render to draw the waveform
    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        var width = bounds.Width;
        var height = bounds.Height;

        if (width <= 0 || height <= 0 || Data == null || Data.Count == 0)
        {
            // Draw background if no data
            if (Background is IBrush backgroundBrush)
            {
                context.FillRectangle(backgroundBrush, bounds);
            }
            return;
        }

        // Draw background
        if (Background is IBrush backgroundBrush)
        {
            context.FillRectangle(backgroundBrush, bounds);
        }

        // Draw waveform
        var waveformGeometry = new StreamGeometry();
        using (var ctx = waveformGeometry.Open())
        {
            var halfHeight = height / 2.0;
            var maxAmplitude = 1.0f; // Assuming WaveformPoint.Amplitude is normalized to [0, 1]

            // Calculate scale factors
            var xScale = width / Data.Count;
            var yScale = halfHeight / maxAmplitude; // Scale amplitude [0, 1] to [0, halfHeight]

            // Start path
            ctx.BeginFigure(new Point(0, halfHeight), true); // Start at the middle left

            // Draw upper half of the waveform
            for (int i = 0; i < Data.Count; i++)
            {
                var point = Data[i];
                var x = i * xScale;
                var y = halfHeight - point.Amplitude * yScale; // Map [0, 1] amplitude to [halfHeight, 0] y
                ctx.LineTo(new Point(x, y));
            }

            // Draw right edge
            ctx.LineTo(new Point(width, halfHeight));

            // Draw lower half of the waveform (mirroring the upper half)
            for (int i = Data.Count - 1; i >= 0; i--)
            {
                var point = Data[i];
                var x = i * xScale;
                var y = halfHeight + point.Amplitude * yScale; // Map [0, 1] amplitude to [halfHeight, height] y
                ctx.LineTo(new Point(x, y));
            }

            // Close the figure to create a filled shape
            ctx.EndFigure(true);
        }

        // Draw the waveform geometry with the specified brush
        context.DrawGeometry(WaveformBrush, null, waveformGeometry);

        // Draw Loop Regions (Saved Loop and Candidate Loop)
        DrawLoopRegion(context, bounds, LoopStart, LoopEnd, LoopRegionBrush);
        DrawLoopRegion(context, bounds, NewLoopStartCandidate, NewLoopEndCandidate, LoopRegionBrush);


        // Draw position marker
        if (Duration.TotalSeconds > 0)
        {
            var positionX = (Position.TotalSeconds / Duration.TotalSeconds) * width;
            context.DrawLine(new Pen(PositionMarkerBrush, 1),
                             new Point(positionX, 0),
                             new Point(positionX, height));
        }

        // Draw vertical center line
        context.DrawLine(new Pen(Brushes.Gray, 0.5), new Point(0, height / 2.0), new Point(width, height / 2.0));
    }

    private void DrawLoopRegion(DrawingContext context, Rect bounds, TimeSpan? start, TimeSpan? end, IBrush brush)
    {
        var width = bounds.Width;
        var durationSeconds = Duration.TotalSeconds;

        if (start.HasValue && end.HasValue && start.Value < end.Value && durationSeconds > 0)
        {
            var startX = (start.Value.TotalSeconds / durationSeconds) * width;
            var endX = (end.Value.TotalSeconds / durationSeconds) * width;

            if (startX < width && endX > 0) // Ensure region is at least partially visible
            {
                var regionRect = new Rect(Math.Max(0, startX), 0, Math.Min(width, endX) - Math.Max(0, startX), bounds.Height);
                context.FillRectangle(brush, regionRect);
            }
        }
    }

    // Handle pointer events for seeking
    protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
    {
        if (!CanSeek) return;
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
        {
            PerformSeek(e.GetCurrentPoint(this).Position.X);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(Avalonia.Input.PointerEventArgs e)
    {
        if (!CanSeek) return;
        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsLeftButtonPressed)
        {
            PerformSeek(e.GetCurrentPoint(this).Position.X);
            e.Handled = true;
        }
    }

    private void PerformSeek(double xPosition)
    {
        var width = Bounds.Width;
        var durationSeconds = Duration.TotalSeconds;

        if (width > 0 && durationSeconds > 0)
        {
            var seekPositionSeconds = (xPosition / width) * durationSeconds;
            seekPositionSeconds = Math.Max(0, Math.Min(durationSeconds, seekPositionSeconds)); // Clamp
            var seekTime = TimeSpan.FromSeconds(seekPositionSeconds);
            // Debug.WriteLine($"[WaveformDisplay] Seek requested to {seekTime}");
            SeekRequested?.Invoke(this, seekTime);
        }
    }
}
