using System;
using Avalonia.Controls;
using Avalonia.Media;
using Sonorize.Models;
using Sonorize.ViewModels;
using Sonorize.Controls; // Required for WaveformDisplayControl
using Avalonia.Data.Converters; // Required for IValueConverter (if defined here)
using Avalonia.Interactivity; // Required for RoutedEventArgs
using System.Globalization;
using Sonorize.Services; // Required for CultureInfo

namespace Sonorize.Views
{
    // Regarding AVLN2002 Error ("Duplicate x:Class directive"):
    // This error typically originates from the .axaml file or the project configuration (.csproj)
    // rather than the C# code-behind file itself, assuming this C# file is correctly structured.
    // Common causes for AVLN2002 include:
    // 1. The corresponding .axaml file (e.g., MainWindow.axaml) might have a syntactical error,
    //    such as literally including the x:Class attribute twice on the root element.
    // 2. Another .axaml file in the project might be incorrectly using the same x:Class
    //    directive (e.g., x:Class="Sonorize.Views.MainWindow").
    // 3. Issues in the .csproj file leading to the XAML or class being processed multiple times.
    // 4. A corrupted build cache; performing a "Clean" and then "Rebuild" of the project can often resolve this.
    // This C# code-behind file (MainWindow.axaml.cs) appears to follow standard Avalonia patterns:
    // - It is declared as a 'partial' class.
    // - It calls 'InitializeComponent()' in its constructors to link with the XAML.
    // No changes to the C# logic are made here as it seems correct; the fix for AVLN2002
    // usually lies in inspecting the .axaml file content or project settings.
    public partial class MainWindow : Window // Ensure 'partial' keyword
    {
        private readonly ThemeColors _theme;

        public MainWindow() // Parameterless constructor for XAML
        {
            InitializeComponent();
            // _theme might not be available here if not passed.
            // If default theme values are needed before DataContext is set, handle appropriately.
            // For this example, we assume ThemeColors comes from DataContext or is passed.
        }

        public MainWindow(ThemeColors theme)
        {
            _theme = theme;
            InitializeComponent();

            var waveformDisplay = this.FindControl<WaveformDisplayControl>("WaveformDisplay");
            if (waveformDisplay != null)
            {
                if (_theme.B_AccentColor is ISolidColorBrush accentBrush)
                {
                    waveformDisplay.LoopRegionBrush = new SolidColorBrush(accentBrush.Color, 0.3);
                }
                else
                {
                    // Fallback if B_AccentColor is not a SolidColorBrush, though your model implies it is.
                    waveformDisplay.LoopRegionBrush = new SolidColorBrush(Colors.Orange, 0.3);
                }
            }
        }

        private void MainPlayPauseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                if (vm.PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Playing)
                    vm.PlaybackService.Pause();
                else
                    vm.PlaybackService.Resume(); // Resume will handle playing from stopped or paused
            }
        }

        private void WaveformDisplay_SeekRequested(object? sender, TimeSpan time)
        {
            if (DataContext is MainWindowViewModel vm)
            {
                vm.WaveformSeekCommand.Execute(time);
            }
        }
    }

    // Converters are better placed in their own files or a shared Converters namespace/file,
    // but kept here for direct translation from your original structure.
    // If they are already in separate files (as they seem to be, based on your MainView.cs),
    // you don't need to redefine them here. The XAML will use xmlns to find them.

    public class BooleanToPlayPauseTextConverter : IValueConverter
    {
        public static readonly BooleanToPlayPauseTextConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isPlaying) return isPlaying ? "Pause" : "Play";
            return "Play";
        }
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    public class NotNullToBooleanConverter : IValueConverter
    {
        public static readonly NotNullToBooleanConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}