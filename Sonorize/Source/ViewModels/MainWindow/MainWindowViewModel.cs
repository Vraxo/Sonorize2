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
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    private readonly ScrobblingService _scrobblingService;
    private readonly SongMetadataService _songMetadataService;
    private readonly SongEditInteractionService _songEditInteractionService;
    private readonly SongLoopService _songLoopService; // New service

    private readonly MainWindowViewModelOrchestrator _orchestrator;
    private readonly ApplicationWorkflowManager _workflowManager;
    private readonly LibraryDisplayModeService _libraryDisplayModeService;
    private readonly MainWindowInteractionCoordinator _interactionCoordinator;

    private Window? _ownerView;


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
                OnPropertyChanged();
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
        SongLoopService songLoopService) // Added songLoopService
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;
        _scrobblingService = scrobblingService;
        _songMetadataService = songMetadataService;
        _songEditInteractionService = songEditInteractionService;
        _songLoopService = songLoopService; // Store new service

        _libraryDisplayModeService = new LibraryDisplayModeService(_settingsService);
        Library = new LibraryViewModel(this, _settingsService, _musicLibraryService, _loopDataService, _libraryDisplayModeService);
        Playback = new PlaybackViewModel(PlaybackService, _waveformService);
        LoopEditor = new LoopEditorViewModel(PlaybackService, _loopDataService, _songLoopService); // Pass new service
        AdvancedPanel = new AdvancedPanelViewModel(Playback, Library);

        _workflowManager = new ApplicationWorkflowManager(
            _settingsService,
            _scrobblingService,
            CurrentTheme,
            Library,
            Playback,
            PlaybackService,
            _loopDataService);

        _interactionCoordinator = new MainWindowInteractionCoordinator(
            () => _ownerView,
            Library,
            AdvancedPanel,
            _workflowManager,
            _songEditInteractionService,
            RaiseAllCommandsCanExecuteChanged
        );

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
        OpenSettingsCommand = new RelayCommand(async _ => await OpenSettingsDialogAsync(), _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async _ => await AddMusicDirectoryAndRefreshAsync(), _ => !Library.IsLoadingLibrary && (Playback.WaveformDisplay == null || !Playback.WaveformDisplay.IsWaveformLoading));
        OpenEditSongMetadataDialogCommand = new RelayCommand(async song => await HandleOpenEditSongMetadataDialogAsync(song), CanOpenEditSongMetadataDialog);


        Dispatcher.UIThread.InvokeAsync(UpdateAllUIDependentStates);
    }

    public void SetOwnerView(Window ownerView)
    {
        _ownerView = ownerView;
    }

    private void PlaybackService_PlaybackEndedNaturally(object? sender, EventArgs e)
    {
        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally event received. Delegating to WorkflowManager.");
        _workflowManager.HandlePlaybackEndedNaturally();
        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally handler completed after delegation.");
    }

    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(IsLoadingLibrary));
        OnPropertyChanged(nameof(Playback.CurrentSong));
        OnPropertyChanged(nameof(Playback.HasCurrentSong));
        OnPropertyChanged(nameof(Playback.CurrentPlaybackStatus));
        OnPropertyChanged(nameof(Playback.IsPlaying));
        OnPropertyChanged(nameof(Playback.CurrentTimeDisplay));
        OnPropertyChanged(nameof(Playback.TotalTimeDisplay));
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


            Library.RaiseLibraryCommandsCanExecuteChanged();
            Playback.RaisePlaybackCommandCanExecuteChanged();
            LoopEditor.RaiseMainLoopCommandsCanExecuteChanged();
            LoopEditor.CandidateLoop.RaiseCaptureCommandsCanExecuteChanged();
            (AdvancedPanel.ToggleVisibilityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        });
    }

    private void UpdateStatusBarText()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusBarText = _workflowManager.GetCurrentStatusText(LoopEditor);
        });
    }

    private async Task OpenSettingsDialogAsync()
    {
        string statusMessage = await _interactionCoordinator.CoordinateOpenSettingsDialogAsync();
        if (!string.IsNullOrEmpty(statusMessage))
        {
            StatusBarText = statusMessage;
        }
        else
        {
            UpdateStatusBarText();
        }
    }

    private async Task AddMusicDirectoryAndRefreshAsync()
    {
        var (refreshNeeded, statusMessage) = await _interactionCoordinator.CoordinateAddMusicDirectoryAsync();
        StatusBarText = statusMessage;

        if (refreshNeeded)
        {
            await Library.LoadLibraryAsync();
        }
        else if (string.IsNullOrEmpty(statusMessage))
        {
            UpdateStatusBarText();
        }
    }

    private async Task HandleOpenEditSongMetadataDialogAsync(object? songObject)
    {
        string statusMessage = await _interactionCoordinator.CoordinateEditSongMetadataAsync(songObject as Song);
        StatusBarText = statusMessage;
        if (string.IsNullOrEmpty(statusMessage))
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
        if (PlaybackService != null)
        {
            PlaybackService.PlaybackEndedNaturally -= PlaybackService_PlaybackEndedNaturally;
        }
        _orchestrator?.Dispose();
        _workflowManager?.Dispose();
        Library?.Dispose();
        Playback?.Dispose();
        AdvancedPanel?.Dispose();
        LoopEditor?.Dispose();
        _ownerView = null;
    }
}