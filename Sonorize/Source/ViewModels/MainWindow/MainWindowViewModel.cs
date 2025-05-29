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

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    private readonly ScrobblingService _scrobblingService;
    private readonly SongMetadataService _songMetadataService;

    private readonly MainWindowViewModelOrchestrator _orchestrator;
    private readonly ApplicationWorkflowManager _workflowManager;
    private readonly LibraryDisplayModeService _libraryDisplayModeService;

    private Window? _ownerView; // Added to store the owner window


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
        SongMetadataService songMetadataService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;
        _scrobblingService = scrobblingService;
        _songMetadataService = songMetadataService;

        _libraryDisplayModeService = new LibraryDisplayModeService(_settingsService);
        Library = new LibraryViewModel(this, _settingsService, _musicLibraryService, _loopDataService, _libraryDisplayModeService);
        Playback = new PlaybackViewModel(PlaybackService, _waveformService);
        LoopEditor = new LoopEditorViewModel(PlaybackService, _loopDataService);
        AdvancedPanel = new AdvancedPanelViewModel(Playback, Library);

        _workflowManager = new ApplicationWorkflowManager(
            _settingsService,
            _scrobblingService,
            CurrentTheme,
            Library,
            Playback,
            PlaybackService,
            _loopDataService,
            _songMetadataService);

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
        OpenEditSongMetadataDialogCommand = new RelayCommand(async song => await OpenEditSongMetadataDialogAsync(song), CanOpenEditSongMetadataDialog);


        Dispatcher.UIThread.InvokeAsync(UpdateAllUIDependentStates);
    }

    // Method for the View to set itself as the owner
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

    private async Task OpenSettingsDialogAsync(object? ownerWindowObj)
    {
        if (ownerWindowObj is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;

        var (statusMessages, settingsChanged) = await _workflowManager.HandleOpenSettingsDialogAsync(owner);

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
        RaiseAllCommandsCanExecuteChanged();
    }

    private async Task AddMusicDirectoryAndRefreshAsync(object? ownerWindowObj)
    {
        if (ownerWindowObj is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;

        var (directoryAdded, statusMessage) = await _workflowManager.HandleAddMusicDirectoryAsync(owner);

        StatusBarText = statusMessage;

        if (directoryAdded)
        {
            await Library.LoadLibraryAsync();
        }
        else
        {
            UpdateStatusBarText();
        }
        RaiseAllCommandsCanExecuteChanged();
    }

    private async Task OpenEditSongMetadataDialogAsync(object? songObject)
    {
        if (songObject is not Song songToEdit)
        {
            Debug.WriteLine("[MainVM] OpenEditSongMetadataDialogAsync: songObject is not a Song.");
            return;
        }

        Window? ownerWindow = _ownerView; // Use the stored owner view

        if (ownerWindow == null)
        {
            Debug.WriteLine("[MainVM] OpenEditSongMetadataDialogAsync: Owner window is not set.");
            return;
        }

        IsAdvancedPanelVisible = false;

        bool metadataSaved = await _workflowManager.HandleEditSongMetadataDialogAsync(songToEdit, ownerWindow);

        if (metadataSaved)
        {
            StatusBarText = $"Metadata for '{songToEdit.Title}' updated.";
        }
        else
        {
            UpdateStatusBarText();
        }
        RaiseAllCommandsCanExecuteChanged();
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
        _ownerView = null; // Clear the view reference
    }
}