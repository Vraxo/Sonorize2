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

    // Expose the Services directly for child VMs or public properties
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    // Expose the child ViewModels
    public LibraryViewModel Library { get; set; }
    public LoopEditorViewModel LoopEditor { get; }
    public PlaybackViewModel Playback { get; }
    public string StatusBarText { get => field; set => SetProperty(ref field, value); } = "Welcome to Sonorize!";

    // Property to control the selected tab index in the main TabControl
    public int ActiveTabIndex { get => field; set => SetProperty(ref field, value); } = 0;


    // IsLoadingLibrary is a proxy to Library's state
    public bool IsLoadingLibrary { get => Library.IsLoadingLibrary; }

    public bool IsAdvancedPanelVisible { get => field; set { if (SetProperty(ref field, value)) OnAdvancedPanelVisibleChanged(); } }

    // Top-level commands
    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }
    public ICommand ToggleAdvancedPanelCommand { get; }

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
        _statusBarTextProvider = new StatusBarTextProvider(Playback, LoopEditor, Library);
        _settingsChangeProcessorService = new SettingsChangeProcessorService(Library, _scrobblingService);
        _playbackFlowManagerService = new PlaybackFlowManagerService(Library, Playback, PlaybackService, _nextTrackSelectorService);
        _applicationInteractionService = new ApplicationInteractionService(
            _settingsService,
            _settingsChangeProcessorService,
            CurrentTheme); // Pass dependencies to the new service


        Library.PropertyChanged += Library_PropertyChanged;
        Playback.PropertyChanged += Playback_PropertyChanged;

        PlaybackService.PlaybackEndedNaturally += PlaybackService_PlaybackEndedNaturally;

        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(), _ => !Library.IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialogAsync(owner), _ => !Library.IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefreshAsync(owner), _ => !Library.IsLoadingLibrary);

        ToggleAdvancedPanelCommand = new RelayCommand(
            _ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible,
            _ => Playback.HasCurrentSong && !Library.IsLoadingLibrary);


        UpdateAllUIDependentStates();
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
                    Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed to: {Library.SelectedSong?.Title ?? "null"}. Instance: {Library.SelectedSong?.GetHashCode() ?? 0}");

                    if (Library.SelectedSong != null && PlaybackService.CurrentSong != Library.SelectedSong)
                    {
                        Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed to a *different* song ({Library.SelectedSong.Title}) than PlaybackService.CurrentSong ({PlaybackService.CurrentSong?.Title ?? "null"}). Calling PlaybackService.Play().");
                        PlaybackService.Play(Library.SelectedSong);
                    }
                    else if (Library.SelectedSong != null && PlaybackService.CurrentSong == Library.SelectedSong)
                    {
                        Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed but is the SAME song instance as PlaybackService.CurrentSong ({Library.SelectedSong.Title}). Assuming RepeatOne handled it or user re-clicked already playing song. No Play call needed here.");
                    }
                    else if (Library.SelectedSong == null)
                    {
                        Debug.WriteLine("[MainVM_LibChanged] Library.SelectedSong is null. No Play call needed here. PlaybackService.Stop might have been called.");
                    }

                    RaiseAllCommandsCanExecuteChanged(); // Still relevant for commands MainWindowViewModel owns.
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

                    if (!Playback.HasCurrentSong && Library.SelectedSong != null)
                    {
                        Debug.WriteLine("[MainVM_PlaybackChanged] PlaybackService has no current song. Clearing Library selection.");
                        Library.SelectedSong = null;
                    }

                    if (Playback.CurrentSong != null && IsAdvancedPanelVisible)
                    {
                        Debug.WriteLine("[MainVM_PlaybackChanged] Playback has current song, advanced panel is visible. Requesting waveform load.");
                        _ = Playback.LoadWaveformForCurrentSongAsync();
                    }

                    UpdateStatusBarText();
                    OnPropertyChanged(nameof(Playback.CurrentTimeDisplay));
                    OnPropertyChanged(nameof(Playback.TotalTimeDisplay));
                    RaiseAllCommandsCanExecuteChanged();

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
                case nameof(PlaybackViewModel.IsWaveformLoading):
                    OnPropertyChanged(nameof(Playback.IsWaveformLoading));
                    break;
                case nameof(PlaybackViewModel.WaveformRenderData):
                    OnPropertyChanged(nameof(Playback.WaveformRenderData));
                    break;
                case nameof(PlaybackViewModel.ShuffleEnabled):
                case nameof(PlaybackViewModel.RepeatMode):
                    Playback.RaisePlaybackCommandCanExecuteChanged();
                    UpdateStatusBarText();
                    break;
            }
        });
    }

    private void OnAdvancedPanelVisibleChanged()
    {
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        if (IsAdvancedPanelVisible && Playback.HasCurrentSong && !Playback.WaveformRenderData.Any() && !Playback.IsWaveformLoading)
        {
            Debug.WriteLine("[MainVM] Advanced Panel visible, song is playing, waveform not loaded/loading. Requesting waveform load.");
            _ = Playback.LoadWaveformForCurrentSongAsync();
        }
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
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();

        Library.RaiseLibraryCommandsCanExecuteChanged(); // For LibraryVM's own commands (e.g., SetDisplayMode)
        // Navigation commands are handled by LibraryViewModel's TrackNavigationManager internally.
        // No longer need: Library.RaiseNavigationCommandsCanExecuteChanged(); 
        Playback.RaisePlaybackCommandCanExecuteChanged();
        LoopEditor.RaiseLoopCommandCanExecuteChanged();
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

        if (settingsChanged) // Check if settings were actually processed (dialog might have been cancelled)
        {
            if (statusMessages.Any())
            {
                StatusBarText = string.Join(" | ", statusMessages);
            }
            else
            {
                UpdateStatusBarText(); // Fallback to default status if no specific messages
            }
        }
        else
        {
            UpdateStatusBarText(); // Ensure status text is current even if no changes
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
            await Library.LoadLibraryAsync(); // Library.LoadLibraryAsync will update its own status text
        }
        else
        {
            UpdateStatusBarText(); // If no directory added, ensure status bar reflects current state or message from service
        }
    }
}