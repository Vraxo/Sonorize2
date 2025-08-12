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
        var loopDataService = new LoopDataService();
        var playCountDataService = new PlayCountDataService(); // NEW
        var playbackService = new PlaybackService(scrobblingService, playCountDataService); // MODIFIED
        var defaultIconGenerator = new DefaultIconGenerator();
        var albumArtLoader = new AlbumArtLoader(); // Create AlbumArtLoader
        var thumbnailService = new ThumbnailService(defaultIconGenerator, albumArtLoader); // Pass both dependencies
        var songFactory = new SongFactory(loopDataService, playCountDataService); // MODIFIED
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