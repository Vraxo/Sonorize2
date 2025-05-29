using System;
using System.Collections.Generic; // Added for List<string>
using System.ComponentModel;
using System.Diagnostics;
using System.IO; // Required for Path.GetFullPath, Directory.Exists
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.Status; // Added for StatusBarTextProvider
using Sonorize.ViewModels.LibraryManagement; // Required for LibraryDisplayModeService

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    private readonly ScrobblingService _scrobblingService;
    private readonly NextTrackSelectorService _nextTrackSelectorService;
    private readonly StatusBarTextProvider _statusBarTextProvider;
    private readonly SettingsChangeProcessorService _settingsChangeProcessorService;
    private readonly PlaybackFlowManagerService _playbackFlowManagerService;
    private readonly ApplicationInteractionService _applicationInteractionService;
    private readonly LibraryPlaybackLinkService _libraryPlaybackLinkService;
    private readonly LibraryDisplayModeService _libraryDisplayModeService;
    private readonly MainWindowViewModelOrchestrator _orchestrator; // New orchestrator

    // Expose the Services directly for child VMs or public properties
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    // Expose the child ViewModels
    public LibraryViewModel Library { get; set; }
    public LoopEditorViewModel LoopEditor { get; }
    public PlaybackViewModel Playback { get; }
    public AdvancedPanelViewModel AdvancedPanel { get; }
    public string StatusBarText { get => field; set => SetProperty(ref field, value); } = "Welcome to Sonorize!";

    public int ActiveTabIndex { get => field; set => SetProperty(ref field, value); } = 0;

    public bool IsLoadingLibrary { get => Library.IsLoadingLibrary; }

    public bool IsAdvancedPanelVisible
    {
        get => AdvancedPanel.IsVisible;
        set
        {
            if (AdvancedPanel.IsVisible != value)
            {
                AdvancedPanel.IsVisible = value;
                OnPropertyChanged(); // PropertyChanged handled by orchestrator if it sets this
            }
        }
    }
    public ICommand ToggleAdvancedPanelCommand => AdvancedPanel.ToggleVisibilityCommand;


    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }


    private readonly Random _shuffleRandom = new();

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService,
        LoopDataService loopDataService,
        ScrobblingService scrobblingService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;
        _scrobblingService = scrobblingService;
        _nextTrackSelectorService = new NextTrackSelectorService(_shuffleRandom);

        _libraryDisplayModeService = new LibraryDisplayModeService(_settingsService);
        Library = new LibraryViewModel(this, _settingsService, _musicLibraryService, _loopDataService, _libraryDisplayModeService);
        Playback = new PlaybackViewModel(PlaybackService, _waveformService);
        LoopEditor = new LoopEditorViewModel(PlaybackService, _loopDataService);
        AdvancedPanel = new AdvancedPanelViewModel(Playback, Library);

        _statusBarTextProvider = new StatusBarTextProvider(Playback, LoopEditor, Library);
        _settingsChangeProcessorService = new SettingsChangeProcessorService(Library, _scrobblingService);
        _playbackFlowManagerService = new PlaybackFlowManagerService(Library, Playback, PlaybackService, _nextTrackSelectorService);
        _applicationInteractionService = new ApplicationInteractionService(
            _settingsService,
            _settingsChangeProcessorService,
            CurrentTheme);
        _libraryPlaybackLinkService = new LibraryPlaybackLinkService(Library, PlaybackService, Playback);

        // Instantiate and set up the orchestrator
        _orchestrator = new MainWindowViewModelOrchestrator(
            Library,
            Playback,
            AdvancedPanel,
            RaiseAllCommandsCanExecuteChanged,
            UpdateStatusBarText,
            (propertyName) => OnPropertyChanged(propertyName)
        );

        PlaybackService.PlaybackEndedNaturally += PlaybackService_PlaybackEndedNaturally;

        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(), _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialogAsync(owner), _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefreshAsync(owner), _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));

        // Initial UI state update
        Dispatcher.UIThread.InvokeAsync(UpdateAllUIDependentStates);
    }

    private void PlaybackService_PlaybackEndedNaturally(object? sender, EventArgs e)
    {
        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally event received. Delegating to PlaybackFlowManagerService.");
        _playbackFlowManagerService.HandlePlaybackEndedNaturally();
        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally handler completed after delegation.");
    }

    // Removed individual PropertyChanged handlers; logic is now in _orchestrator

    private void UpdateAllUIDependentStates()
    {
        // These ensure that the UI reflects the initial state correctly,
        // especially for properties managed or affected by the orchestrator.
        OnPropertyChanged(nameof(IsLoadingLibrary));
        OnPropertyChanged(nameof(Playback.CurrentSong)); // Trigger updates for all Playback related derived properties
        OnPropertyChanged(nameof(Playback.HasCurrentSong));
        OnPropertyChanged(nameof(Playback.CurrentPlaybackStatus));
        OnPropertyChanged(nameof(Playback.IsPlaying));
        OnPropertyChanged(nameof(Playback.CurrentTimeDisplay));
        OnPropertyChanged(nameof(Playback.TotalTimeDisplay));
        OnPropertyChanged(nameof(IsAdvancedPanelVisible));
        OnPropertyChanged(nameof(ActiveTabIndex));

        UpdateStatusBarText(); // Initial status bar text
        RaiseAllCommandsCanExecuteChanged(); // Initial command states
    }

    public void RaiseAllCommandsCanExecuteChanged()
    {
        Dispatcher.UIThread.InvokeAsync(() => // Ensure runs on UI thread
        {
            (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();

            Library.RaiseLibraryCommandsCanExecuteChanged();
            Playback.RaisePlaybackCommandCanExecuteChanged();
            LoopEditor.RaiseMainLoopCommandsCanExecuteChanged(); // Corrected method name
            LoopEditor.CandidateLoop.RaiseCaptureCommandsCanExecuteChanged(); // Ensure candidate commands are also updated
            (AdvancedPanel.ToggleVisibilityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        });
    }

    private void UpdateStatusBarText()
    {
        Dispatcher.UIThread.InvokeAsync(() => // Ensure runs on UI thread
        {
            StatusBarText = _statusBarTextProvider.GetCurrentStatusText();
        });
    }

    private async Task OpenSettingsDialogAsync(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;

        var (statusMessages, settingsChanged) = await _applicationInteractionService.HandleOpenSettingsDialogAsync(owner);

        if (settingsChanged)
        {
            if (statusMessages.Any())
            {
                StatusBarText = string.Join(" | ", statusMessages);
            }
            else
            {
                UpdateStatusBarText(); // Fallback to default status text logic
            }
        }
        else
        {
            UpdateStatusBarText(); // Update status if no changes or no specific messages
        }
        // Re-evaluate commands as settings changes might affect them (e.g., scrobbling related UI)
        RaiseAllCommandsCanExecuteChanged();
    }

    private async Task AddMusicDirectoryAndRefreshAsync(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;

        var (directoryAdded, statusMessage) = await _applicationInteractionService.HandleAddMusicDirectoryAsync(owner);

        StatusBarText = statusMessage; // Display immediate feedback

        if (directoryAdded)
        {
            await Library.LoadLibraryAsync(); // This will update status text further during/after loading
        }
        else
        {
            UpdateStatusBarText(); // Revert to standard status text if dir not added
        }
        RaiseAllCommandsCanExecuteChanged();
    }

    public void Dispose() // Ensure orchestrator is disposed
    {
        if (PlaybackService != null)
        {
            PlaybackService.PlaybackEndedNaturally -= PlaybackService_PlaybackEndedNaturally;
        }
        _orchestrator?.Dispose();
        _libraryPlaybackLinkService?.Dispose();
        Library?.Dispose();
        Playback?.Dispose();
        AdvancedPanel?.Dispose();
        LoopEditor?.Dispose(); // Dispose LoopEditorViewModel
    }
}