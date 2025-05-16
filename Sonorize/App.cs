using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling; // For FluentTheme even without XAML files
using Avalonia.Styling; // For ThemeVariant
using Sonorize.ViewModels;
using Sonorize.Views;
using Sonorize.Services;
using Avalonia.Themes.Fluent;
// using Avalonia.Themes.Fluent; // This was redundant

namespace Sonorize;

public class App : Application
{
    public override void Initialize()
    {
        // No XAML parsing, AvaloniaXamlLoader.Load(this); is not called.
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settingsService = new SettingsService();
            var playbackService = new PlaybackService();
            var musicLibraryService = new MusicLibraryService();

            var mainWindowViewModel = new MainWindowViewModel(settingsService, musicLibraryService, playbackService);

            // This new MainWindow should be Sonorize.Views.MainWindow (from your Source/Views/MainView.cs)
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            mainWindowViewModel.LoadInitialDataCommand.Execute(null);
        }

        base.OnFrameworkInitializationCompleted();
    }
}