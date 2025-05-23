using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb, RepeatButton
using Avalonia.Controls.Templates;  // For FuncControlTemplate
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Converters;
using Sonorize.Models; // For ThemeColors

namespace Sonorize.Views.MainWindowControls;

public static class MainPlaybackControlsPanel
{
    public static StackPanel Create(ThemeColors theme)
    {
        var mainPlaybackSlider = new Slider
        {
            Name = "MainPlaybackSliderInstance",
            Minimum = 0,
            VerticalAlignment = VerticalAlignment.Center,
            Background = theme.B_SecondaryTextColor, // This is the inactive part of the track
            Foreground = theme.B_AccentColor,       // This is the active part of the track
            // Example: Set a CornerRadius for the slider track itself if desired
            // CornerRadius = new CornerRadius(2), // This would make the track pill-shaped if height is 4
            // Height = 4, // Example height, Fluent default track height is 4
        };

        // Style the Thumb to be completely invisible and non-interactive
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<Thumb>())
        {
            Setters =
            {
                new Setter(Thumb.OpacityProperty, 0.0),
                new Setter(TemplatedControl.BackgroundProperty, Brushes.Transparent),
                new Setter(Thumb.BorderThicknessProperty, new Thickness(0)),
                new Setter(Thumb.WidthProperty, 0.0),
                new Setter(Thumb.HeightProperty, 0.0),
                new Setter(Thumb.MinWidthProperty, 0.0),
                new Setter(Thumb.MinHeightProperty, 0.0),
                new Setter(Thumb.IsHitTestVisibleProperty, false),
                new Setter(Thumb.FocusableProperty, false),
                new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<Thumb>((_, __) => new Panel()))
            }
        });

        // Style RepeatButtons (PART_DecreaseButton and PART_IncreaseButton) within this Slider
        // to be simple, flat-colored borders. Their Background is set by Slider's Foreground/Background.
        mainPlaybackSlider.Styles.Add(new Style(s => s.Is<RepeatButton>())
        {
            Setters =
            {
                new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(0)), // No border on the RepeatButton itself
                new Setter(TemplatedControl.TemplateProperty, new FuncControlTemplate<RepeatButton>((control, scope) =>
                    new Border
                    {
                        // Bind this Border's Background to the RepeatButton's Background property.
                        // The Slider's template links Slider.Foreground to PART_DecreaseButton.Background,
                        // and Slider.Background to PART_IncreaseButton.Background.
                        [!Border.BackgroundProperty] = control[!TemplatedControl.BackgroundProperty],
                        // Ensure the internal parts of the track are sharp,
                        // the Slider's own CornerRadius will handle the overall track shape.
                        CornerRadius = new CornerRadius(0)
                    }))
            }
        });


        mainPlaybackSlider.Bind(Slider.MaximumProperty, new Binding("Playback.CurrentSongDurationSeconds"));
        mainPlaybackSlider.Bind(Slider.ValueProperty, new Binding("Playback.CurrentPositionSeconds", BindingMode.TwoWay));
        mainPlaybackSlider.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var mainPlayPauseButton = new Button { Content = "Play", Background = theme.B_SlightlyLighterBackground, Foreground = theme.B_TextColor, BorderBrush = theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(10, 5), MinWidth = 70 };
        mainPlayPauseButton.Bind(Button.CommandProperty, new Binding("Playback.PlayPauseResumeCommand"));
        var playPauseContentBinding = new Binding("Playback.IsPlaying") { Converter = BooleanToPlayPauseTextConverter.Instance };
        mainPlayPauseButton.Bind(Button.ContentProperty, playPauseContentBinding);

        var toggleAdvPanelButton = new Button { Content = "+", Background = theme.B_SlightlyLighterBackground, Foreground = theme.B_TextColor, BorderBrush = theme.B_AccentColor, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Padding = new Thickness(8, 4), MinWidth = 30, FontWeight = FontWeight.Bold };
        toggleAdvPanelButton.Bind(Button.CommandProperty, new Binding("ToggleAdvancedPanelCommand"));
        toggleAdvPanelButton.Bind(Control.IsEnabledProperty, new Binding("Playback.HasCurrentSong"));

        var timeDisplayTextBlock = new TextBlock
        {
            Foreground = theme.B_TextColor,
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 0, 0), // Adjusted margin for new layout
            MinWidth = 75
        };
        timeDisplayTextBlock.Bind(TextBlock.TextProperty, new Binding("Playback.CurrentTimeTotalTimeDisplay"));
        timeDisplayTextBlock.Bind(Visual.IsVisibleProperty, new Binding("Playback.HasCurrentSong"));

        // New layout for topMainPlaybackControls using a Grid
        var topMainPlaybackControls = new Grid
        {
            Height = 35,
            Margin = new Thickness(10, 0), // Outer margin for the whole control strip
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") // Col0 for Toggle, Col1 for Slider+Play, Col2 for Time
        };

        // Column 0: Toggle Advanced Panel Button
        toggleAdvPanelButton.VerticalAlignment = VerticalAlignment.Center;
        toggleAdvPanelButton.Margin = new Thickness(0, 0, 10, 0); // Space between toggle button and center content
        Grid.SetColumn(toggleAdvPanelButton, 0);
        topMainPlaybackControls.Children.Add(toggleAdvPanelButton);

        // Column 1: Slider and Play Button (layered)
        var centerContentPanel = new Panel // This Panel will be in Grid Column 1, allowing layering
        {
            // Panel stretches by default in Grid cell. Children are positioned relative to this Panel.
        };
        // Add Slider first (drawn below)
        mainPlaybackSlider.VerticalAlignment = VerticalAlignment.Center; // Center slider within the Panel
        centerContentPanel.Children.Add(mainPlaybackSlider);

        // Add Play Button second (drawn on top)
        mainPlayPauseButton.HorizontalAlignment = HorizontalAlignment.Center; // Center button horizontally in the Panel
        mainPlayPauseButton.VerticalAlignment = VerticalAlignment.Center; // Center button vertically in the Panel
        centerContentPanel.Children.Add(mainPlayPauseButton);

        Grid.SetColumn(centerContentPanel, 1);
        topMainPlaybackControls.Children.Add(centerContentPanel);

        // Column 2: Time Display Text Block
        timeDisplayTextBlock.VerticalAlignment = VerticalAlignment.Center; // Ensure vertical centering
        // timeDisplayTextBlock.Margin is already set to provide spacing from the left (10,0,0,0)
        Grid.SetColumn(timeDisplayTextBlock, 2);
        topMainPlaybackControls.Children.Add(timeDisplayTextBlock);


        var activeLoopDisplayText = new TextBlock { Foreground = theme.B_SecondaryTextColor, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(10, 0, 10, 2), MinHeight = 14 };
        activeLoopDisplayText.Bind(TextBlock.TextProperty, new Binding("LoopEditor.ActiveLoopDisplayText"));

        var outerPanel = new StackPanel { Orientation = Orientation.Vertical, Background = theme.B_BackgroundColor, Margin = new Thickness(0, 5, 0, 5) };
        outerPanel.Children.Add(topMainPlaybackControls);
        outerPanel.Children.Add(activeLoopDisplayText);
        return outerPanel;
    }
}