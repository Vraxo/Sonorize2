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

    private readonly Random _shuffleRandom = new();

    public ApplicationWorkflowManager(
        SettingsService settingsService,
        ScrobblingService scrobblingService,
        ThemeColors currentTheme,
        LibraryViewModel libraryViewModel,
        PlaybackViewModel playbackViewModel,
        PlaybackService playbackService,
        LoopDataService loopDataService) // LoopDataService needed by LoopEditor, indirectly for status
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        _currentTheme = currentTheme ?? throw new ArgumentNullException(nameof(currentTheme));
        _libraryViewModel = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));
        _playbackViewModel = playbackViewModel ?? throw new ArgumentNullException(nameof(playbackViewModel));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));

        // Create internal services
        _nextTrackSelectorService = new NextTrackSelectorService(_shuffleRandom);

        // Assuming LoopEditorViewModel is accessible or its relevant state for status bar is handled differently
        // For StatusBarTextProvider, we might need LoopEditorViewModel or a subset of its state.
        // For simplicity here, if LoopEditor is a direct child of MainWindowViewModel, it could be passed too.
        // Or StatusBarTextProvider could be simplified if LoopEditor state isn't strictly needed or handled via PlaybackViewModel.
        // Let's assume MainWindowViewModel will pass its LoopEditorViewModel instance to GetCurrentStatusText.
        // For now, StatusBarTextProvider is created here.
        // If LoopEditorViewModel is critical, it should be passed in constructor.
        // Let's pass LoopEditorViewModel's required data if possible, or the VM itself.
        // For now, creating it with what's available.
        // LoopEditorViewModel is created in MainWindowViewModel, so we can't easily pass it here without a circular setup.
        // Solution: MainWindowViewModel passes its instance of LoopEditorViewModel to GetCurrentStatusText.
        _statusBarTextProvider = new StatusBarTextProvider(_playbackViewModel, null!, _libraryViewModel); // Placeholder for LoopEditorViewModel

        _settingsChangeProcessorService = new SettingsChangeProcessorService(_libraryViewModel, _scrobblingService);
        _playbackFlowManagerService = new PlaybackFlowManagerService(_libraryViewModel, _playbackViewModel, _playbackService, _nextTrackSelectorService);

        _applicationInteractionService = new ApplicationInteractionService(
            _settingsService,
            _settingsChangeProcessorService,
            _currentTheme);

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

    public void HandlePlaybackEndedNaturally()
    {
        _playbackFlowManagerService.HandlePlaybackEndedNaturally();
    }

    public string GetCurrentStatusText(LoopEditorViewModel loopEditorViewModel) // Accept LoopEditorViewModel here
    {
        // Temporarily create a new StatusBarTextProvider if we can't store LoopEditorViewModel
        // This is not ideal. Better to have StatusBarTextProvider take LoopEditorViewModel in its constructor.
        // For this refactor, we'll assume the existing StatusBarTextProvider in MainWindowViewModel is used,
        // and this method would reconstruct the string or MainWindowViewModel calls its own provider.
        // To make this class fully responsible, it needs LoopEditorViewModel.
        // Let's refine _statusBarTextProvider initialization or GetCurrentStatusText method.
        // A simple way is for this method to reconstruct the provider instance or update it.
        var localStatusBarTextProvider = new StatusBarTextProvider(_playbackViewModel, loopEditorViewModel, _libraryViewModel);
        return localStatusBarTextProvider.GetCurrentStatusText();
    }

    public void Dispose()
    {
        _libraryPlaybackLinkService?.Dispose();
    }
}