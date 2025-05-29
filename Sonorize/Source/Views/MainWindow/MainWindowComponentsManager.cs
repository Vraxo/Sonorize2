using System;
using System.ComponentModel;
using Avalonia.Controls;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.LibraryManagement; // Required for LibraryDisplayModeService

namespace Sonorize.ViewModels.MainWindow;

public class MainWindowComponentsManager : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly PlaybackService _playbackService;
    private readonly ThemeColors _currentTheme;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    private readonly ScrobblingService _scrobblingService;
    private readonly SongMetadataService _songMetadataService;
    private readonly SongEditInteractionService _songEditInteractionService;
    private readonly SongLoopService _songLoopService;

    public LibraryViewModel Library { get; }
    public PlaybackViewModel Playback { get; }
    public LoopEditorViewModel LoopEditor { get; }
    public AdvancedPanelViewModel AdvancedPanel { get; }
    public LibraryDisplayModeService LibraryDisplayModeService { get; }
    public ApplicationWorkflowManager WorkflowManager { get; }
    public MainWindowInteractionCoordinator InteractionCoordinator { get; }
    public MainWindowViewModelOrchestrator ViewModelOrchestrator { get; }

    public MainWindowComponentsManager(
        MainWindowViewModel parentMainWindowViewModel,
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors currentTheme,
        WaveformService waveformService,
        LoopDataService loopDataService,
        ScrobblingService scrobblingService,
        SongMetadataService songMetadataService,
        SongEditInteractionService songEditInteractionService,
        SongLoopService songLoopService,
        Func<Window?> getOwnerViewFunc,
        Action raiseAllCommandsCanExecuteChangedCallback,
        Action updateStatusBarTextCallback,
        Action<string> notifyMainWindowVMPropertyChangedCallback)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        _playbackService = playbackService;
        _currentTheme = currentTheme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;
        _scrobblingService = scrobblingService;
        _songMetadataService = songMetadataService;
        _songEditInteractionService = songEditInteractionService;
        _songLoopService = songLoopService;

        LibraryDisplayModeService = new LibraryDisplayModeService(_settingsService);
        Library = new LibraryViewModel(parentMainWindowViewModel, _settingsService, _musicLibraryService, _loopDataService, LibraryDisplayModeService);
        Playback = new PlaybackViewModel(_playbackService, _waveformService);
        LoopEditor = new LoopEditorViewModel(_playbackService, _loopDataService, _songLoopService);
        AdvancedPanel = new AdvancedPanelViewModel(Playback, Library);

        WorkflowManager = new ApplicationWorkflowManager(
            _settingsService,
            _scrobblingService,
            _currentTheme,
            Library,
            Playback,
            _playbackService,
            _loopDataService);

        InteractionCoordinator = new MainWindowInteractionCoordinator(
            getOwnerViewFunc,
            Library,
            AdvancedPanel,
            WorkflowManager,
            _songEditInteractionService,
            raiseAllCommandsCanExecuteChangedCallback
        );

        ViewModelOrchestrator = new MainWindowViewModelOrchestrator(
            Library,
            Playback,
            AdvancedPanel,
            raiseAllCommandsCanExecuteChangedCallback,
            updateStatusBarTextCallback,
            notifyMainWindowVMPropertyChangedCallback
        );

        _playbackService.PlaybackEndedNaturally += PlaybackService_PlaybackEndedNaturally;
    }

    private void PlaybackService_PlaybackEndedNaturally(object? sender, EventArgs e)
    {
        WorkflowManager.HandlePlaybackEndedNaturally();
    }

    public void Dispose()
    {
        if (_playbackService != null)
        {
            _playbackService.PlaybackEndedNaturally -= PlaybackService_PlaybackEndedNaturally;
        }
        ViewModelOrchestrator?.Dispose();
        WorkflowManager?.Dispose();
        Library?.Dispose();
        Playback?.Dispose();
        AdvancedPanel?.Dispose();
        LoopEditor?.Dispose();
        // LibraryDisplayModeService does not currently implement IDisposable
    }
}