using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia;

using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.Views.MainWindowControls;
using Avalonia.Media.Imaging; // Required for BitmapInterpolationMode

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
        previousButton.Bind(Button.CommandProperty, new Binding("Playback.PreviousTrackCommand")); // Assuming this command exists or will exist
        previousButton.Bind(Button.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var mainPlayPauseButton = new Button
        {
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_AccentColor,
            BorderThickness = new Thickness(1),
            Width = 38,
            Height = 38,
            CornerRadius = new CornerRadius(19),
            Padding = new Thickness(0),
            FontSize = 18,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        mainPlayPauseButton.Bind(Button.ContentProperty, new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseIconConverter.Instance });

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
        nextButton.Bind(Button.CommandProperty, new Binding("Playback.NextTrackCommand")); // Assuming this command exists or will exist
        nextButton.Bind(Button.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var playbackButtonControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center, // Centered within its parent StackPanel
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0) // No margin needed here
        };
        playbackButtonControlsPanel.Children.Add(previousButton);
        playbackButtonControlsPanel.Children.Add(mainPlayPauseButton);
        playbackButtonControlsPanel.Children.Add(nextButton);

        var toggleAdvPanelButton = new Button
        {
            Content = "+",
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_AccentColor,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(8, 4),
            MinWidth = 30, // Give it a minimum size to occupy space
            FontWeight = FontWeight.Bold
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


        var centerPlaybackControlsStack = new StackPanel // Contains buttons and slider grid
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 5, 0, 0),
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center, // Center this stack panel within the root Grid
            VerticalAlignment = VerticalAlignment.Center // Center vertically within its grid row
        };
        centerPlaybackControlsStack.Children.Add(playbackButtonControlsPanel);
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


        // --- Main Grid Layout ---
        // Use a single column Grid. The center element is centered within it.
        // Left and Right elements are aligned to the sides *within* that same single column.
        // This prevents the size of the left/right elements from affecting the center's horizontal position.
        var outerGrid = new Grid // This is the root panel
        {
            Background = theme.B_BackgroundColor,
            Margin = new Thickness(0, 5, 0, 5), // Vertical margin for the whole control
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = new RowDefinitions("Auto"), // Single row, height is Auto based on content
            ColumnDefinitions = new ColumnDefinitions("*") // Single column spanning the width
        };

        // Place all three main sections in the *same* grid cell (row 0, column 0).
        // Their HorizontalAlignment properties will dictate their position within that cell.
        Grid.SetRow(songInfoPanel, 0);
        Grid.SetColumn(songInfoPanel, 0);

        Grid.SetRow(centerPlaybackControlsStack, 0);
        Grid.SetColumn(centerPlaybackControlsStack, 0);

        Grid.SetRow(rightControlsPanel, 0);
        Grid.SetColumn(rightControlsPanel, 0);

        outerGrid.Children.Add(songInfoPanel);
        outerGrid.Children.Add(centerPlaybackControlsStack);
        outerGrid.Children.Add(rightControlsPanel);

        return outerGrid;
    }
}