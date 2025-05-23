using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives; // For Thumb
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Sonorize.Controls;
using Sonorize.Converters;
using Sonorize.Models; // For ThemeColors
using Sonorize.ViewModels; // For MainWindowViewModel, LoopEditorViewModel

namespace Sonorize.Views.MainWindowControls;

public static class AdvancedPlaybackPanelControls
{
    public static Border Create(ThemeColors theme)
    {
        var panelRoot = new Border
        {
            Background = theme.B_SlightlyLighterBackground,
            Padding = new Thickness(10),
            BorderBrush = theme.B_AccentColor,
            BorderThickness = new Thickness(0, 1, 0, 1),
            MinHeight = 180,
            ClipToBounds = true
        };
        var mainStack = new StackPanel { Spacing = 10 };

        // Speed and Pitch Controls
        var speedPitchGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,15,Auto,*,Auto"), Margin = new Thickness(0, 0, 0, 5) };
        var speedLabel = new TextBlock { Text = "Tempo:", VerticalAlignment = VerticalAlignment.Center, Foreground = theme.B_TextColor, Margin = new Thickness(0, 0, 5, 0) };
        var speedSlider = new Slider { Minimum = 0.5, Maximum = 2.0, SmallChange = 0.05, LargeChange = 0.25, TickFrequency = 0.25, Foreground = theme.B_AccentColor, Background = theme.B_SecondaryTextColor };
        speedSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor) } });
        speedSlider.Bind(Slider.ValueProperty, new Binding("Playback.PlaybackSpeed", BindingMode.TwoWay));
        var speedDisplay = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0), Foreground = theme.B_TextColor, MinWidth = 35, HorizontalAlignment = HorizontalAlignment.Right };
        speedDisplay.Bind(TextBlock.TextProperty, new Binding("Playback.PlaybackSpeedDisplay"));

        var pitchLabel = new TextBlock { Text = "Pitch:", VerticalAlignment = VerticalAlignment.Center, Foreground = theme.B_TextColor, Margin = new Thickness(0, 0, 5, 0) };
        var pitchSlider = new Slider { Minimum = -4, Maximum = 4, SmallChange = 0.1, LargeChange = 0.5, TickFrequency = 0.5, Foreground = theme.B_AccentColor, Background = theme.B_SecondaryTextColor };
        pitchSlider.Styles.Add(new Style(s => s.Is<Thumb>()) { Setters = { new Setter(TemplatedControl.BackgroundProperty, theme.B_AccentColor) } });
        pitchSlider.Bind(Slider.ValueProperty, new Binding("Playback.PlaybackPitch", BindingMode.TwoWay));
        var pitchDisplay = new TextBlock { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0), Foreground = theme.B_TextColor, MinWidth = 45, HorizontalAlignment = HorizontalAlignment.Right };
        pitchDisplay.Bind(TextBlock.TextProperty, new Binding("Playback.PlaybackPitchDisplay"));

        Grid.SetColumn(speedLabel, 0); Grid.SetColumn(speedSlider, 1); Grid.SetColumn(speedDisplay, 2);
        Grid.SetColumn(pitchLabel, 4); Grid.SetColumn(pitchSlider, 5); Grid.SetColumn(pitchDisplay, 6);
        speedPitchGrid.Children.Add(speedLabel); speedPitchGrid.Children.Add(speedSlider); speedPitchGrid.Children.Add(speedDisplay);
        speedPitchGrid.Children.Add(pitchLabel); speedPitchGrid.Children.Add(pitchSlider); speedPitchGrid.Children.Add(pitchDisplay);
        mainStack.Children.Add(speedPitchGrid);

        // Waveform Display
        Color accentColorForLoopRegion = (theme.B_AccentColor as ISolidColorBrush)?.Color ?? Colors.Orange;
        var waveformDisplay = new WaveformDisplayControl
        {
            Height = 80,
            MinHeight = 60,
            Background = theme.B_ControlBackgroundColor,
            WaveformBrush = theme.B_AccentColor,
            PositionMarkerBrush = Brushes.OrangeRed,
            LoopRegionBrush = new SolidColorBrush(accentColorForLoopRegion, 0.3)
        };
        waveformDisplay.Bind(WaveformDisplayControl.WaveformPointsProperty, new Binding("Playback.WaveformRenderData"));
        waveformDisplay.Bind(WaveformDisplayControl.CurrentPositionProperty, new Binding("Playback.CurrentPosition"));
        waveformDisplay.Bind(WaveformDisplayControl.DurationProperty, new Binding("Playback.CurrentSongDuration"));
        waveformDisplay.Bind(WaveformDisplayControl.ActiveLoopProperty, new Binding("Playback.PlaybackService.CurrentSong.SavedLoop"));
        waveformDisplay.SeekRequested += (s, time) =>
        {
            if (s is Control { DataContext: MainWindowViewModel mainWindowVM })
            {
                mainWindowVM.LoopEditor.WaveformSeekCommand.Execute(time);
            }
        };

        var waveformLoadingIndicator = new ProgressBar { IsIndeterminate = true, Height = 5, Margin = new Thickness(0, -5, 0, 0), Foreground = theme.B_AccentColor, Background = Brushes.Transparent };
        waveformLoadingIndicator.Bind(Visual.IsVisibleProperty, new Binding("Playback.IsWaveformLoading"));
        var waveformContainer = new Panel();
        waveformContainer.Children.Add(waveformDisplay); waveformContainer.Children.Add(waveformLoadingIndicator);
        mainStack.Children.Add(waveformContainer);

        // Loop Controls
        var loopControlsOuterPanel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 5, Margin = new Thickness(0, 10, 0, 0) };
        var loopDefinitionLabel = new TextBlock { Text = "Define Loop:", FontSize = 12, FontWeight = FontWeight.SemiBold, Foreground = theme.B_TextColor };
        var loopActionsPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };

        var setStartBtn = new Button { Content = "A", FontSize = 12, Padding = new Thickness(10, 5), MinWidth = 40, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        setStartBtn.Bind(Button.CommandProperty, new Binding("LoopEditor.CaptureLoopStartCandidateCommand"));
        var startDisp = new TextBlock { FontSize = 11, Margin = new Thickness(3, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = theme.B_SecondaryTextColor, MinWidth = 60 };
        startDisp.Bind(TextBlock.TextProperty, new Binding("LoopEditor.NewLoopStartCandidateDisplay"));

        var setEndBtn = new Button { Content = "B", FontSize = 12, Padding = new Thickness(10, 5), MinWidth = 40, Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        setEndBtn.Bind(Button.CommandProperty, new Binding("LoopEditor.CaptureLoopEndCandidateCommand"));
        var endDisp = new TextBlock { FontSize = 11, Margin = new Thickness(3, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = theme.B_SecondaryTextColor, MinWidth = 60 };
        endDisp.Bind(TextBlock.TextProperty, new Binding("LoopEditor.NewLoopEndCandidateDisplay"));

        var saveLoopBtn = new Button { Content = "Save Loop", FontSize = 11, Padding = new Thickness(10, 5), Background = theme.B_AccentColor, Foreground = theme.B_AccentForeground };
        saveLoopBtn.Bind(Button.CommandProperty, new Binding("LoopEditor.SaveLoopCommand"));
        saveLoopBtn.Bind(Button.IsEnabledProperty, new Binding("LoopEditor.CanSaveLoopRegion"));

        var clearLoopBtn = new Button { Content = "Clear Loop", FontSize = 11, Padding = new Thickness(10, 5), Background = theme.B_ControlBackgroundColor, Foreground = theme.B_TextColor };
        clearLoopBtn.Bind(Button.CommandProperty, new Binding("LoopEditor.ClearLoopCommand"));
        var clearLoopBinding = new Binding("PlaybackService.CurrentSong.SavedLoop") { Converter = NotNullToBooleanConverter.Instance };
        clearLoopBtn.Bind(Button.IsEnabledProperty, clearLoopBinding);

        loopActionsPanel.Children.Add(setStartBtn); loopActionsPanel.Children.Add(startDisp);
        loopActionsPanel.Children.Add(setEndBtn); loopActionsPanel.Children.Add(endDisp);
        loopActionsPanel.Children.Add(saveLoopBtn); loopActionsPanel.Children.Add(clearLoopBtn);

        var loopActiveTogglePanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0), Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        var loopActiveCheckBox = new CheckBox { Content = "Activate Loop", Foreground = theme.B_TextColor, VerticalAlignment = VerticalAlignment.Center };
        loopActiveCheckBox.Bind(ToggleButton.IsCheckedProperty, new Binding("LoopEditor.IsCurrentLoopActiveUiBinding", BindingMode.TwoWay));
        var loopActiveCheckBoxIsEnabledBinding = new Binding("PlaybackService.CurrentSong.SavedLoop") { Converter = NotNullToBooleanConverter.Instance };
        loopActiveCheckBox.Bind(Control.IsEnabledProperty, loopActiveCheckBoxIsEnabledBinding); // Corrected: Control.IsEnabledProperty
        loopActiveTogglePanel.Children.Add(loopActiveCheckBox);

        loopControlsOuterPanel.Children.Add(loopDefinitionLabel);
        loopControlsOuterPanel.Children.Add(loopActionsPanel);
        loopControlsOuterPanel.Children.Add(loopActiveTogglePanel);
        mainStack.Children.Add(loopControlsOuterPanel);

        panelRoot.Child = mainStack;
        return panelRoot;
    }
}