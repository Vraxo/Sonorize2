using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters; // Added required using directive for FuncValueConverter
using Avalonia.Layout;
using Avalonia.Media;
using Sonorize.Converters;
using Sonorize.Models;

namespace Sonorize.Views.MainWindowControls;

public static class MainPlaybackControlsPanel
{
    public static Grid Create(ThemeColors theme) // Root is a Grid
    {
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

        // --- Standard Vertical Layout ---
        var verticalLayout = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Margin = new Thickness(0, 5, 0, 0),
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        verticalLayout.Children.Add(PlaybackNavigationButtonsPanel.Create(theme));
        verticalLayout.Children.Add(PlaybackTimeSliderPanel.Create(theme));
        verticalLayout.Bind(Visual.IsVisibleProperty, new Binding("!UseCompactPlaybackControls"));

        // --- New Compact Horizontal Layout ---
        var timeSliderGrid_Compact = PlaybackTimeSliderPanel.Create(theme);
        timeSliderGrid_Compact.MinWidth = 400;

        var horizontalLayout = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 5, 0, 0),
            Spacing = 15,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        horizontalLayout.Children.Add(PlaybackNavigationButtonsPanel.Create(theme));
        horizontalLayout.Children.Add(timeSliderGrid_Compact);
        horizontalLayout.Bind(Visual.IsVisibleProperty, new Binding("UseCompactPlaybackControls"));

        // --- Center Playback Controls Container ---
        var centerPlaybackControlsContainer = new Panel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        centerPlaybackControlsContainer.Children.Add(verticalLayout);
        centerPlaybackControlsContainer.Children.Add(horizontalLayout);


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
        Grid.SetColumn(centerPlaybackControlsContainer, 1);
        Grid.SetColumn(rightControlsPanel, 2);
        contentGrid.Children.Add(songInfoPanel);
        contentGrid.Children.Add(centerPlaybackControlsContainer);
        contentGrid.Children.Add(rightControlsPanel);


        // --- Root Grid for background and layering ---
        var outerGrid = new Grid
        {
            Margin = new Thickness(0, 5, 0, 5),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true
        };
        outerGrid.Bind(Grid.BackgroundProperty, new Binding("PlaybackAreaBackground"));

        var stretchBackgroundImage = new Image
        {
            Stretch = Stretch.Fill,
            Opacity = 0.2
        };
        stretchBackgroundImage.Bind(Image.MaxHeightProperty, new Binding("Bounds.Height") { Source = contentGrid });
        stretchBackgroundImage.Bind(Image.SourceProperty, new Binding("AlbumArtForStretchBackground"));
        stretchBackgroundImage.Bind(Visual.IsVisibleProperty, new Binding("ShowAlbumArtStretchBackground"));

        var abstractBackgroundImage = new Image
        {
            Stretch = Stretch.Fill,
            Opacity = 0.35 // Increased opacity for more color presence
        };
        abstractBackgroundImage.Bind(Image.MaxHeightProperty, new Binding("Bounds.Height") { Source = contentGrid });
        abstractBackgroundImage.Bind(Image.SourceProperty, new Binding("AlbumArtForAbstractBackground"));
        abstractBackgroundImage.Bind(Visual.IsVisibleProperty, new Binding("ShowAlbumArtAbstractBackground"));


        var backgroundOverlay = new Border
        {
            Background = new SolidColorBrush(Colors.Black, 0.4) // Reduced opacity further
        };
        backgroundOverlay.Bind(Visual.IsVisibleProperty, new MultiBinding
        {
            Converter = OrBooleanConverter.Instance,
            Bindings =
            {
                new Binding("ShowAlbumArtStretchBackground"),
                new Binding("ShowAlbumArtAbstractBackground")
            }
        });

        // Add layers to the outer grid. Backgrounds first, then the contentGrid on top.
        outerGrid.Children.Add(stretchBackgroundImage);
        outerGrid.Children.Add(abstractBackgroundImage);
        outerGrid.Children.Add(backgroundOverlay);
        outerGrid.Children.Add(contentGrid);

        return outerGrid;
    }
}