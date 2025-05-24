using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia;
using Avalonia.Data.Converters; // Added required using directive for FuncValueConverter
using Avalonia.Controls.Templates; // Required for FuncDataTemplate
using Avalonia.Media.Imaging; // Required for BitmapInterpolationMode
using Sonorize.Models;
using Sonorize.ViewModels; // Required for RepeatMode enum
using System; // Required for Func
using System.Diagnostics; // Added for Debug
using Sonorize.Converters; // Added using for the new converter

namespace Sonorize.Views.MainWindowControls;

public static class MainPlaybackControlsPanel
{
    public static Grid Create(ThemeColors theme) // Root is a Grid
    {
        // Previous Button
        var previousButton = new Button
        {
            Content = "<",
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(0),
            FontSize = 16,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        // Bind to Library.PreviousTrackCommand
        previousButton.Bind(Button.CommandProperty, new Binding("Library.PreviousTrackCommand"));
        // IsEnabled is controlled by the command's CanExecute

        var mainPlayPauseButton = new Button
        {
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor, // Button foreground controls icon color
            BorderBrush = theme.B_AccentColor,
            BorderThickness = new Thickness(1),
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(19),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Content = new PathIcon // Use PathIcon for SVG
            {
                Width = 24, // Set size for the icon
                Height = 24,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                // Data will be bound by the converter
            }
        };

        // Bind the PathIcon's Data property using the new converter
        var pathIcon = (PathIcon)mainPlayPauseButton.Content!;
        pathIcon.Bind(PathIcon.DataProperty, new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseGeometryConverter.Instance });

        // Old binding using text converter removed:
        // mainPlayPauseButton.Bind(Button.ContentProperty, new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseIconConverter.Instance });

        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));


        var nextButton = new Button
        {
            Content = ">",
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor,
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(0),
            FontSize = 16,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        // Bind to Library.NextTrackCommand
        nextButton.Bind(Button.CommandProperty, new Binding("Library.NextTrackCommand"));
        // IsEnabled is controlled by the command's CanExecute


        // --- Shuffle and Loop/Repeat Buttons ---

        var shuffleButton = new ToggleButton
        {
            // Content is bound via converter directly
            Foreground = theme.B_SecondaryTextColor, // Default color (off) - This will be overridden by the style
            Background = Brushes.Transparent,
            BorderBrush = theme.B_ControlBackgroundColor, // Default border color (off) - This will be overridden by the style
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), // Add some rounded corners
            Padding = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, // Center the button horizontally
            VerticalContentAlignment = VerticalAlignment.Center, // Center content vertically
            HorizontalContentAlignment = HorizontalAlignment.Center, // Center content horizontally
            FontSize = 18, // Set font size directly on button
            FontFamily = "Segoe UI Symbol, Arial", // Set font family directly on button
            ContentTemplate = null, // No explicit template needed for simple string content
            Width = 32, // Fixed width for icon
            Height = 32 // Fixed height for icon
        };
        // Bind IsChecked to Playback.ShuffleEnabled (TwoWay) - This is essential for the toggle state
        // This binding, when checked/unchecked by user click, will trigger the ShuffleEnabled setter in the VM.
        shuffleButton.Bind(ToggleButton.IsCheckedProperty, new Binding("Playback.ShuffleEnabled", BindingMode.TwoWay));
        // Bind Content directly using the converter based on the *ViewModel's* ShuffleEnabled state
        shuffleButton.Bind(ContentControl.ContentProperty, new Binding("Playback.ShuffleEnabled") { Converter = BooleanToShuffleIconConverter.Instance });

        // REMOVED: Explicit Button.Command binding. The TwoWay IsChecked binding handles the toggle.
        // shuffleButton.Bind(Button.CommandProperty, new Binding("Playback.ToggleShuffleCommand"));

        Debug.WriteLine($"[View] Shuffle Button Content Bound Directly to Playback.ShuffleEnabled with BooleanToShuffleIconConverter. Command binding removed.");


        // Change Foreground color based on IsChecked state (using the FuncValueConverter)
        shuffleButton[!ToggleButton.ForegroundProperty] = new Binding("IsChecked")
        {
            Source = shuffleButton,
            Converter = new FuncValueConverter<bool, IBrush>(isChecked => isChecked ? theme.B_AccentColor : theme.B_SecondaryTextColor)
        };
        // Change BorderBrush color based on IsChecked state for a stronger visual cue (using the FuncValueConverter)
        shuffleButton[!ToggleButton.BorderBrushProperty] = new Binding("IsChecked")
        {
            Source = shuffleButton,
            Converter = new FuncValueConverter<bool, IBrush>(isChecked => isChecked ? theme.B_AccentColor : theme.B_ControlBackgroundColor)
        };
        // Ensure button is enabled only when a song is loaded
        shuffleButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));


        var repeatModeButton = new ToggleButton // Renamed from loopButton
        {
            // Content is bound via converter directly
            Foreground = theme.B_SecondaryTextColor, // Default color (off) - Will be overridden by style
            Background = Brushes.Transparent,
            BorderBrush = theme.B_ControlBackgroundColor, // Default border color (off) - Will be overridden by style
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4), // Add some rounded corners
            Padding = new Thickness(5),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center, // Center the button horizontally
            VerticalContentAlignment = VerticalAlignment.Center, // Center content vertically
            HorizontalContentAlignment = HorizontalAlignment.Center, // Center content horizontally
            FontSize = 18, // Use larger font size for icons
            FontFamily = "Segoe UI Symbol, Arial", // Explicitly set font family for symbols
            ContentTemplate = null, // No explicit template needed for simple string content
            Width = 32, // Fixed width for icon
            Height = 32 // Fixed height for icon
        };
        // Bind Content to Playback.RepeatMode (using a converter to show state) - Renamed
        repeatModeButton.Bind(ToggleButton.ContentProperty, new Binding("Playback.RepeatMode")
        {
            Converter = new FuncValueConverter<RepeatMode, string>(mode => mode switch
            {
                RepeatMode.None => "―", // Horizontal Bar: Do Nothing / Stop
                RepeatMode.PlayOnce => "₁", // Subscript 1: Play list once
                RepeatMode.RepeatOne => "🔂", // Repeat One Button: Repeat current song
                RepeatMode.RepeatAll => "🔁", // Repeat Button: Repeat all songs
                _ => "?" // Fallback icon
            })
        });
        // Change foreground color based on RepeatMode state (if not None - i.e. any repeat/cycle is active)
        repeatModeButton[!ToggleButton.ForegroundProperty] = new Binding("Playback.RepeatMode")
        {
            // Accent color for PlayOnce, RepeatOne, RepeatAll. Secondary for None.
            Converter = new FuncValueConverter<RepeatMode, IBrush>(mode => mode != RepeatMode.None ? theme.B_AccentColor : theme.B_SecondaryTextColor)
        };
        // Change BorderBrush color based on RepeatMode state (if not None - i.e. any repeat/cycle is active)
        repeatModeButton[!ToggleButton.BorderBrushProperty] = new Binding("Playback.RepeatMode")
        {
            // Accent color for PlayOnce, RepeatOne, RepeatAll. ControlBackground for None.
            Converter = new FuncValueConverter<RepeatMode, IBrush>(mode => mode != RepeatMode.None ? theme.B_AccentColor : theme.B_ControlBackgroundColor)
        };
        // Bind IsChecked to Playback.IsRepeatActive (ViewModel calculates this based on RepeatMode != None)
        repeatModeButton.Bind(ToggleButton.IsCheckedProperty, new Binding("Playback.IsRepeatActive"));
        // Bind Command to Playback.CycleRepeatModeCommand
        repeatModeButton.Bind(Button.CommandProperty, new Binding("Playback.CycleRepeatModeCommand"));
        // Ensure button is enabled only when a song is loaded
        repeatModeButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));


        // --- Combined Playback Controls Panel (Shuffle + Nav Buttons + Loop) ---
        // This stack panel holds the core playback buttons including the mode toggles
        var combinedPlaybackButtonControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10, // Space between buttons
            HorizontalAlignment = HorizontalAlignment.Center, // Center buttons within this stack panel
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0)
        };

        // Add the buttons in the desired order (Shuffle - Previous - Play/Pause - Next - Repeat Mode)
        combinedPlaybackButtonControlsPanel.Children.Add(shuffleButton);
        combinedPlaybackButtonControlsPanel.Children.Add(previousButton);
        combinedPlaybackButtonControlsPanel.Children.Add(mainPlayPauseButton);
        combinedPlaybackButtonControlsPanel.Children.Add(nextButton);
        combinedPlaybackButtonControlsPanel.Children.Add(repeatModeButton); // Added the renamed repeat button


        var toggleAdvPanelButton = new Button
        {
            Content = "+",
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor, // Default color
            BorderBrush = theme.B_ControlBackgroundColor, // Default border color
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 4),
            MinWidth = 30, // Give it a minimum size to occupy space
            FontWeight = FontWeight.Bold,
            Width = 32, // Fixed width for consistency
            Height = 32 // Fixed height for consistency
        };
        // Change BorderBrush color based on IsAdvancedPanelVisible
        toggleAdvPanelButton[!Button.BorderBrushProperty] = new Binding("IsAdvancedPanelVisible")
        {
            Converter = new FuncValueConverter<bool, IBrush>(isVisible => isVisible ? theme.B_AccentColor : theme.B_ControlBackgroundColor)
        };
        toggleAdvPanelButton[!Button.ForegroundProperty] = new Binding("IsAdvancedPanelVisible")
        {
            Converter = new FuncValueConverter<bool, IBrush>(isVisible => isVisible ? theme.B_AccentColor : theme.B_TextColor)
        };
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        toggleAdvPanelButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));


        var rightControlsPanel = new StackPanel // Holds toggle button
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, // Align to the right within its grid cell
            Margin = new Thickness(0, 0, 10, 0) // Margin from the right edge of the grid cell
            // MinWidth/Width could be added here if needed to reserve space even when invisible
        };
        rightControlsPanel.Children.Add(toggleAdvPanelButton);

        // TextBlock for Current Time
        var currentTimeTextBlock = new TextBlock
        {
            Foreground = theme.B_SecondaryTextColor, // Use secondary color
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0), // Margin to the right of the text
            MinWidth = 40, // Ensure enough space for MM:SS
            HorizontalAlignment = HorizontalAlignment.Left // Explicitly left align within its grid cell
        };
        currentTimeTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentTimeDisplay"));
        currentTimeTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        // TextBlock for Total Time
        var totalTimeTextBlock = new TextBlock
        {
            Foreground = theme.B_SecondaryTextColor, // Use secondary color
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 0, 0, 0), // Margin to the left of the text
            MinWidth = 40, // Ensure enough space for MM:SS
            HorizontalAlignment = HorizontalAlignment.Right // Explicitly right align within its grid cell
        };
        totalTimeTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.TotalTimeDisplay"));
        totalTimeTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));


        var mainPlaybackSlider = new Slider
        {
            Name = "MainPlaybackSliderInstance",
            Minimum = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = theme.B_SecondaryTextColor,
            Foreground = theme.B_AccentColor,
            HorizontalAlignment = HorizontalAlignment.Stretch // Allow slider to fill the available space in its column
        };
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>())
        {
            Setters =
            {
                new Setter(Thumb.WidthProperty, 0.0),
                new Setter(Thumb.HeightProperty, 0.0),
                new Setter(Thumb.OpacityProperty, 0.0) // Hide the thumb visually
            }
        });
        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));


        // Use a Grid to place time text blocks next to the slider
        var timeSliderGrid = new Grid
        {
            // Three columns: Auto (CurrentTime), * (Slider), Auto (TotalTime)
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center,
            Height = 30, // Fixed height
            MinWidth = 500, // Ensure minimum width for layout stability
            HorizontalAlignment = HorizontalAlignment.Stretch // Stretch to fill the center container
        };

        // Place controls in the grid columns
        Grid.SetColumn(currentTimeTextBlock, 0);
        Grid.SetColumn(mainPlaybackSlider, 1);
        Grid.SetColumn(totalTimeTextBlock, 2);

        timeSliderGrid.Children.Add(currentTimeTextBlock);
        timeSliderGrid.Children.Add(mainPlaybackSlider);
        timeSliderGrid.Children.Add(totalTimeTextBlock);


        // --- Center Playback Controls Stack (Combined Buttons Panel + Slider) ---
        // This stack panel contains the combined button panel (including mode toggles) and the time/slider grid.
        var centerPlaybackControlsStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 5, 0, 0),
            Spacing = 8, // Space between the button row and the slider row
            HorizontalAlignment = HorizontalAlignment.Center, // Center this stack panel within its parent grid cell
            VerticalAlignment = VerticalAlignment.Center
        };
        // Add the combined button panel (now includes shuffle/loop, prev/play/next)
        centerPlaybackControlsStack.Children.Add(combinedPlaybackButtonControlsPanel);
        centerPlaybackControlsStack.Children.Add(timeSliderGrid);


        // --- Currently Playing Song Info Panel (Bottom Left) ---
        var songInfoPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center, // Center vertically in its grid cell
            HorizontalAlignment = HorizontalAlignment.Left, // Align to the left edge of its grid cell
            Margin = new Thickness(10, 0, 0, 0), // Margin from the left edge of the grid cell
            Spacing = 8,
            // Add MaxWidth to prevent text pushing content, trimming is handled by TextBlock MaxWidth/TextTrimming
            // We rely on the root Grid column definition to prevent pushing the center.
        };
        songInfoPanel.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong")); // Only visible when a song is loaded

        // MODIFIED: Increased thumbnail size from 48x48 to 64x64
        var thumbnailImage = new Image
        {
            Width = 64, // Increased size
            Height = 64, // Increased size
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
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = theme.B_TextColor,
            TextTrimming = TextTrimming.CharacterEllipsis, // Crucial for preventing overflow
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 200 // Limit width of the text itself
        };
        titleTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Title"));

        var artistTextBlock = new TextBlock
        {
            Text = "Unknown Artist", // Default, will be bound
            FontSize = 11,
            Foreground = theme.B_SecondaryTextColor,
            TextTrimming = TextTrimming.CharacterEllipsis, // Crucial for preventing overflow
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 200 // Limit width of the text itself
        };
        artistTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Artist"));

        textStack.Children.Add(titleTextBlock);
        textStack.Children.Add(artistTextBlock);

        songInfoPanel.Children.Add(thumbnailImage);
        songInfoPanel.Children.Add(textStack);


        // --- Main Grid Layout (Restored Single Column Centering) ---
        // Use a single star (*) column. All children are placed in this column.
        // Their HorizontalAlignment determines their position within the column.
        // The centerPlaybackControlsStack has HorizontalAlignment.Center, ensuring it's centered
        // regardless of the width of the left (songInfoPanel) or right (rightControlsPanel) elements.
        var outerGrid = new Grid // This is the root panel
        {
            Background = theme.B_BackgroundColor,
            Margin = new Thickness(0, 5, 0, 5), // Vertical margin for the whole control
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = new RowDefinitions("Auto"), // Single row, height is Auto based on content
            ColumnDefinitions = new ColumnDefinitions("*") // Single column spanning the width
        };

        // Place all panels in the single column (column 0).
        // Their HorizontalAlignment will handle horizontal positioning.
        Grid.SetColumn(songInfoPanel, 0);
        Grid.SetColumn(centerPlaybackControlsStack, 0);
        Grid.SetColumn(rightControlsPanel, 0);

        // Add children in any order; their position is determined by grid layout and alignment.
        outerGrid.Children.Add(songInfoPanel);
        outerGrid.Children.Add(centerPlaybackControlsStack);
        outerGrid.Children.Add(rightControlsPanel);

        return outerGrid;
    }
}