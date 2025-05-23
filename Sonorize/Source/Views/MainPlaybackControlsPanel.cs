using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Media.Imaging; // Required for Image
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.Views.MainWindowControls;
using Avalonia.Data.Converters; // Required for IMultiValueConverter
using System; // Required for Math

namespace Sonorize.Views.MainWindowControls;

public static class MainPlaybackControlsPanel
{
    public static Panel Create(ThemeColors theme)
    {
        // Main container - Use DockPanel to place song info on the left
        var outerPanel = new DockPanel
        {
            Background = theme.B_BackgroundColor,
            Margin = new Thickness(0, 5, 0, 5),
            LastChildFill = true, // The central content will fill the remaining space
            MinHeight = 60 // Ensure panel has enough height
        };

        // --- Song Info Panel (Left) ---
        // This panel remains visible, but its content will be bound to Playback.HasCurrentSong or CurrentSong
        var songInfoPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 15, 0), // Add some space to the right
            Spacing = 10, // Space between image and text
            MinWidth = 200, // Give it a minimum width
            MaxWidth = 350 // Prevent it from taking up too much space on wide screens
        };
        DockPanel.SetDock(songInfoPanel, Dock.Left);

        // Toggle Advanced Panel Button - Placed here on the left side
        var toggleAdvPanelButton = new Button { Content = "+", Background = theme.B_SlightlyLighterBackground, Foreground = theme.B_TextColor, BorderBrush = theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), MinWidth = 30, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
        // This button should only be enabled if a song is loaded
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        toggleAdvPanelButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));
        songInfoPanel.Children.Add(toggleAdvPanelButton);


        // Song Thumbnail
        var songThumbnail = new Image
        {
            Width = 40,
            Height = 40,
            Stretch = Stretch.UniformToFill,
            Source = null // Will be bound - if CurrentSong is null, Source binding will likely resolve to null, which is okay
        };
        RenderOptions.SetBitmapInterpolationMode(songThumbnail, BitmapInterpolationMode.HighQuality);
        songThumbnail.Bind(Image.SourceProperty, new Binding("Playback.CurrentSong.Thumbnail"));
        songInfoPanel.Children.Add(songThumbnail);

        // Song Title and Artist Text
        var songTextStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 250 // Prevent text from overflowing significantly
        };
        var songTitleBlock = new TextBlock
        {
            Foreground = theme.B_TextColor,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis
            // Text will be bound - if CurrentSong is null, Title binding will likely resolve to null/empty string
        };
        songTitleBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Title"));

        var songArtistBlock = new TextBlock
        {
            Foreground = theme.B_SecondaryTextColor,
            FontSize = 11,
            TextTrimming = TextTrimming.CharacterEllipsis
            // Text will be bound - if CurrentSong is null, Artist binding will likely resolve to null/empty string
        };
        songArtistBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentSong.Artist"));

        songTextStack.Children.Add(songTitleBlock);
        songTextStack.Children.Add(songArtistBlock);

        songInfoPanel.Children.Add(songTextStack);


        // --- Playback Controls Grid (Center/Right) ---
        // This grid contains the buttons row and the slider/time row.
        // Its columns define the layout for *both* rows.
        var playbackControlsGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch, // Let it fill the remaining space
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0), // Space on the right edge
            RowDefinitions = new RowDefinitions("Auto,Auto"), // Row 0 for buttons, Row 1 for slider/time
            // Define columns that will center the 600px max width slider
            ColumnDefinitions = new ColumnDefinitions("*,600,Auto,*") // Left padding (*), Slider max width (600), Time (Auto), Right padding (*)
        };


        // Group for playback control buttons (Prev, Play/Pause, Next)
        var playbackButtonControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center, // Center buttons within their spanned grid cell
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetRow(playbackButtonControlsPanel, 0);
        Grid.SetColumn(playbackButtonControlsPanel, 0); // Start at column 0
        Grid.SetColumnSpan(playbackButtonControlsPanel, 4); // Span all columns

        // Previous Button
        var previousButton = new Button
        {
            Content = "<", // Previous Track Symbol
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor, // Slightly less prominent border
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(0),
            FontSize = 16,
            FontWeight = FontWeight.Normal,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        // Bind to the actual command if implemented in PlaybackViewModel later
        // For now, bind to a non-existent command or disable if needed.
        // Leaving command binding commented out until implemented.
        // previousButton.Bind(Button.CommandProperty, new Binding("Playback.PreviousTrackCommand"));
        previousButton.Bind(Button.IsEnabledProperty, new Binding("Playback.HasCurrentSong")); // Enabled if a song is loaded

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
            FontWeight = FontWeight.Normal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        // Cannot play/pause if no song or waveform is loading
        mainPlayPauseButton.Bind(Control.IsEnabledProperty, new MultiBinding
        {
            Bindings = {
                new Binding("Playback.HasCurrentSong"),
                new Binding("Playback.IsWaveformLoading") { Converter = new InverseBooleanConverter() }
            },
            Converter = new AndBooleanConverter()
        });
        var playPauseIconBinding = new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseIconConverter.Instance };
        mainPlayPauseButton.Bind(Button.ContentProperty, playPauseIconBinding);

        // Next Button
        var nextButton = new Button
        {
            Content = ">", // Next Track Symbol
            Background = theme.B_SlightlyLighterBackground,
            Foreground = theme.B_TextColor,
            BorderBrush = theme.B_ControlBackgroundColor, // Slightly less prominent border
            BorderThickness = new Thickness(1),
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(0),
            FontSize = 16,
            FontWeight = FontWeight.Normal,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        // Bind to the actual command if implemented in PlaybackViewModel later
        // For now, bind to a non-existent command or disable if needed.
        // Leaving command binding commented out until implemented.
        // nextButton.Bind(Button.CommandProperty, new Binding("Playback.NextTrackCommand"));
        nextButton.Bind(Button.IsEnabledProperty, new Binding("Playback.HasCurrentSong")); // Enabled if a song is loaded

        // Add all three buttons back
        playbackButtonControlsPanel.Children.Add(previousButton);
        playbackButtonControlsPanel.Children.Add(mainPlayPauseButton);
        playbackButtonControlsPanel.Children.Add(nextButton);
        playbackControlsGrid.Children.Add(playbackButtonControlsPanel);


        // Slider and Time Display Grid
        // This grid aligns the time text block to the right of the slider
        var sliderAndTimeGridPanel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"), // Slider fills (*), Time takes needed space (Auto)
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 25,
            Margin = new Thickness(0, -5, 0, 0) // Add negative top margin to bring it higher
        };
        Grid.SetRow(sliderAndTimeGridPanel, 1);
        Grid.SetColumn(sliderAndTimeGridPanel, 0); // Start at column 0
        Grid.SetColumnSpan(sliderAndTimeGridPanel, 4); // Span all columns, aligns slider and time text

        var mainPlaybackSliderCentered = new Slider
        {
            Minimum = 0,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch, // Let it stretch within its grid cell
            Background = theme.B_SecondaryTextColor,
            Foreground = theme.B_AccentColor,
            Classes = { "custom-thumb-style" } // Apply custom style for thumb
        };
        // Apply custom thumb style to make it visible and themed
        mainPlaybackSliderCentered.Styles.Add(new Style(s => s.Is<Thumb>().Class("custom-thumb-style"))
        {
            Setters =
            {
                new Setter(Thumb.WidthProperty, 10.0),
                new Setter(Thumb.HeightProperty, 10.0),
                new Setter(Thumb.BackgroundProperty, theme.B_AccentColor),
                new Setter(Thumb.BorderBrushProperty, theme.B_AccentColor),
                new Setter(Thumb.BorderThicknessProperty, new Thickness(1)),
                new Setter(Thumb.CornerRadiusProperty, new CornerRadius(5)),
                new Setter(Thumb.OpacityProperty, 1.0) // Ensure visible
            }
        });
        mainPlaybackSliderCentered.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSliderCentered.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay)); // TwoWay binding for seeking
        mainPlaybackSliderCentered.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong")); // Cannot seek if no song

        var timeDisplayTextBlockGrid = new TextBlock
        {
            Foreground = theme.B_TextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
            MinWidth = 75,
            TextAlignment = TextAlignment.Right // Align time to the right
            // Text will be bound - will show "--:-- / --:--" if no song loaded
        };
        timeDisplayTextBlockGrid.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentTimeTotalTimeDisplay"));

        // Add elements to the sliderAndTimeGridPanel
        Grid.SetColumn(mainPlaybackSliderCentered, 0); // Slider is in the * column
        Grid.SetColumn(timeDisplayTextBlockGrid, 1); // Time is in the Auto column
        sliderAndTimeGridPanel.Children.Add(mainPlaybackSliderCentered);
        sliderAndTimeGridPanel.Children.Add(timeDisplayTextBlockGrid);

        // Add the slider/time grid panel to the main playbackControlsGrid
        playbackControlsGrid.Children.Add(sliderAndTimeGridPanel);

        // Add the panels to the main DockPanel
        outerPanel.Children.Add(songInfoPanel); // This panel is always visible
        outerPanel.Children.Add(playbackControlsGrid); // This will fill the remaining space (DockPanel's LastChild)

        return outerPanel;
    }
}