using System;
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
    private readonly StatusBarTextProvider _statusBarTextProvider; // Added

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
        _statusBarTextProvider = new StatusBarTextProvider(Playback, LoopEditor, Library); // Instantiated


        Library.PropertyChanged += Library_PropertyChanged;
        Playback.PropertyChanged += Playback_PropertyChanged;

        PlaybackService.PlaybackEndedNaturally += PlaybackService_PlaybackEndedNaturally;

        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(), _ => !Library.IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !Library.IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !Library.IsLoadingLibrary);

        ToggleAdvancedPanelCommand = new RelayCommand(
            _ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible,
            _ => Playback.HasCurrentSong && !Library.IsLoadingLibrary);


        UpdateAllUIDependentStates();
    }

    private void PlaybackService_PlaybackEndedNaturally(object? sender, EventArgs e)
    {
        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally event received.");

        var currentSong = Library.SelectedSong;
        var currentList = Library.FilteredSongs.ToList();
        var repeatMode = Playback.RepeatMode;
        var shuffleEnabled = Playback.ShuffleEnabled;

        Song? nextSong = _nextTrackSelectorService.GetNextSong(currentSong, currentList, repeatMode, shuffleEnabled);

        if (nextSong != null)
        {
            Debug.WriteLine($"[MainVM] Next song determined by NextTrackSelectorService: {nextSong.Title}. Setting Library.SelectedSong.");
            Library.SelectedSong = nextSong;
        }
        else
        {
            Debug.WriteLine("[MainVM] No next song determined by NextTrackSelectorService. Calling PlaybackService.Stop().");
            PlaybackService.Stop();
        }

        Debug.WriteLine("[MainVM] PlaybackService_PlaybackEndedNaturally handler completed.");
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

    private async Task LoadMusicLibrary()
    {
        await Library.LoadLibraryAsync();
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;

        var currentSettingsBeforeDialog = _settingsService.LoadSettings();
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };

        await settingsDialog.ShowDialog(owner);

        if (settingsVM.SettingsChanged)
        {
            Debug.WriteLine("[MainVM] Settings changed detected after dialog closed.");
            var newSettingsAfterDialog = _settingsService.LoadSettings();
            bool dirsActuallyChanged = !currentSettingsBeforeDialog.MusicDirectories.SequenceEqual(newSettingsAfterDialog.MusicDirectories);
            bool themeActuallyChanged = currentSettingsBeforeDialog.PreferredThemeFileName != newSettingsAfterDialog.PreferredThemeFileName;
            bool scrobbleSettingsActuallyChanged =
                currentSettingsBeforeDialog.LastfmScrobblingEnabled != newSettingsAfterDialog.LastfmScrobblingEnabled ||
                currentSettingsBeforeDialog.LastfmUsername != newSettingsAfterDialog.LastfmUsername ||
                currentSettingsBeforeDialog.LastfmPassword != newSettingsAfterDialog.LastfmPassword;


            if (dirsActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Music directories changed. Reloading library.");
                await Library.LoadLibraryAsync();
            }

            if (themeActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Theme changed. Restart recommended.");
                StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect.";
            }

            if (scrobbleSettingsActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Scrobbling settings changed. Refreshing ScrobblingService.");
                _scrobblingService.RefreshSettings();
                // Optionally, update status bar if scrobbling got enabled/disabled
                if (newSettingsAfterDialog.LastfmScrobblingEnabled && !string.IsNullOrEmpty(newSettingsAfterDialog.LastfmUsername))
                {
                    if (StatusBarText.Contains("restart Sonorize")) {/* Append if needed */} else StatusBarText = "Scrobbling enabled.";
                }
                else if (!newSettingsAfterDialog.LastfmScrobblingEnabled && currentSettingsBeforeDialog.LastfmScrobblingEnabled)
                {
                    if (StatusBarText.Contains("restart Sonorize")) {/* Append if needed */} else StatusBarText = "Scrobbling disabled.";
                }
            }

            if (!themeActuallyChanged && !dirsActuallyChanged && !scrobbleSettingsActuallyChanged)
            {
                UpdateStatusBarText();
            }
        }
        else
        {
            Debug.WriteLine("[MainVM] Settings dialog closed, no changes reported by SettingsViewModel.");
            UpdateStatusBarText();
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;

        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Music Directory", AllowMultiple = false });

        if (result != null && result.Count > 0)
        {
            string? folderPath = null;
            try
            {
                folderPath = Path.GetFullPath(result[0].Path.LocalPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainVM] Error getting full path for selected directory: {ex.Message}");
                StatusBarText = "Error getting path for selected directory.";
                return;
            }

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                var settings = _settingsService.LoadSettings();
                if (!settings.MusicDirectories.Any(d => string.Equals(d, folderPath, StringComparison.OrdinalIgnoreCase)))
                {
                    settings.MusicDirectories.Add(folderPath);
                    _settingsService.SaveSettings(settings);
                    Debug.WriteLine($"[MainVM] Added new directory: {folderPath}. Reloading library.");
                    await Library.LoadLibraryAsync();
                }
                else
                {
                    Debug.WriteLine($"[MainVM] Directory already exists: {folderPath}");
                    StatusBarText = "Directory already in library.";
                }
            }
            else
            {
                Debug.WriteLine($"[MainVM] Selected directory path is invalid or does not exist: {folderPath}");
                StatusBarText = "Invalid directory selected.";
            }
        }
        else
        {
            Debug.WriteLine("[MainVM] Folder picker cancelled or returned no results.");
            UpdateStatusBarText();
        }
    }
}