using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
// Removed converter usings as they are no longer defined here
using Sonorize.Models; // Required for ThemeColors
using Sonorize.ViewModels; // Required for MainWindowViewModel
// Removed Control usings if controls are now referenced only in XAML via namespace
// using Sonorize.Controls; // May still be needed if interacting with controls in code-behind
// Removed System, System.Globalization if not used directly anymore

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
    // These handlers are now on the specific UserControl code-behinds

    // private void PlayPauseButton_Click(object? sender, RoutedEventArgs e) { ... moved to MainPlaybackControl.axaml.cs }
    // private void WaveformDisplay_SeekRequested(object? sender, TimeSpan e) { ... moved to AdvancedPlaybackControl.axaml.cs }

    // --- Converters (moved outside the MainWindow partial class to separate files) ---
}

// Converters are now in their own files and are top-level classes in the Sonorize.Views namespace.
// They are no longer defined within MainWindow.axaml.cs.
/*
public class BooleanToPlayPauseTextConverter : IValueConverter { ... moved }
public class NotNullToBooleanConverter : IValueConverter { ... moved }
public class AccentBrushWithAlphaConverter : IValueConverter { ... moved }
*/