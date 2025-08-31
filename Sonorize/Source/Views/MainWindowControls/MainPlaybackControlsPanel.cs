using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters; // Added required using directive for FuncValueConverter
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models;

namespace Sonorize.Views.MainWindowControls;

public static class MainPlaybackControlsPanel
{
    public static Grid Create(ThemeColors theme) // Root is a Grid
    {
        // --- Playback Navigation Buttons Panel (Extracted) ---
        var combinedPlaybackButtonControlsPanel = PlaybackNavigationButtonsPanel.Create(theme);

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
            Height = 32, // Fixed height for consistency
            HorizontalContentAlignment = HorizontalAlignment.Center, // Center content horizontally
            VerticalContentAlignment = VerticalAlignment.Center     // Center content vertically
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
        };
        rightControlsPanel.Children.Add(toggleAdvPanelButton);

        // --- Playback Time Slider Panel (Extracted) ---
        var timeSliderGrid = PlaybackTimeSliderPanel.Create(theme);


        // --- Center Playback Controls Stack (Combined Buttons Panel + Slider) ---
        var centerPlaybackControlsStack = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 5, 0, 0),
            Spacing = 8, // Space between the button row and the slider row
            HorizontalAlignment = HorizontalAlignment.Center, // Center this stack panel within its parent grid cell
            VerticalAlignment = VerticalAlignment.Center
        };
        centerPlaybackControlsStack.Children.Add(combinedPlaybackButtonControlsPanel);
        centerPlaybackControlsStack.Children.Add(timeSliderGrid);


        // --- Currently Playing Song Info Panel (Extracted) ---
        var songInfoPanel = SongInfoDisplayPanel.Create(theme);


        // --- Grid to hold the content and determine the component's size ---
        var contentGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = new RowDefinitions("Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,*"),
            Background = Brushes.Transparent, // This grid is for layout, not appearance.
            VerticalAlignment = VerticalAlignment.Center
        };

        // Place all visible controls into the contentGrid. Their alignment properties will position them correctly.
        Grid.SetColumn(songInfoPanel, 0);
        Grid.SetColumn(centerPlaybackControlsStack, 1);
        Grid.SetColumn(rightControlsPanel, 2);
        contentGrid.Children.Add(songInfoPanel);
        contentGrid.Children.Add(centerPlaybackControlsStack);
        contentGrid.Children.Add(rightControlsPanel);


        // --- Root Grid for background and layering ---
        var outerGrid = new Grid
        {
            Margin = new Thickness(0, 5, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true
        };
        outerGrid.Bind(Grid.BackgroundProperty, new Binding("PlaybackAreaBackground"));

        var backgroundImage = new Image
        {
            Stretch = Stretch.Fill,
            Opacity = 0.2
        };
        // Bind MaxHeight to the content grid's height to prevent the image from expanding its parent.
        // Use a direct source binding instead of ElementName to avoid NameScope issues in code-behind.
        backgroundImage.Bind(Image.MaxHeightProperty, new Binding("Bounds.Height") { Source = contentGrid });
        backgroundImage.Bind(Image.SourceProperty, new Binding("AlbumArtForBackground"));
        backgroundImage.Bind(Visual.IsVisibleProperty, new Binding("ShowAlbumArtBackground"));

        var backgroundOverlay = new Border
        {
            Background = new SolidColorBrush(Colors.Black, 0.6) // Semi-transparent black overlay to darken the image
        };
        backgroundOverlay.Bind(Visual.IsVisibleProperty, new Binding("ShowAlbumArtBackground"));

        // Add layers to the outer grid. Backgrounds first, then the contentGrid on top.
        outerGrid.Children.Add(backgroundImage);
        outerGrid.Children.Add(backgroundOverlay);
        outerGrid.Children.Add(contentGrid);

        return outerGrid;
    }
}