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