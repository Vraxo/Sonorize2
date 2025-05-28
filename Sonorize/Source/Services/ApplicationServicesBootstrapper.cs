using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Services;

public class ApplicationServicesBootstrapper
{
    public MainWindowViewModel Bootstrap(SettingsService settingsService, ThemeColors currentCustomTheme)
    {
        // Create all services
        var lastfmAuthenticatorService = new LastfmAuthenticatorService(settingsService);
        var scrobblingService = new ScrobblingService(settingsService, lastfmAuthenticatorService);
        var playbackService = new PlaybackService(scrobblingService);
        var loopDataService = new LoopDataService();
        var thumbnailService = new ThumbnailService();
        var songFactory = new SongFactory(loopDataService); // Create SongFactory
        var musicLibraryService = new MusicLibraryService(loopDataService, thumbnailService, songFactory); // Inject SongFactory
        var waveformService = new WaveformService();

        // Create MainWindowViewModel
        var mainWindowViewModel = new MainWindowViewModel(
            settingsService,
            musicLibraryService,
            playbackService,
            currentCustomTheme,
            waveformService,
            loopDataService,
            scrobblingService);

        return mainWindowViewModel;
    }
}