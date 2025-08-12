using System;
using System.ComponentModel;
using Avalonia.Controls;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.LibraryManagement; // Required for LibraryDisplayModeService

namespace Sonorize.ViewModels.MainWindow;

public class MainWindowComponentsManager : IDisposable
{
    // Keep direct service references if they are passed to multiple components
    // or if MainWindowViewModel needs them directly (though preferably through properties here)
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    // PlaybackService is now a property for MainWindowViewModel to expose
    public PlaybackService PlaybackServiceProperty { get; }
    private readonly ThemeColors _currentTheme;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    public ScrobblingService ScrobblingServiceProperty { get; } // Exposed for MainWindowViewModel
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
        PlaybackService playbackService, // Renamed for clarity from _playbackService
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
        PlaybackServiceProperty = playbackService; // Store the passed service
        _currentTheme = currentTheme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;
        ScrobblingServiceProperty = scrobblingService; // Store the passed service
        _songMetadataService = songMetadataService;
        _songEditInteractionService = songEditInteractionService;
        _songLoopService = songLoopService;

        LibraryDisplayModeService = new LibraryDisplayModeService(_settingsService);
        // Pass parentMainWindowViewModel to LibraryViewModel as per its constructor
        Library = new LibraryViewModel(parentMainWindowViewModel, _settingsService, _musicLibraryService, _loopDataService, LibraryDisplayModeService);
        Playback = new PlaybackViewModel(PlaybackServiceProperty, _waveformService); // Use the stored PlaybackService
        LoopEditor = new LoopEditorViewModel(PlaybackServiceProperty, _loopDataService, _songLoopService); // Use stored PlaybackService
        AdvancedPanel = new AdvancedPanelViewModel(Playback, Library);

        WorkflowManager = new ApplicationWorkflowManager(
            _settingsService,
            ScrobblingServiceProperty, // Use stored ScrobblingService
            _currentTheme,
            Library,
            Playback,
            PlaybackServiceProperty, // Use stored PlaybackService
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

        // Subscribe to PlaybackServiceProperty events here, as this manager owns the WorkflowManager that handles it.
        PlaybackServiceProperty.PlaybackEndedNaturally += PlaybackService_PlaybackEndedNaturally;
    }

    private void PlaybackService_PlaybackEndedNaturally(object? sender, EventArgs e)
    {
        // WorkflowManager is instantiated and owned by this ComponentsManager
        WorkflowManager.HandlePlaybackEndedNaturally();
    }

    public void Dispose()
    {
        if (PlaybackServiceProperty != null)
        {
            PlaybackServiceProperty.PlaybackEndedNaturally -= PlaybackService_PlaybackEndedNaturally;
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
