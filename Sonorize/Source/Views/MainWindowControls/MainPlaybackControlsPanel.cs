using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging; // Required for BitmapInterpolationMode
using Avalonia.Styling;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels; // Required for RepeatMode enum
using System;
using System.Collections.Generic; // Required for IList<object>
using System.Diagnostics;
using System.Globalization; // Required for CultureInfo

namespace Sonorize.Views.MainWindowControls;

// This static class creates the entire main playback control bar at the bottom of the window.
public static class MainPlaybackControlsPanel
{
    public static Grid Create(ThemeColors theme)
    {
        var mainGrid = new Grid
        {
            Height = 80, // Fixed height for the whole panel
            MinHeight = 70,
            Background = theme.B_SlightlyLighterBackground,
            ColumnDefinitions = new ColumnDefinitions("250,*,250"), // Left (Song Info), Center (Slider/Buttons), Right (Filler/Volume)
            VerticalAlignment = VerticalAlignment.Center
        };

        // --- Left Section: Song Info ---
        var songInfoPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(10, 0, 0, 0),
            Spacing = 8,
        };
        songInfoPanel.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        var thumbnailImage = new Image
        {
            Width = 50, // Adjusted size
            Height = 50, // Adjusted size
            Source = null, // Will be bound
            Stretch = Stretch.UniformToFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        RenderOptions.SetBitmapInterpolationMode(thumbnailImage, BitmapInterpolationMode.HighQuality);
        thumbnailImage.Bind(Image.SourceProperty, new Binding("Playback.CurrentSong.Thumbnail"));

        var textStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 1
        };

        var titleTextBlock = new TextBlock
        {
            Text = "Unknown Title", // Default, will be bound
            FontSize = 13, // Adjusted size
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.B_TextColor,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 180 // Constrain width
        };
        titleTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Title"));

        var artistTextBlock = new TextBlock
        {
            Text = "Unknown Artist", // Default, will be bound
            FontSize = 10, // Adjusted size
            Foreground = theme.B_SecondaryTextColor,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 180 // Constrain width
        };
        artistTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Artist"));

        textStack.Children.Add(titleTextBlock);
        textStack.Children.Add(artistTextBlock);

        songInfoPanel.Children.Add(thumbnailImage);
        songInfoPanel.Children.Add(textStack);

        Grid.SetColumn(songInfoPanel, 0);
        mainGrid.Children.Add(songInfoPanel);

