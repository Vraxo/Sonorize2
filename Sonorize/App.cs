using System;
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
using Sonorize.Extensions;
using Sonorize.UI;
using Avalonia.Controls; // Added for ThemeResourceApplicator

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

            // Check if Mica is enabled and supported (Windows 11+)
            bool micaEnabled = appSettings.EnableMicaEffect &&
                               OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000);

            if (micaEnabled)
            {
                // For Mica to be visible, underlying controls need semi-transparent backgrounds.
                // We modify the loaded theme colors before they are used to build the UI.
                currentCustomTheme.BackgroundColor = WithAlpha(currentCustomTheme.BackgroundColor, "A0");
                currentCustomTheme.SlightlyLighterBackground = WithAlpha(currentCustomTheme.SlightlyLighterBackground, "A0");
                currentCustomTheme.ControlBackgroundColor = WithAlpha(currentCustomTheme.ControlBackgroundColor, "B0");
                currentCustomTheme.ListBoxBackground = WithAlpha(currentCustomTheme.ListBoxBackground, "90");
            }

            var fluentTheme = new FluentTheme();
            Styles.Add(fluentTheme);
            RequestedThemeVariant = ThemeVariant.Dark;
            Debug.WriteLine($"[App] RequestedThemeVariant set to: {RequestedThemeVariant}");

            // Delegate theme color application to the new service
            ThemeResourceApplicator.ApplyCustomColorsToResources(this, currentCustomTheme);

            // Use the bootstrapper to create services and the main ViewModel
            var bootstrapper = new ApplicationServicesBootstrapper();
            var mainWindowViewModel = bootstrapper.Bootstrap(settingsService, currentCustomTheme);

            // Pass micaEnabled flag to MainWindow constructor
            var mainWindow = new MainWindow(currentCustomTheme, micaEnabled)
            {
                DataContext = mainWindowViewModel
            };
            desktop.MainWindow = mainWindow;

            if (micaEnabled)
            {
                // The Window itself must be transparent for Mica to show through.
                mainWindow.Background = Brushes.Transparent;
                mainWindow.ExtendClientAreaToDecorationsHint = true;
                mainWindow.TransparencyLevelHint = new[] {
                    WindowTransparencyLevel.Mica,
                    WindowTransparencyLevel.AcrylicBlur,
                    WindowTransparencyLevel.None
                };
                Debug.WriteLine("[App] Mica effect enabled for MainWindow.");
            }

            mainWindowViewModel.LoadInitialDataCommand.Execute(null);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static string WithAlpha(string hex, string alphaHex)
    {
        if (hex.StartsWith("#") && (hex.Length == 7 || hex.Length == 9))
        {
            string colorPart = hex.Length == 9 ? hex.Substring(3) : hex.Substring(1);
            return $"#{alphaHex}{colorPart}";
        }
        return hex; // Return original if format is not recognized
    }
}