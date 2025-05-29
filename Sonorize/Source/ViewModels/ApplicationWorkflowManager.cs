using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls; // For Window
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.Status; // For StatusBarTextProvider

namespace Sonorize.ViewModels;

public class ApplicationWorkflowManager : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly ScrobblingService _scrobblingService;
    private readonly ThemeColors _currentTheme;
    private readonly LibraryViewModel _libraryViewModel;
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly PlaybackService _playbackService;

    private readonly NextTrackSelectorService _nextTrackSelectorService;
    private readonly StatusBarTextProvider _statusBarTextProvider;
    private readonly SettingsChangeProcessorService _settingsChangeProcessorService;
    private readonly PlaybackFlowManagerService _playbackFlowManagerService;
    private readonly ApplicationInteractionService _applicationInteractionService;
    private readonly LibraryPlaybackLinkService _libraryPlaybackLinkService;
    private readonly SongMetadataService _songMetadataService; // Added


    private readonly Random _shuffleRandom = new();

    public ApplicationWorkflowManager(
        SettingsService settingsService,
        ScrobblingService scrobblingService,
        ThemeColors currentTheme,
        LibraryViewModel libraryViewModel,
        PlaybackViewModel playbackViewModel,
        PlaybackService playbackService,
        LoopDataService loopDataService,
        SongMetadataService songMetadataService) // Added SongMetadataService
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        _currentTheme = currentTheme ?? throw new ArgumentNullException(nameof(currentTheme));
        _libraryViewModel = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));
        _playbackViewModel = playbackViewModel ?? throw new ArgumentNullException(nameof(playbackViewModel));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _songMetadataService = songMetadataService ?? throw new ArgumentNullException(nameof(songMetadataService)); // Added

        // Create internal services
        _nextTrackSelectorService = new NextTrackSelectorService(_shuffleRandom);
        _statusBarTextProvider = new StatusBarTextProvider(_playbackViewModel, null!, _libraryViewModel); // Placeholder

        _settingsChangeProcessorService = new SettingsChangeProcessorService(_libraryViewModel, _scrobblingService);
        _playbackFlowManagerService = new PlaybackFlowManagerService(_libraryViewModel, _playbackViewModel, _playbackService, _nextTrackSelectorService);

        _applicationInteractionService = new ApplicationInteractionService(
            _settingsService,
            _settingsChangeProcessorService,
            _currentTheme,
            _songMetadataService); // Pass SongMetadataService

        _libraryPlaybackLinkService = new LibraryPlaybackLinkService(_libraryViewModel, _playbackService, _playbackViewModel);
    }

    public async Task<(List<string> statusMessages, bool settingsChanged)> HandleOpenSettingsDialogAsync(Window owner)
    {
        return await _applicationInteractionService.HandleOpenSettingsDialogAsync(owner);
    }

    public async Task<(bool directoryAddedAndLibraryRefreshNeeded, string statusMessage)> HandleAddMusicDirectoryAsync(Window owner)
    {
        return await _applicationInteractionService.HandleAddMusicDirectoryAsync(owner);
    }

    public async Task<bool> HandleEditSongMetadataDialogAsync(Song song, Window owner)
    {
        return await _applicationInteractionService.HandleEditSongMetadataDialogAsync(song, owner);
    }

    public void HandlePlaybackEndedNaturally()
    {
        _playbackFlowManagerService.HandlePlaybackEndedNaturally();
    }

    public string GetCurrentStatusText(LoopEditorViewModel loopEditorViewModel)
    {
        var localStatusBarTextProvider = new StatusBarTextProvider(_playbackViewModel, loopEditorViewModel, _libraryViewModel);
        return localStatusBarTextProvider.GetCurrentStatusText();
    }

    public void Dispose()
    {
        _libraryPlaybackLinkService?.Dispose();
    }
}