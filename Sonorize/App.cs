using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels;
using Sonorize.Views;

namespace Sonorize;

public class App : Application
{
    public override void Initialize()
    {
        // Delay adding FluentTheme until OnFrameworkInitializationCompleted
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();
            var appSettings = settingsService.LoadSettings();

            var themeService = new ThemeService(appSettings.PreferredThemeFileName);
            ThemeColors currentCustomTheme = themeService.CurrentTheme;

            var fluentTheme = new FluentTheme();
            Styles.Add(fluentTheme);
            RequestedThemeVariant = ThemeVariant.Dark;
            Debug.WriteLine($"[App] RequestedThemeVariant set to: {RequestedThemeVariant}");

            if (currentCustomTheme.B_AccentColor is ISolidColorBrush accentSolidBrush &&
                currentCustomTheme.B_AccentForeground is ISolidColorBrush accentForegroundSolidBrush)
            {
                Color accentColor = accentSolidBrush.Color;
                Color accentForegroundColor = accentForegroundSolidBrush.Color;
                Debug.WriteLine($"[App] Overriding FluentTheme accent resources. Accent: {accentColor}, AccentFG: {accentForegroundColor}");
                Resources["SystemAccentColor"] = accentColor;
                Resources["SystemAccentColorLight1"] = accentColor.ChangeLightness(0.15);
                Resources["SystemAccentColorLight2"] = accentColor.ChangeLightness(0.30);
                Resources["SystemAccentColorLight3"] = accentColor.ChangeLightness(0.45);
                Resources["SystemAccentColorDark1"] = accentColor.ChangeLightness(-0.15);
                Resources["SystemAccentColorDark2"] = accentColor.ChangeLightness(-0.30);
                Resources["SystemAccentColorDark3"] = accentColor.ChangeLightness(-0.45);
                Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(accentColor);
                Resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(accentColor.ChangeLightness(0.15).WithAlpha(204));
                Resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(accentColor.ChangeLightness(0.30).WithAlpha(153));
                Resources["AccentFillColorDisabledBrush"] = new SolidColorBrush(accentColor.WithAlpha(51));
                Resources["AccentFillColorSelectedTextBackgroundBrush"] = new SolidColorBrush(accentColor);
                Resources["TextOnAccentFillColorPrimaryBrush"] = new SolidColorBrush(accentForegroundColor);
                Resources["TextOnAccentFillColorSecondaryBrush"] = new SolidColorBrush(accentForegroundColor.WithAlpha(178));
                Resources["TextOnAccentFillColorDisabledBrush"] = new SolidColorBrush(accentForegroundColor.WithAlpha(127));
                Resources["AccentControlBackgroundBrush"] = new SolidColorBrush(accentColor);
            }
            else
            {
                Debug.WriteLine("[App] Warning: Custom theme AccentColor or AccentForeground is not a SolidColorBrush. Cannot fully override Fluent accent system.");
            }

            var scrobblingService = new ScrobblingService(settingsService); // Create ScrobblingService
            var playbackService = new PlaybackService(scrobblingService); // Pass ScrobblingService
            var loopDataService = new LoopDataService();
            var thumbnailService = new ThumbnailService(); // Create ThumbnailService
            var musicLibraryService = new MusicLibraryService(loopDataService, thumbnailService); // Pass ThumbnailService
            var waveformService = new WaveformService();

            var mainWindowViewModel = new MainWindowViewModel(
                settingsService,
                musicLibraryService,
                playbackService,
                currentCustomTheme,
                waveformService,
                loopDataService,
                scrobblingService); // Pass ScrobblingService

            desktop.MainWindow = new MainWindow(currentCustomTheme)
            {
                DataContext = mainWindowViewModel
            };

            mainWindowViewModel.LoadInitialDataCommand.Execute(null);
        }

        base.OnFrameworkInitializationCompleted();
    }
}

public static class ColorManipulationExtensions
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
        double newL = System.Math.Clamp(hsl.L + factor, 0.0, 1.0);
        return HslColor.FromAhsl(hsl.A, hsl.H, hsl.S, newL).ToRgb();
    }
}