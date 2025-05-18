using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Sonorize.Services;
using Sonorize.ViewModels;
using System;
using System.Globalization;

namespace Sonorize.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void WaveformDisplay_SeekRequested(object? sender, TimeSpan time)
        {
            if (this.DataContext is MainWindowViewModel vm && vm.WaveformSeekCommand.CanExecute(time))
            {
                vm.WaveformSeekCommand.Execute(time);
            }
        }

        private void MainPlayPauseButton_Click(object? sender, RoutedEventArgs e)
        {
            if (this.DataContext is MainWindowViewModel vm)
            {
                if (vm.PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Playing)
                    vm.PlaybackService.Pause();
                else
                    vm.PlaybackService.Resume();
            }
        }
    }

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

    public static class BrushExtensions
    {
        public static IBrush Multiply(this IBrush brush, double factor)
        {
            if (brush is ISolidColorBrush solidBrush)
            {
                var c = solidBrush.Color;
                return new SolidColorBrush(Color.FromArgb(c.A, (byte)Math.Clamp(c.R * factor, 0, 255), (byte)Math.Clamp(c.G * factor, 0, 255), (byte)Math.Clamp(c.B * factor, 0, 255)));
            }
            return brush;
        }
    }
}