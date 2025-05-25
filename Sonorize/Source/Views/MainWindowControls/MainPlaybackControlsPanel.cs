using System.Diagnostics; // Added for Debug
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters; // Added required using directive for FuncValueConverter
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging; // Required for BitmapInterpolationMode
using Avalonia.Styling;
using Sonorize.Converters;
using Sonorize.Models;
using Sonorize.ViewModels; // Required for RepeatMode enum

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
            // MinWidth/Width could be added here if needed to reserve space even when invisible
        };
        rightControlsPanel.Children.Add(toggleAdvPanelButton);

        // --- Playback Time Slider Panel (Extracted) ---
        var timeSliderGrid = PlaybackTimeSliderPanel.Create(theme);


        // --- Center Playback Controls Stack (Combined Buttons Panel + Slider) ---
        // This stack panel contains the combined button panel (now includes shuffle/loop, prev/play/next) and the time/slider grid.
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


        // --- Currently Playing Song Info Panel (Extracted) ---
        var songInfoPanel = SongInfoDisplayPanel.Create(theme);


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