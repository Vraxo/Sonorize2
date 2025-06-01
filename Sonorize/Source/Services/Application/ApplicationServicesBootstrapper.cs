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
        // Removed: var scrobbleEligibilityService = new ScrobbleEligibilityService();
        var scrobblingService = new ScrobblingService(settingsService, lastfmAuthenticatorService); // Updated instantiation
        var playbackService = new PlaybackService(scrobblingService);
        var loopDataService = new LoopDataService();
        var defaultIconGenerator = new DefaultIconGenerator();
        var albumArtLoader = new AlbumArtLoader(); // Create AlbumArtLoader
        var thumbnailService = new ThumbnailService(defaultIconGenerator, albumArtLoader); // Pass both dependencies
        var songFactory = new SongFactory(loopDataService);
        var musicLibraryService = new MusicLibraryService(loopDataService, thumbnailService, songFactory);
        var waveformService = new WaveformService();
        var songMetadataService = new SongMetadataService();
        var songLoopService = new SongLoopService(loopDataService);

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
            songEditInteractionService,
            songLoopService);

        return mainWindowViewModel;
    }
}