        // --- Center Section: Slider, Time, and Buttons ---
        var centerStackPanel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, // Center the whole stack within the column
            Spacing = 5,
            Width = 500, // Give the center panel a fixed width to prevent infinite stretch, adjust as needed
            MaxWidth = 600 // Or a max width
        };

        // Time and Slider Grid
        var timeSliderGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center,
            Height = 24, // Height for slider/time row
            HorizontalAlignment = HorizontalAlignment.Stretch, // Stretch within the centerStackPanel's width
            ClipToBounds = false // Allow elements like thumb to render outside bounds if needed
        };

        var currentTimeTextBlock = new TextBlock
        {
            Foreground = theme.B_SecondaryTextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0),
            MinWidth = 35, // Give it some minimum width
            HorizontalAlignment = HorizontalAlignment.Right // Align text right
        };
        currentTimeTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentTimeDisplay"));
        currentTimeTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong")); // Hide if no song

        var totalTimeTextBlock = new TextBlock
        {
            Foreground = theme.B_SecondaryTextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0),
            MinWidth = 35, // Give it some minimum width
            HorizontalAlignment = HorizontalAlignment.Left // Align text left
        };
        totalTimeTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.TotalTimeDisplay"));
        totalTimeTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong")); // Hide if no song


        var mainPlaybackSlider = new Slider
        {
            Name = "MainPlaybackSliderInstance",
            Minimum = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = theme.B_ControlBackgroundColor, // Use control background for track
            Foreground = theme.B_AccentColor, // Use accent for fill
            HorizontalAlignment = HorizontalAlignment.Stretch, // Stretch within the center column
            Height = 24, // Match grid height
            MinHeight = 24,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(3), // Match track style
            // RenderTransform = new TranslateTransform(0, -3) // Remove or adjust if template handles vertical centering
        };

        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        // Apply custom template or style for a more modern look
        // The provided custom template using FuncControlTemplate is below the MainPlaybackControlsPanel static class definition in the prompt text.
        // Let's replicate that structure or apply styles if the template approach was just an example.
        // Let's use styling for simplicity unless the template is strictly necessary for the desired look.
        // Replicating the styling approach used for other controls:
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor) } });
        mainPlaybackSlider.Styles.Add(new Style(s => s.OfType<Track>()) { Setters = { new Setter(Track.BackgroundProperty, theme.B_ControlBackgroundColor) } }); // Track background
        mainPlaybackSlider.Styles.Add(new Style(s => s.OfType<Track>().Template().Find<Border>("PART_SelectionIndicator")) // Select indicator is often filled part
        { Setters = { new Setter(Border.BackgroundProperty, theme.B_AccentColor) } });


        Grid.SetColumn(currentTimeTextBlock, 0);
        Grid.SetColumn(mainPlaybackSlider, 1);
        Grid.SetColumn(totalTimeTextBlock, 2);

        timeSliderGrid.Children.Add(currentTimeTextBlock);
        timeSliderGrid.Children.Add(mainPlaybackSlider);
        timeSliderGrid.Children.Add(totalTimeTextBlock);

        // Playback Buttons Panel (Copying from user's provided PlaybackNavigationButtonsPanel)
        var combinedPlaybackButtonControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0)
        };

        // Shuffle Button
        var shuffleButton = new ToggleButton
        {
            Foreground = theme.B_SecondaryTextColor,
            Background = Brushes.Transparent, // Use Transparent or a subtle color
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            ContentTemplate = null, // Explicitly null content template if using direct content
            Width = 32,
            Height = 32
        };
        shuffleButton.Content = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 18,
            FontFamily = new FontFamily("Segoe UI Symbol, Arial"),
            [!TextBlock.TextProperty] = new Binding("Playback.ShuffleEnabled") { Converter = BooleanToShuffleIconConverter.Instance }
        };
        shuffleButton.Bind(ToggleButton.IsCheckedProperty, new Binding("Playback.ShuffleEnabled", BindingMode.TwoWay));
        // Dynamic foreground/border based on IsChecked
        shuffleButton[!ToggleButton.ForegroundProperty] = new Binding("IsChecked")
        {
            Source = shuffleButton, // Binding source is the button itself
            Converter = new FuncValueConverter<bool, IBrush>(isChecked => isChecked ? theme.B_AccentColor : theme.B_SecondaryTextColor)
        };
        shuffleButton[!ToggleButton.BorderBrushProperty] = new Binding("IsChecked")
        {
            Source = shuffleButton, // Binding source is the button itself
            Converter = new FuncValueConverter<bool, IBrush>(isChecked => isChecked ? theme.B_AccentColor : theme.B_ControlBackgroundColor)
        };
        shuffleButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));


        // Previous Button
        var previousButton = new Button
        {
            Content = "<",
            Background = theme.B_SlightlyLighterBackground, // Match Main Play/Pause button background
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor, // Match Main Play/Pause button border
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16), // Circle
            Padding = new Thickness(0),
            FontSize = 16,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        previousButton.Bind(Button.CommandProperty, new Binding("Library.PreviousTrackCommand"));


        // Main Play/Pause Button
        var mainPlayPauseButton = new Button
        {
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_AccentColor, // Use accent for border
            BorderThickness = new Thickness(1),
            Width = 38, // Larger
            Height = 38,
            CornerRadius = new CornerRadius(19), // Circle
            Padding = new Thickness(0),
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        mainPlayPauseButton.Bind(Button.ContentProperty, new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseIconConverter.Instance });


        // Next Button
        var nextButton = new Button
        {
            Content = ">",
            Background = theme.B_SlightlyLighterBackground, // Match Prev/Play button background
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor, // Match Prev/Play button border
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16), // Circle
            Padding = new Thickness(0),
            FontSize = 16,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        nextButton.Bind(Button.CommandProperty, new Binding("Library.NextTrackCommand"));


        // Repeat Mode Button
        var repeatModeButton = new ToggleButton
        {
            Foreground = theme.B_SecondaryTextColor,
            Background = Brushes.Transparent, // Use Transparent or subtle color
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            ContentTemplate = null, // Explicitly null content template if using direct content
            Width = 32,
            Height = 32
        };
        repeatModeButton.Content = new TextBlock
        {
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 18, // Adjusted size for clarity
            FontFamily = new FontFamily("Segoe UI Symbol, Arial"),
            RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative),
            [!TextBlock.TextProperty] = new Binding("Playback.RepeatMode")
            {
                Converter = new FuncValueConverter<RepeatMode, string>(mode => mode switch
                {
                    RepeatMode.None => "―", // Hyphen or similar?
                    RepeatMode.PlayOnce => "₁", // Subscript 1
                    RepeatMode.RepeatOne => "🔂", // Repeat One Arrows
                    RepeatMode.RepeatAll => "🔁", // Repeat All Arrows
                    _ => "?" // Should not happen
                })
            },
            // Adjust vertical position slightly for some characters
            [!TextBlock.RenderTransformProperty] = new Binding("Playback.RepeatMode")
            {
                Converter = new FuncValueConverter<RepeatMode, ITransform?>(mode =>
                {
                    return mode switch
                    {
                        RepeatMode.PlayOnce => new TranslateTransform(0, -3), // Nudge 1 subscript up
                        RepeatMode.None => new TranslateTransform(0, -1), // Nudge hyphen up
                        _ => null // No transform for symbols
                    };
                })
            }
        };
        // Dynamic foreground/border based on RepeatMode
        repeatModeButton[!ToggleButton.ForegroundProperty] = new Binding("Playback.RepeatMode")
        {
            Converter = new FuncValueConverter<RepeatMode, IBrush>(mode => mode != RepeatMode.None ? theme.B_AccentColor : theme.B_SecondaryTextColor)
        };
        repeatModeButton[!ToggleButton.BorderBrushProperty] = new Binding("Playback.RepeatMode")
        {
            Converter = new FuncValueConverter<RepeatMode, IBrush>(mode => mode != RepeatMode.None ? theme.B_AccentColor : theme.B_ControlBackgroundColor)
        };
        repeatModeButton.Bind(ToggleButton.IsCheckedProperty, new Binding("Playback.IsRepeatActive")); // Use IsRepeatActive VM property
        repeatModeButton.Bind(Button.CommandProperty, new Binding("Playback.CycleRepeatModeCommand"));
        repeatModeButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));


        combinedPlaybackButtonControlsPanel.Children.Add(shuffleButton);
        combinedPlaybackButtonControlsPanel.Children.Add(previousButton);
        combinedPlaybackButtonControlsPanel.Children.Add(mainPlayPauseButton);
        combinedPlaybackButtonControlsPanel.Children.Add(nextButton);
        combinedPlaybackButtonControlsPanel.Children.Add(repeatModeButton);

        // Add Time/Slider grid and Buttons panel to the center stack
        centerStackPanel.Children.Add(timeSliderGrid);
        centerStackPanel.Children.Add(combinedPlaybackButtonControlsPanel);

        Grid.SetColumn(centerStackPanel, 1);
        mainGrid.Children.Add(centerStackPanel);

        // --- Right Section: Volume (Placeholder) ---
        // You can add volume controls or leave this column empty
        var rightFiller = new Border
        {
            // Example: Add a dummy element or volume control here
            // Background = Brushes.Red.WithOpacity(0.2) // Visual guide
        };
        Grid.SetColumn(rightFiller, 2);
        mainGrid.Children.Add(rightFiller);


        // Apply the custom template to the slider AFTER it has been created and configured
        mainPlaybackSlider.Template = CreateCustomSliderTemplate(theme);


        return mainGrid;
    }

    // --- Slider Fill Width Converter (Required for the custom slider template below) ---
    public class SliderFillWidthConverter : IMultiValueConverter
    {
        public object Convert(IList<object> values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Count == 3 &&
                values[0] is double value &&
                values[1] is double max &&
                values[2] is Rect bounds &&
                max > 0 && bounds.Width > 0) // Add check for non-zero width
            {
                // Clamp the value to prevent overdraw if max is zero or bounds are weird
                double clampedValue = Math.Clamp(value, 0, max);
                return bounds.Width * (clampedValue / max);
            }
            Debug.WriteLine($"[SliderConverter] Invalid values: count={values.Count}, value={values.Count > 0 ? values[0] : "N / A"}, max={values.Count > 1 ? values[1] : "N / A"}, bounds={values.Count > 2 ? values[2] : "N / A"}");
            return 0.0;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }

    // --- Custom Slider Template Definition (Based on user's example in PlaybackTimeSliderPanel.cs) ---
    private static IControlTemplate<Slider> CreateCustomSliderTemplate(ThemeColors theme)
    {
        return new FuncControlTemplate<Slider>((slider, nameScope) =>
        {
            // Ensure the background border matches the track height
            var backgroundPart = new Border
            {
                Background = theme.B_ControlBackgroundColor, // Slider Track Background
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center,
                Height = 6, // Match desired track height
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var filledPart = new Border
            {
                Background = theme.B_AccentColor, // Slider Fill Color
                CornerRadius = new CornerRadius(3),
                VerticalAlignment = VerticalAlignment.Center,
                Height = 6, // Match desired track height
                HorizontalAlignment = HorizontalAlignment.Left
            };

            // Thumb appearance
            var thumbBackground = new Ellipse // Use Ellipse for circular thumb
            {
                Width = 14,
                Height = 14,
                Fill = theme.B_AccentColor, // Thumb color
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
            };

            var thumb = new Thumb
            {
                Name = "PART_Thumb", // Important name for Track to find it
                Width = 16, // slightly larger hit target
                Height = 16,
                Background = Brushes.Transparent, // Thumb control itself is transparent
                IsHitTestVisible = true,
                Cursor = new Cursor(StandardCursorType.Hand),
                Focusable = true, // Make thumb focusable for keyboard input
                Template = new FuncControlTemplate<Thumb>((t, _) => new Border { Child = thumbBackground }) // Wrap the visual in a Border or directly use the visual
            };

            var track = new Track
            {
                Name = "PART_Track", // Important name for Slider to find it
                Orientation = Orientation.Horizontal,
                Thumb = thumb,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = true // Track itself needs to be hit test visible for pointer events
            };

            // Bind Track properties to Slider properties
            track.Bind(Track.MinimumProperty, slider[!Slider.MinimumProperty]);
            track.Bind(Track.MaximumProperty, slider[!Slider.MaximumProperty]);
            track.Bind(Track.ValueProperty, slider[!Slider.ValueProperty]);

            // Manual click-to-seek logic on the background/track area
            var seekAreaPanel = new Panel // Use a panel to layer track, background, fill
            {
                VerticalAlignment = VerticalAlignment.Center,
                Height = 24, // Allow a taller hit target area for clicks
                Children = { backgroundPart, filledPart, track } // Order matters: background first, then fill, then track (with thumb) on top
            };

            seekAreaPanel.PointerPressed += (sender, e) =>
            {
                // Handle clicks outside the thumb to seek
                if (e.GetCurrentPoint(seekAreaPanel).Properties.IsLeftButtonPressed)
                {
                    // Get the position relative to the Track's visual area (backgroundPart or seekAreaPanel)
                    var position = e.GetPosition(backgroundPart); // Use backgroundPart for width reference
                    var width = backgroundPart.Bounds.Width;
                    if (width <= 0) return;

                    double ratio = position.X / width;
                    ratio = Math.Clamp(ratio, 0, 1);

                    // Calculate the new value based on the ratio
                    double newValue = slider.Minimum + ratio * (slider.Maximum - slider.Minimum);

                    // Update the slider's Value. The two-way binding will propagate this to the ViewModel.
                    // Setting Value directly here bypasses the ViewModel's setter if the binding mode is OneWayToSource.
                    // If the binding is TwoWay, setting it here is fine. The ViewModel's setter logic should handle the Seek call.
                    slider.Value = newValue;

                    e.Handled = true; // Mark event as handled
                }
            };

            // Bind fill width to value/max * total width using the custom converter
            filledPart.Bind(Border.WidthProperty, new MultiBinding
            {
                Converter = new SliderFillWidthConverter(), // Use the custom converter
                Bindings =
                 {
                     slider[!Slider.ValueProperty],
                     slider[!Slider.MaximumProperty],
                     backgroundPart[!Visual.BoundsProperty] // Bind to the bounds of the background part for width
                 }
            });

            // Bind Thumb's position to the Value using a converter if needed,
            // but the Track control usually handles positioning the Thumb automatically based on its Value.
            // If the Thumb doesn't appear or position correctly, the Track template might need customization.
            // For simplicity, rely on the standard Track behavior for Thumb positioning first.

            return seekAreaPanel; // Return the panel containing all parts
        });
    }
}