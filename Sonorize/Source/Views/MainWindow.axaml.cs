using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Data.Converters;
using Avalonia.Media; // Required for Color
using Sonorize.Models; // Required for ThemeColors, PlaybackStateStatus
using Sonorize.ViewModels; // Required for MainWindowViewModel
using Sonorize.Controls; // Required for WaveformDisplayControl
using System;
using System.Globalization;
using Sonorize.Services; // Required for CultureInfo

namespace Sonorize.Views;

// Partial class for the MainWindow UI (code-behind)
public partial class MainWindow : Window
{
    // Parameterless constructor needed for XAML previewer
    public MainWindow()
    {
        InitializeComponent();

#if DEBUG
        // Design-time logic here if necessary
        // Example: if (Design.IsDesignMode) { DataContext = new MainWindowViewModel(... design time services ...); }
#endif
    }

    // Constructor used by App.cs to pass theme
    // The DataContext is set in App.cs, so we just initialize components here
    public MainWindow(ThemeColors currentDisplayTheme) : this()
    {
        // No explicit theme application needed here if bindings handle it
        // We can store the theme if needed for other non-bound logic, but often not necessary with bindings
        // var theme = currentDisplayTheme;
    }

    // This method must exist so that AvaloniaXamlCompiler can hook in.
    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    // --- Event Handlers from XAML ---

    private void PlayPauseButton_Click(object? sender, RoutedEventArgs e)
    {
        // This logic was moved from the C# MainView definition
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Playing)
                vm.PlaybackService.Pause();
            else
                vm.PlaybackService.Resume();
        }
    }

    private void WaveformDisplay_SeekRequested(object? sender, TimeSpan e)
    {
        // This logic was moved from the C# MainView definition
        if (DataContext is MainWindowViewModel vm)
        {
            vm.WaveformSeekCommand.Execute(e);
        }
    }
}

// --- Converters (moved outside the MainWindow partial class) ---
// Make converters public so they can be referenced in XAML resources using just the namespace prefix

public class BooleanToPlayPauseTextConverter : IValueConverter
{
    public static readonly BooleanToPlayPauseTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool isPlaying) return isPlaying ? "Pause" : "Play";
        return "Play";
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

public class NotNullToBooleanConverter : IValueConverter
{
    public static readonly NotNullToBooleanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

// --- Dedicated converter for applying Alpha to a SolidColorBrush ---
// This replaces the incorrect usage of BrushExtensions.Multiply as a converter in the initial XAML attempt.
public class AccentBrushWithAlphaConverter : IValueConverter
{
    public static readonly AccentBrushWithAlphaConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is ISolidColorBrush solidBrush && parameter is double alphaFactor)
        {
            // Use the color from the brush and apply the alpha factor to create a new brush
            var color = solidBrush.Color;
            byte newAlpha = (byte)(Math.Clamp(alphaFactor, 0.0, 1.0) * 255);
            return new SolidColorBrush(Color.FromArgb(newAlpha, color.R, color.G, color.B));
        }
        // Fallback if input is not a SolidColorBrush or parameter is not a double
        return new SolidColorBrush(Colors.Transparent); // Or a default semi-transparent color
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

// Removing the old BrushExtensions class as it was not used correctly in XAML and the new converter serves the purpose.