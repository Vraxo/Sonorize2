using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling; // Still useful for XAML-defined styles if any
using Avalonia.Styling;
using Sonorize.ViewModels;
using Sonorize.Views;
using Sonorize.Services;
using Sonorize.Models;
using Avalonia.Themes.Fluent;
using Avalonia.Media;          // Required for SolidColorBrush, Color, etc.
using System.Diagnostics;     // For Debug.WriteLine

namespace Sonorize;

public class App : Application
{
    public override void Initialize()
    {
        // Delay adding FluentTheme until OnFrameworkInitializationCompleted
        // where we have access to loaded settings and the custom theme.
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();
            var appSettings = settingsService.LoadSettings();

            var themeService = new ThemeService(appSettings.PreferredThemeFileName);
            ThemeColors currentCustomTheme = themeService.CurrentTheme;

            // 1. Add FluentTheme
            var fluentTheme = new FluentTheme();
            Styles.Add(fluentTheme);

            // 2. Set preferred theme variant (Dark/Light)
            // For Amoled Spotify, Dark is appropriate.
            RequestedThemeVariant = ThemeVariant.Dark;
            Debug.WriteLine($"[App] RequestedThemeVariant set to: {RequestedThemeVariant}");


            // 3. Override FluentTheme's accent color resources with your custom theme's accent
            if (currentCustomTheme.B_AccentColor is ISolidColorBrush accentSolidBrush &&
                currentCustomTheme.B_AccentForeground is ISolidColorBrush accentForegroundSolidBrush)
            {
                Color accentColor = accentSolidBrush.Color;
                Color accentForegroundColor = accentForegroundSolidBrush.Color;

                Debug.WriteLine($"[App] Overriding FluentTheme accent resources. Accent: {accentColor}, AccentFG: {accentForegroundColor}");

                // Core accent color
                Resources["SystemAccentColor"] = accentColor;

                // Derived accent colors (Fluent theme generates these, we provide overrides)
                // The lightness factors are illustrative; you might fine-tune them.
                Resources["SystemAccentColorLight1"] = accentColor.ChangeLightness(0.15);
                Resources["SystemAccentColorLight2"] = accentColor.ChangeLightness(0.30);
                Resources["SystemAccentColorLight3"] = accentColor.ChangeLightness(0.45);
                Resources["SystemAccentColorDark1"] = accentColor.ChangeLightness(-0.15);
                Resources["SystemAccentColorDark2"] = accentColor.ChangeLightness(-0.30);
                Resources["SystemAccentColorDark3"] = accentColor.ChangeLightness(-0.45);

                // Key brushes used by many controls
                Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(accentColor);
                Resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(accentColor.ChangeLightness(0.15).WithAlpha(204)); // ~80% opacity
                Resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(accentColor.ChangeLightness(0.30).WithAlpha(153));  // ~60% opacity
                Resources["AccentFillColorDisabledBrush"] = new SolidColorBrush(accentColor.WithAlpha(51)); // ~20% opacity
                Resources["AccentFillColorSelectedTextBackgroundBrush"] = new SolidColorBrush(accentColor);

                // Text on accent background
                Resources["TextOnAccentFillColorPrimaryBrush"] = new SolidColorBrush(accentForegroundColor);
                Resources["TextOnAccentFillColorSecondaryBrush"] = new SolidColorBrush(accentForegroundColor.WithAlpha(178)); // ~70%
                Resources["TextOnAccentFillColorDisabledBrush"] = new SolidColorBrush(accentForegroundColor.WithAlpha(127));  // ~50%

                // Specific for controls like buttons, checkbox backgrounds etc.
                Resources["AccentControlBackgroundBrush"] = new SolidColorBrush(accentColor);

                // You might need to identify more specific keys if issues persist using Avalonia DevTools
                // For example, for sliders:
                // Resources["SliderThumbBackground"] = new SolidColorBrush(accentColor); // General thumb
                // Resources["SliderThumbBackgroundPointerOver"] = new SolidColorBrush(accentColor.ChangeLightness(0.1));
                // Resources["SliderThumbBackgroundPressed"] = new SolidColorBrush(accentColor.ChangeLightness(-0.1));
                // Resources["SliderTrackValueFill"] = new SolidColorBrush(accentColor); // Filled part of the track
            }
            else
            {
                Debug.WriteLine("[App] Warning: Custom theme AccentColor or AccentForeground is not a SolidColorBrush. Cannot fully override Fluent accent system.");
            }

            var playbackService = new PlaybackService();
            var musicLibraryService = new MusicLibraryService();
            var waveformService = new WaveformService();

            var mainWindowViewModel = new MainWindowViewModel(
                settingsService,
                musicLibraryService,
                playbackService,
                currentCustomTheme, // Pass your custom theme colors
                waveformService);

            desktop.MainWindow = new MainWindow(currentCustomTheme) // MainWindow receives the custom theme
            {
                DataContext = mainWindowViewModel
            };

            mainWindowViewModel.LoadInitialDataCommand.Execute(null);
        }

        base.OnFrameworkInitializationCompleted();
    }
}

// Add WithAlpha helper if not already present (Avalonia.Media.Color doesn't have it directly)
public static class ColorManipulationExtensions // Can be in the same ColorExtensions.cs file
{
    public static Color WithAlpha(this Color color, byte alpha)
    {
        return new Color(alpha, color.R, color.G, color.B);
    }
}

public static class ColorExtensions
{
    public static Color ChangeLightness(this Color color, double factor)
    {
        var hsl = color.ToHsl();
        // factor > 0 lightens (e.g., 0.2 is 20% lighter)
        // factor < 0 darkens (e.g., -0.2 is 20% darker)
        // This adjusts the L (Lightness) component of HSL.
        double newL = System.Math.Clamp(hsl.L + factor, 0.0, 1.0);
        return HslColor.FromAhsl(hsl.A, hsl.H, hsl.S, newL).ToRgb();
    }
}