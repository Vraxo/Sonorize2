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
    private readonly LibraryDisplayModeService _libraryDisplayModeService; // Added service instance

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

        // Instantiate the new service
        _libraryDisplayModeService = new LibraryDisplayModeService(_settingsService);

        // Pass the new service to LibraryViewModel
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


        Library.PropertyChanged += Library_PropertyChanged;
        Playback.PropertyChanged += Playback_PropertyChanged;
        Playback.ModeControls.PropertyChanged += PlaybackModeControls_PropertyChanged;

        if (Playback.WaveformDisplay != null)
        {
            Playback.WaveformDisplay.PropertyChanged += PlaybackWaveformDisplay_PropertyChanged;
        }
        AdvancedPanel.PropertyChanged += AdvancedPanel_PropertyChanged;

        PlaybackService.PlaybackEndedNaturally += PlaybackService_PlaybackEndedNaturally;

        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(), _ => !Library.IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialogAsync(owner), _ => !Library.IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefreshAsync(owner), _ => !Library.IsLoadingLibrary);

        UpdateAllUIDependentStates();
    }

    private void PlaybackWaveformDisplay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WaveformDisplayViewModel.IsWaveformLoading))
        {
            RaiseAllCommandsCanExecuteChanged();
        }
    }


    private void AdvancedPanel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdvancedPanelViewModel.IsVisible))
        {
            OnPropertyChanged(nameof(IsAdvancedPanelVisible));
            RaiseAllCommandsCanExecuteChanged();
        }
    }

    private void PlaybackService_PlaybackEndedNaturally(object? sender, EventArgs e)
    {
        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally event received. Delegating to PlaybackFlowManagerService.");
        _playbackFlowManagerService.HandlePlaybackEndedNaturally();
        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally handler completed after delegation.");
    }

    private void Library_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(Library.SelectedSong):
                    RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(Library.IsLoadingLibrary):
                    OnPropertyChanged(nameof(IsLoadingLibrary));
                    RaiseAllCommandsCanExecuteChanged();
                    UpdateStatusBarText();
                    break;
                case nameof(Library.LibraryStatusText):
                    UpdateStatusBarText();
                    break;
            }
        });
    }

    private void Playback_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PlaybackViewModel.CurrentSong):
                    OnPropertyChanged(nameof(Playback.CurrentSong));
                    OnPropertyChanged(nameof(Playback.HasCurrentSong));
                    RaiseAllCommandsCanExecuteChanged();
                    UpdateStatusBarText();
                    OnPropertyChanged(nameof(Playback.CurrentTimeDisplay));
                    OnPropertyChanged(nameof(Playback.TotalTimeDisplay));
                    break;
                case nameof(PlaybackViewModel.CurrentPlaybackStatus):
                    OnPropertyChanged(nameof(Playback.CurrentPlaybackStatus));
                    OnPropertyChanged(nameof(Playback.IsPlaying));
                    UpdateStatusBarText();
                    RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(PlaybackViewModel.CurrentPosition):
                    OnPropertyChanged(nameof(Playback.CurrentPosition));
                    OnPropertyChanged(nameof(Playback.CurrentPositionSeconds));
                    OnPropertyChanged(nameof(Playback.CurrentTimeDisplay));
                    break;
                case nameof(PlaybackViewModel.CurrentSongDuration):
                    OnPropertyChanged(nameof(Playback.CurrentSongDuration));
                    OnPropertyChanged(nameof(Playback.CurrentSongDurationSeconds));
                    OnPropertyChanged(nameof(Playback.TotalTimeDisplay));
                    RaiseAllCommandsCanExecuteChanged();
                    break;
            }
        });
    }

    private void PlaybackModeControls_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PlaybackModeViewModel.ShuffleEnabled):
                case nameof(PlaybackModeViewModel.RepeatMode):
                    Playback.RaisePlaybackCommandCanExecuteChanged();
                    UpdateStatusBarText();
                    break;
            }
        });
    }


    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(IsLoadingLibrary));
        OnPropertyChanged(nameof(Playback.CurrentSong));
        OnPropertyChanged(nameof(Playback.HasCurrentSong));
        OnPropertyChanged(nameof(IsAdvancedPanelVisible));
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


    private void UpdateStatusBarText()
    {
        StatusBarText = _statusBarTextProvider.GetCurrentStatusText();
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
        IsAdvancedPanelVisible = false;

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
}