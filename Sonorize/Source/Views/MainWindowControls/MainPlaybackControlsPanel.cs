using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.MainWindowControls;

public static class MainPlaybackControlsPanel
{
    public static Border Create(ThemeColors theme)
    {
        var mainControlsGrid = new Grid
        {
            // Background is now handled by the wrapping Border
            ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), // SongInfo (stretches), Buttons (auto), TimeSlider (stretches)
            HorizontalAlignment = HorizontalAlignment.Stretch // Grid should stretch within the Border's content area
            // MinHeight is now handled by the wrapping Border
        };

        // Song Info Panel (Left Aligned in its column)
        var songInfoPanel = SongInfoDisplayPanel.Create(theme);
        Grid.SetColumn(songInfoPanel, 0);
        mainControlsGrid.Children.Add(songInfoPanel);

        // Playback Buttons (Center Aligned in its column)
        var playbackButtonsPanel = PlaybackNavigationButtonsPanel.Create(theme);
        playbackButtonsPanel.HorizontalAlignment = HorizontalAlignment.Center;
        Grid.SetColumn(playbackButtonsPanel, 1);
        mainControlsGrid.Children.Add(playbackButtonsPanel);

        // Time Slider Panel (Stretches in its column)
        var timeSliderPanel = PlaybackTimeSliderPanel.Create(theme);
        Grid.SetColumn(timeSliderPanel, 2);
        mainControlsGrid.Children.Add(timeSliderPanel);

        var panelRoot = new Border
        {
            Background = theme.B_BackgroundColor,
            Padding = new Thickness(10, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MinHeight = 70,
            Child = mainControlsGrid
        };

        return panelRoot;
    }
}