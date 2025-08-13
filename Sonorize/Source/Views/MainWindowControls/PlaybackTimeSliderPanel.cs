using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb, Track
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Sonorize.Models; // For ThemeColors
using Sonorize.ViewModels;

namespace Sonorize.Views.MainWindowControls;

public static class PlaybackTimeSliderPanel
{
    public static Grid Create(ThemeColors theme)
    {
        // TextBlock for Current Time
        var currentTimeTextBlock = new TextBlock
        {
            Foreground = theme.B_SecondaryTextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            MinWidth = 40,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        currentTimeTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentTimeDisplay"));
        currentTimeTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        // TextBlock for Total Time
        var totalTimeTextBlock = new TextBlock
        {
            Foreground = theme.B_SecondaryTextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0),
            MinWidth = 40,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        totalTimeTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.TotalTimeDisplay"));
        totalTimeTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        var mainPlaybackSlider = new Slider
        {
            Name = "MainPlaybackSliderInstance",
            Minimum = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = theme.B_SecondaryTextColor, // Inactive part of the track
            Foreground = theme.B_AccentColor,        // Active part of the track
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        // Add a style for the slider's thumb to make it visible and circular
        mainPlaybackSlider.Styles.Add(new Style(s => s.OfType<Slider>().Descendant().Is<Thumb>())
        {
            Setters =
            {
                new Setter(Thumb.BackgroundProperty, theme.B_AccentColor),
                new Setter(Thumb.BorderThicknessProperty, new Thickness(0)),
                new Setter(Thumb.CornerRadiusProperty, new CornerRadius(7)),
                new Setter(Thumb.WidthProperty, 14.0),
                new Setter(Thumb.HeightProperty, 14.0),
            }
        });

        // Optional: Style for the track height
        mainPlaybackSlider.Styles.Add(new Style(s => s.OfType<Slider>().Descendant().Is<Track>())
        {
            Setters =
            {
                new Setter(Track.HeightProperty, 4.0)
            }
        });

        // --- New Event Handling for "Snap-and-Drag" ---

        mainPlaybackSlider.AddHandler(Slider.PointerPressedEvent, (sender, e) =>
        {
            if (sender is not Slider slider || slider.DataContext is not MainWindowViewModel { Playback: { } playbackVM }) return;

            var point = e.GetCurrentPoint(slider);
            if (!point.Properties.IsLeftButtonPressed) return;

            // Don't interfere with built-in thumb dragging
            var thumb = slider.FindDescendantOfType<Thumb>();
            if (thumb is not null && thumb.IsPointerOver)
            {
                return;
            }

            // This is a click on the track. Begin the operation.
            playbackVM.BeginSliderDrag();

            // Immediately move the slider to the clicked position
            var bounds = slider.Bounds;
            if (bounds.Width > 0)
            {
                var ratio = Math.Clamp(point.Position.X / bounds.Width, 0, 1);
                var newValue = slider.Minimum + (ratio * (slider.Maximum - slider.Minimum));
                slider.Value = newValue;
            }

            // Capture the pointer to receive PointerMoved events for dragging from the track.
            e.Pointer.Capture(slider);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);

        mainPlaybackSlider.AddHandler(Slider.PointerMovedEvent, (sender, e) =>
        {
            if (sender is not Slider slider) return;

            // Only process moves if we have captured the pointer (i.e., we are in a track-drag operation).
            if (e.Pointer.Captured != slider) return;

            var point = e.GetCurrentPoint(slider);
            var bounds = slider.Bounds;
            if (bounds.Width > 0)
            {
                var ratio = Math.Clamp(point.Position.X / bounds.Width, 0, 1);
                var newValue = slider.Minimum + (ratio * (slider.Maximum - slider.Minimum));
                slider.Value = newValue;
            }
        });

        mainPlaybackSlider.AddHandler(Slider.PointerReleasedEvent, (sender, e) =>
        {
            if (sender is not Slider slider || slider.DataContext is not MainWindowViewModel { Playback: { } playbackVM }) return;

            // Only process releases if we have captured the pointer.
            if (e.Pointer.Captured != slider) return;

            // Release pointer capture and finalize the seek.
            e.Pointer.Capture(null);
            playbackVM.CompleteSliderDrag();
            e.Handled = true;
        });

        // Add handlers specifically for the Thumb's native drag operations
        mainPlaybackSlider.AddHandler(Thumb.DragStartedEvent, (s, e) =>
        {
            if (s is Slider { DataContext: MainWindowViewModel { Playback: { } playbackVM } })
            {
                playbackVM.BeginSliderDrag();
            }
        });

        mainPlaybackSlider.AddHandler(Thumb.DragCompletedEvent, (s, e) =>
        {
            if (s is Slider { DataContext: MainWindowViewModel { Playback: { } playbackVM } })
            {
                playbackVM.CompleteSliderDrag();
            }
        });


        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.SliderPosition", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var timeSliderGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 500,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = false
        };

        Grid.SetColumn(currentTimeTextBlock, 0);
        Grid.SetColumn(mainPlaybackSlider, 1);
        Grid.SetColumn(totalTimeTextBlock, 2);

        timeSliderGrid.Children.Add(currentTimeTextBlock);
        timeSliderGrid.Children.Add(mainPlaybackSlider);
        timeSliderGrid.Children.Add(totalTimeTextBlock);

        return timeSliderGrid;
    }
}