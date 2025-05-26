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
using Sonorize.UI; // Added for ThemeResourceApplicator

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

            // Delegate theme color application to the new service
            ThemeResourceApplicator.ApplyCustomColorsToResources(this, currentCustomTheme);

            var lastfmAuthenticatorService = new LastfmAuthenticatorService(settingsService); // Create Authenticator
            var scrobblingService = new ScrobblingService(settingsService, lastfmAuthenticatorService); // Pass Authenticator
            var playbackService = new PlaybackService(scrobblingService);
            var loopDataService = new LoopDataService();
            var thumbnailService = new ThumbnailService();
            var musicLibraryService = new MusicLibraryService(loopDataService, thumbnailService);
            var waveformService = new WaveformService();

            var mainWindowViewModel = new MainWindowViewModel(
                settingsService,
                musicLibraryService,
                playbackService,
                currentCustomTheme,
                waveformService,
                loopDataService,
                scrobblingService);

            desktop.MainWindow = new MainWindow(currentCustomTheme)
            {
                DataContext = mainWindowViewModel
            };

            mainWindowViewModel.LoadInitialDataCommand.Execute(null);
        }

        base.OnFrameworkInitializationCompleted();
    }
}