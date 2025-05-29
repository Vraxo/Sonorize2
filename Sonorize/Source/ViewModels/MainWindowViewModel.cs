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
    private readonly ApplicationInteractionService _applicationInteractionService; // Added
    private readonly LibraryPlaybackLinkService _libraryPlaybackLinkService; // Added
    private readonly MainWindowViewModelCoordinator _viewModelCoordinator; // Added

    // Expose the Services directly for child VMs or public properties
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    // Expose the child ViewModels
    public LibraryViewModel Library { get; set; }
    public LoopEditorViewModel LoopEditor { get; }
    public PlaybackViewModel Playback { get; }
    public AdvancedPanelViewModel AdvancedPanel { get; } // New ViewModel for Advanced Panel
    public string StatusBarText { get => field; set => SetProperty(ref field, value); } = "Welcome to Sonorize!";

    // Property to control the selected tab index in the main TabControl
    public int ActiveTabIndex { get => field; set => SetProperty(ref field, value); } = 0;

    // IsLoadingLibrary is a proxy to Library's state
    public bool IsLoadingLibrary { get => Library.IsLoadingLibrary; }

    // Pass-through properties for view bindings to AdvancedPanelViewModel
    public bool IsAdvancedPanelVisible
    {
        get => AdvancedPanel.IsVisible;
        set
        {
            if (AdvancedPanel.IsVisible != value)
            {
                AdvancedPanel.IsVisible = value;
                // Notify that MainWindowViewModel's property changed, even if it's a pass-through
                OnPropertyChanged();
                // The AdvancedPanelViewModel's setter will handle its own OnPropertyChanged for its IsVisible
                // and trigger OnVisibilityChanged logic.
            }
        }
    }
    public ICommand ToggleAdvancedPanelCommand => AdvancedPanel.ToggleVisibilityCommand;


    // Top-level commands
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

        Library = new LibraryViewModel(this, _settingsService, _musicLibraryService, _loopDataService);
        Playback = new PlaybackViewModel(PlaybackService, _waveformService);
        LoopEditor = new LoopEditorViewModel(PlaybackService, _loopDataService);
        AdvancedPanel = new AdvancedPanelViewModel(Playback, Library); // Instantiate new VM

        _statusBarTextProvider = new StatusBarTextProvider(Playback, LoopEditor, Library);
        _settingsChangeProcessorService = new SettingsChangeProcessorService(Library, _scrobblingService);
        _playbackFlowManagerService = new PlaybackFlowManagerService(Library, Playback, PlaybackService, _nextTrackSelectorService);
        _applicationInteractionService = new ApplicationInteractionService(
            _settingsService,
            _settingsChangeProcessorService,
            CurrentTheme);
        _libraryPlaybackLinkService = new LibraryPlaybackLinkService(Library, PlaybackService, Playback);

        // Instantiate the coordinator to handle event subscriptions and subsequent logic
        _viewModelCoordinator = new MainWindowViewModelCoordinator(this, Library, Playback, AdvancedPanel, PlaybackService, _playbackFlowManagerService);

        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(), _ => !Library.IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialogAsync(owner), _ => !Library.IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefreshAsync(owner), _ => !Library.IsLoadingLibrary);

        UpdateAllUIDependentStates();
    }

    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(IsLoadingLibrary));
        OnPropertyChanged(nameof(Playback.CurrentSong));
        OnPropertyChanged(nameof(Playback.HasCurrentSong));
        OnPropertyChanged(nameof(IsAdvancedPanelVisible)); // Relies on AdvancedPanel.IsVisible
        OnPropertyChanged(nameof(ActiveTabIndex));

        UpdateStatusBarText();
        RaiseAllCommandsCanExecuteChanged();
    }

    public void RaiseAllCommandsCanExecuteChanged()
    {
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();

        Library.RaiseLibraryCommandsCanExecuteChanged();
        Playback.RaisePlaybackCommandCanExecuteChanged();
        LoopEditor.RaiseLoopCommandCanExecuteChanged();
        (AdvancedPanel.ToggleVisibilityCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }


    internal void UpdateStatusBarText() // Made internal for Coordinator access if needed, or keep public
    {
        StatusBarText = _statusBarTextProvider.GetCurrentStatusText();
    }

    private async Task OpenSettingsDialogAsync(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false; // Close advanced panel when opening settings

        var (statusMessages, settingsChanged) = await _applicationInteractionService.HandleOpenSettingsDialogAsync(owner);

        if (settingsChanged)
        {
            if (statusMessages.Any())
            {
                StatusBarText = string.Join(" | ", statusMessages);
            }
            else
            {
                UpdateStatusBarText();
            }
        }
        else
        {
            UpdateStatusBarText();
        }
    }

    private async Task AddMusicDirectoryAndRefreshAsync(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false; // Close advanced panel

        var (directoryAdded, statusMessage) = await _applicationInteractionService.HandleAddMusicDirectoryAsync(owner);

        StatusBarText = statusMessage;

        if (directoryAdded)
        {
            await Library.LoadLibraryAsync();
        }
        else
        {
            UpdateStatusBarText();
        }
    }

    protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
    }

    // Dispose the coordinator if MainWindowViewModel is disposable
    // For simplicity, assuming ViewModelBase handles IDisposable pattern if needed, or adding it here.
    // public void Dispose() { _viewModelCoordinator?.Dispose(); }
}