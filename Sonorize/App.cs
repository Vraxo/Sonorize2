using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Sonorize.ViewModels;
using Sonorize.Views;
using Sonorize.Services;
using Sonorize.Models;
using Avalonia.Themes.Fluent;

namespace Sonorize;

public class App : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService(); // Load settings first
            var appSettings = settingsService.LoadSettings();

            var themeService = new ThemeService(appSettings.PreferredThemeFileName); // Pass preferred theme name
            var currentTheme = themeService.CurrentTheme;

            var playbackService = new PlaybackService();
            var musicLibraryService = new MusicLibraryService();
            var waveformService = new WaveformService(); // Instantiate WaveformService

            var mainWindowViewModel = new MainWindowViewModel(
                settingsService,
                musicLibraryService,
                playbackService,
                currentTheme,
                waveformService); // Pass WaveformService instance

            desktop.MainWindow = new MainWindow(currentTheme)
            {
                DataContext = mainWindowViewModel
            };

            mainWindowViewModel.LoadInitialDataCommand.Execute(null);
        }

        base.OnFrameworkInitializationCompleted();
    }
}