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

        var songInfoPanel_Std = SongInfoDisplayPanel.Create(theme);
        var rightControlsPanel_Std = CreateRightControlsPanel(theme);

        var standardContentGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = new RowDefinitions("Auto"),
            ColumnDefinitions = new ColumnDefinitions("*,Auto,*"),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(songInfoPanel_Std, 0);
        Grid.SetColumn(verticalLayout, 1);
        Grid.SetColumn(rightControlsPanel_Std, 2);
        standardContentGrid.Children.Add(songInfoPanel_Std);
        standardContentGrid.Children.Add(verticalLayout);
        standardContentGrid.Children.Add(rightControlsPanel_Std);
        standardContentGrid.Bind(Visual.IsVisibleProperty, new Binding("!UseCompactPlaybackControls"));

        // --- Compact Horizontal Layout ---
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

        var songInfoPanel_Cpt = SongInfoDisplayPanel.Create(theme);
        var rightControlsPanel_Cpt = CreateRightControlsPanel(theme);

        var compactContentGrid = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            RowDefinitions = new RowDefinitions("Auto"),
            ColumnDefinitions = new ColumnDefinitions("2*,Auto,*"),
            Background = Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(songInfoPanel_Cpt, 0);
        Grid.SetColumn(horizontalLayout, 1);
        Grid.SetColumn(rightControlsPanel_Cpt, 2);
        compactContentGrid.Children.Add(songInfoPanel_Cpt);
        compactContentGrid.Children.Add(horizontalLayout);
        compactContentGrid.Children.Add(rightControlsPanel_Cpt);
        compactContentGrid.Bind(Visual.IsVisibleProperty, new Binding("UseCompactPlaybackControls"));

        // --- Panel to hold the two switchable layouts ---
        var contentContainer = new Panel();
        contentContainer.Children.Add(standardContentGrid);
        contentContainer.Children.Add(compactContentGrid);

        // --- Root Grid for background and layering ---
        var outerGrid = new Grid
        {
            Margin = new Thickness(0, 5, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ClipToBounds = true
        };
        outerGrid.Bind(Grid.BackgroundProperty, new Binding("PlaybackAreaBackground"));

        var stretchBackgroundImage = new Image
        {
            Stretch = Stretch.Fill,
            Opacity = 0.2
        };
        stretchBackgroundImage.Bind(Image.MaxHeightProperty, new Binding("Bounds.Height") { Source = contentContainer });
        stretchBackgroundImage.Bind(Image.SourceProperty, new Binding("AlbumArtForStretchBackground"));
        stretchBackgroundImage.Bind(Visual.IsVisibleProperty, new Binding("ShowAlbumArtStretchBackground"));

        var abstractBackgroundImage = new Image
        {
            Stretch = Stretch.Fill,
            Opacity = 0.35 // Increased opacity for more color presence
        };
        abstractBackgroundImage.Bind(Image.MaxHeightProperty, new Binding("Bounds.Height") { Source = contentContainer });
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
        outerGrid.Children.Add(contentContainer);

        return outerGrid;
    }

    private static StackPanel CreateRightControlsPanel(ThemeColors theme)
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

        var rightControlsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 10, 0)
        };
        rightControlsPanel.Children.Add(toggleAdvPanelButton);
        return rightControlsPanel;
    }
}