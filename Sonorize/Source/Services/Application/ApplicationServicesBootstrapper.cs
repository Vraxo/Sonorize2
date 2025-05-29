using Sonorize.Models;
using Sonorize.Services.Playback; // Required for PlaybackResourceInterlockService
using Sonorize.ViewModels;

namespace Sonorize.Services;

public class ApplicationServicesBootstrapper
{
    public MainWindowViewModel Bootstrap(SettingsService settingsService, ThemeColors currentCustomTheme)
    {
        // Create all services
        var lastfmAuthenticatorService = new LastfmAuthenticatorService(settingsService);
        var scrobblingService = new ScrobblingService(settingsService, lastfmAuthenticatorService);
        var playbackService = new PlaybackService(scrobblingService); // PlaybackService creates its SessionManager
        var loopDataService = new LoopDataService();
        var thumbnailService = new ThumbnailService();
        var songFactory = new SongFactory(loopDataService);
        var musicLibraryService = new MusicLibraryService(loopDataService, thumbnailService, songFactory);
        var waveformService = new WaveformService();
        var songMetadataService = new SongMetadataService();

        // Create PlaybackResourceInterlockService, requires PlaybackSessionManager from PlaybackService
        var playbackResourceInterlockService = new PlaybackResourceInterlockService(playbackService.SessionManager);

        var songEditInteractionService = new SongEditInteractionService(playbackResourceInterlockService, songMetadataService, currentCustomTheme);

        // Create MainWindowViewModel
        var mainWindowViewModel = new MainWindowViewModel(
            settingsService,
            musicLibraryService,
            playbackService,
            currentCustomTheme,
            waveformService,
            loopDataService,
            scrobblingService,
            songMetadataService,
            songEditInteractionService);

        return mainWindowViewModel;
    }
}