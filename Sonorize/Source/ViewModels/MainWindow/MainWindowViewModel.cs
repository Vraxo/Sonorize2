using System;
using System.Collections.Generic; // Added for List<string>
using System.ComponentModel;
using System.Diagnostics;
using System.IO; // Required for Path.GetFullPath, Directory.Exists
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
// Removed: using Avalonia.Platform.Storage; // No longer directly used here for Application.Current
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
// Removed: using Sonorize.ViewModels.Status; // StatusBarTextProvider usage moved
using Sonorize.ViewModels.LibraryManagement; // Required for LibraryDisplayModeService
using Sonorize.ViewModels.MainWindow; // Added for MainWindowInteractionCoordinator

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly MainWindowComponentsManager _componentsManager;
    private Window? _ownerView;

    // Expose Services needed by views or bindings if not through child VMs
    public PlaybackService PlaybackService => _componentsManager.PlaybackServiceProperty; // Exposed from ComponentsManager
    public ThemeColors CurrentTheme { get; }

    // Expose the child ViewModels from ComponentsManager
    public LibraryViewModel Library => _componentsManager.Library;
    public LoopEditorViewModel LoopEditor => _componentsManager.LoopEditor;
    public PlaybackViewModel Playback => _componentsManager.Playback;
    public AdvancedPanelViewModel AdvancedPanel => _componentsManager.AdvancedPanel;

    public string StatusBarText { get => field; set => SetProperty(ref field, value); } = "Welcome to Sonorize!";
    public int ActiveTabIndex { get => field; set => SetProperty(ref field, value); } = 0;

    public bool IsLoadingLibrary => Library.IsLoadingLibrary;

    public bool IsAdvancedPanelVisible
    {
        get => AdvancedPanel.IsVisible;
        set
        {
            if (AdvancedPanel.IsVisible != value)
            {
                AdvancedPanel.IsVisible = value;
                OnPropertyChanged(); // AdvancedPanel will also raise its own, this is for MainWindowViewModel bindings
            }
        }
    }
    public ICommand ToggleAdvancedPanelCommand => AdvancedPanel.ToggleVisibilityCommand;

    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }
    public ICommand OpenEditSongMetadataDialogCommand { get; }

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService,
        LoopDataService loopDataService,
        ScrobblingService scrobblingService,
        SongMetadataService songMetadataService,
        SongEditInteractionService songEditInteractionService,
        SongLoopService songLoopService)
    {
        CurrentTheme = theme; // Store theme directly

        _componentsManager = new MainWindowComponentsManager(
            this, // Pass self as parent
            settingsService,
            musicLibraryService,
            playbackService, // Pass the service instance
            theme,
            waveformService,
            loopDataService,
            scrobblingService,
            songMetadataService,
            songEditInteractionService,
            songLoopService,
            () => _ownerView,
            RaiseAllCommandsCanExecuteChanged,
            UpdateStatusBarText,
            (propertyName) => OnPropertyChanged(propertyName)
        );

        // Commands that interact with components managed by _componentsManager
        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(),
            _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));
        OpenSettingsCommand = new RelayCommand(async _ => await OpenSettingsDialogAsync(),
            _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));

        // Correctly handle exit by closing the window, which triggers the disposal chain.
        ExitCommand = new RelayCommand(_ => _ownerView?.Close(), _ => _ownerView is not null);

        AddDirectoryAndRefreshCommand = new RelayCommand(async _ => await AddMusicDirectoryAndRefreshAsync(),
            _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));
        OpenEditSongMetadataDialogCommand = new RelayCommand(async song => await HandleOpenEditSongMetadataDialogAsync(song), CanOpenEditSongMetadataDialog);

        Dispatcher.UIThread.InvokeAsync(UpdateAllUIDependentStates);
    }

    public void SetOwnerView(Window ownerView)
    {
        _ownerView = ownerView;
        // Re-evaluate CanExecute for commands that depend on the owner view.
        (ExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    // PlaybackService_PlaybackEndedNaturally is now handled within MainWindowComponentsManager via its WorkflowManager

    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(IsLoadingLibrary));
        // Properties of child VMs (Playback.CurrentSong etc.) will notify through their own INPC.
        // MainWindowViewModel itself doesn't need to raise OnPropertyChanged for them unless it has direct proxy properties.
        // However, if bindings are to "Playback.CurrentSong" directly from MainWindowViewModel's XAML, they will work.
        // Let's ensure relevant top-level states that might affect commands are refreshed.
        OnPropertyChanged(nameof(IsAdvancedPanelVisible));
        OnPropertyChanged(nameof(ActiveTabIndex));

        UpdateStatusBarText();
        RaiseAllCommandsCanExecuteChanged();
    }

    public void RaiseAllCommandsCanExecuteChanged()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (OpenEditSongMetadataDialogCommand as RelayCommand)?.RaiseCanExecuteChanged();

            // Child VMs manage their own command executability updates based on their state.
            // If MainWindowViewModel needs to explicitly trigger updates in child VMs' commands:
            Library.RaiseLibraryCommandsCanExecuteChanged();
            Playback.RaisePlaybackCommandCanExecuteChanged();
            LoopEditor.RaiseMainLoopCommandsCanExecuteChanged();
            // AdvancedPanel's ToggleVisibilityCommand CanExecute will be updated by AdvancedPanelViewModel itself.
        });
    }

    private void UpdateStatusBarText()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // WorkflowManager is now accessed via _componentsManager
            StatusBarText = _componentsManager.WorkflowManager.GetCurrentStatusText(LoopEditor);
        });
    }

    private async Task OpenSettingsDialogAsync()
    {
        // InteractionCoordinator is accessed via _componentsManager
        string statusMessage = await _componentsManager.InteractionCoordinator.CoordinateOpenSettingsDialogAsync();
        if (!string.IsNullOrEmpty(statusMessage))
        {
            StatusBarText = statusMessage;
        }
        else
        {
            UpdateStatusBarText(); // Update with default status if no specific message
        }
    }

    private async Task AddMusicDirectoryAndRefreshAsync()
    {
        // InteractionCoordinator is accessed via _componentsManager
        var (refreshNeeded, statusMessage) = await _componentsManager.InteractionCoordinator.CoordinateAddMusicDirectoryAsync();
        StatusBarText = statusMessage;

        if (refreshNeeded)
        {
            await Library.LoadLibraryAsync(); // Library is from _componentsManager
        }
        else if (string.IsNullOrEmpty(statusMessage)) // If no specific message (e.g. "already exists")
        {
            UpdateStatusBarText();
        }
    }

    private async Task HandleOpenEditSongMetadataDialogAsync(object? songObject)
    {
        // InteractionCoordinator is accessed via _componentsManager
        string statusMessage = await _componentsManager.InteractionCoordinator.CoordinateEditSongMetadataAsync(songObject as Song);
        StatusBarText = statusMessage;
        if (string.IsNullOrEmpty(statusMessage)) // Ensure status bar updates if no message
        {
            UpdateStatusBarText();
        }
    }

    private bool CanOpenEditSongMetadataDialog(object? songObject)
    {
        return songObject is Song && !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading);
    }

    public void Dispose()
    {
        _componentsManager?.Dispose();
        _ownerView = null;
    }
}
