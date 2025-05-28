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

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly ApplicationOrchestrator _orchestrator;

    // Expose the child ViewModels via the orchestrator
    public LibraryViewModel Library => _orchestrator.Library;
    public PlaybackViewModel Playback => _orchestrator.Playback;
    public LoopEditorViewModel LoopEditor => _orchestrator.LoopEditor;
    public AdvancedPanelViewModel AdvancedPanel => _orchestrator.AdvancedPanel;
    public PlaybackService PlaybackService => _orchestrator.PlaybackService;
    public ThemeColors CurrentTheme => _orchestrator.CurrentTheme;


    private string _statusBarText = "Welcome to Sonorize!";
    public string StatusBarText { get => _statusBarText; set => SetProperty(ref _statusBarText, value); }

    private int _activeTabIndex = 0;
    public int ActiveTabIndex { get => _activeTabIndex; set => SetProperty(ref _activeTabIndex, value); }


    // IsLoadingLibrary is a proxy to Library's state
    public bool IsLoadingLibrary => Library.IsLoadingLibrary;

    // Pass-through properties for view bindings to AdvancedPanelViewModel
    public bool IsAdvancedPanelVisible
    {
        get => AdvancedPanel.IsVisible;
        set
        {
            if (AdvancedPanel.IsVisible != value)
            {
                AdvancedPanel.IsVisible = value;
                // OnPropertyChanged(); // ApplicationOrchestrator will call RaiseInternalPropertyChanged
            }
        }
    }
    public ICommand ToggleAdvancedPanelCommand => AdvancedPanel.ToggleVisibilityCommand;


    // Top-level commands
    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService,
        LoopDataService loopDataService,
        ScrobblingService scrobblingService)
    {
        var nextTrackSelectorService = new NextTrackSelectorService(new Random());

        _orchestrator = new ApplicationOrchestrator(
            this,
            settingsService,
            musicLibraryService,
            playbackService,
            theme,
            waveformService,
            loopDataService,
            scrobblingService,
            nextTrackSelectorService);

        LoadInitialDataCommand = new RelayCommand(async _ => await _orchestrator.LoadInitialLibraryDataAsync(), _ => !Library.IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialogAsync(owner), _ => !Library.IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefreshAsync(owner), _ => !Library.IsLoadingLibrary);

        _orchestrator.InitializeUIDependentStates();
    }

    internal void RaiseInternalPropertyChanged(string propertyName)
    {
        OnPropertyChanged(propertyName);
    }

    // Called by Orchestrator when PlaybackViewModel properties change
    internal void HandlePlaybackViewModelChange(string? propertyName)
    {
        // This method is called by ApplicationOrchestrator when a property on PlaybackViewModel changes.
        // MainWindowViewModel might need to:
        // 1. Update its own properties that depend on PlaybackViewModel's state (if any).
        // 2. Raise CanExecuteChanged for its commands.
        // UI bindings to PlaybackViewModel's properties (e.g., Playback.CurrentSong) are handled by PlaybackViewModel's INPC.

        switch (propertyName)
        {
            case nameof(PlaybackViewModel.CurrentSong):
            case nameof(PlaybackViewModel.HasCurrentSong):
            case nameof(PlaybackViewModel.CurrentPlaybackStatus):
            case nameof(PlaybackViewModel.IsPlaying):
            case nameof(PlaybackViewModel.CurrentSongDuration):
            case nameof(PlaybackViewModel.CurrentSongDurationSeconds):
                RaiseAllCommandsCanExecuteChanged();
                break;
                // Other properties of PlaybackViewModel usually don't affect MainWindowViewModel's command states directly,
                // but if they did, they would be handled here or RaiseAllCommandsCanExecuteChanged would be called more broadly.
        }
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

    private async Task OpenSettingsDialogAsync(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;

        // Logic to set IsAdvancedPanelVisible to false before opening settings
        // This might involve direct property set or via a command if preferred.
        if (IsAdvancedPanelVisible) // Check current state
        {
            IsAdvancedPanelVisible = false; // Directly set the property; orchestrator will be notified if setter logic is kept
        }


        var (statusMessages, settingsChanged) = await _orchestrator.ApplicationInteraction.HandleOpenSettingsDialogAsync(owner);

        if (settingsChanged)
        {
            if (statusMessages.Any())
            {
                StatusBarText = string.Join(" | ", statusMessages);
            }
            else
            {
                _orchestrator.UpdateStatusBarText();
            }
        }
        else
        {
            _orchestrator.UpdateStatusBarText();
        }
    }

    private async Task AddMusicDirectoryAndRefreshAsync(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;

        if (IsAdvancedPanelVisible)
        {
            IsAdvancedPanelVisible = false;
        }

        var (directoryAdded, statusMessage) = await _orchestrator.ApplicationInteraction.HandleAddMusicDirectoryAsync(owner);

        StatusBarText = statusMessage;

        if (directoryAdded)
        {
            await Library.LoadLibraryAsync();
        }
        else
        {
            _orchestrator.UpdateStatusBarText();
        }
    }

    public void Dispose()
    {
        _orchestrator.Dispose();
    }
}