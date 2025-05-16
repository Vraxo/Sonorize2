using Avalonia.Controls; // For OpenFolderDialog, Window
using Avalonia.Platform.Storage; // For IStorageFolder (though ShowAsync returns string now)
using Sonorize.Models;    // For Song, AppSettings, ThemeColors
using Sonorize.Services;  // For SettingsService, MusicLibraryService, PlaybackService
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System; // Required for Action
using System.Diagnostics; // For Debug.WriteLine
using Avalonia.Threading; // For Dispatcher

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; } // To make theme accessible to UI if needed for dynamic bindings (less common in C#-only UI)

    public ObservableCollection<Song> Songs { get; } = new();

    private Song? _selectedSong;
    public Song? SelectedSong
    {
        get => _selectedSong;
        set
        {
            if (SetProperty(ref _selectedSong, value) && value != null)
            {
                PlaySongCommand.Execute(value);
            }
        }
    }

    private string _statusBarText = "Welcome to Sonorize!";
    public string StatusBarText
    {
        get => _statusBarText;
        set => SetProperty(ref _statusBarText, value);
    }

    private bool _isLoadingLibrary = false;
    public bool IsLoadingLibrary
    {
        get => _isLoadingLibrary;
        set
        {
            if (SetProperty(ref _isLoadingLibrary, value))
            {
                // Update CanExecute for commands that should be disabled during loading
                (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged(); // If you add a manual refresh button
            }
        }
    }

    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand PlaySongCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme) // ThemeColors passed in
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme; // Store the passed theme

        LoadInitialDataCommand = new RelayCommand(async _ => await LoadMusicLibrary(), _ => !IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        PlaySongCommand = new RelayCommand(song => PlaybackService.Play((Song)song!), song => song is Song);
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !IsLoadingLibrary);

        PlaybackService.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(PlaybackService.CurrentSong) || args.PropertyName == nameof(PlaybackService.IsPlaying))
            {
                UpdateStatusBarTextPlayingStatus();
            }
        };
    }

    private void UpdateStatusBarTextPlayingStatus()
    {
        if (IsLoadingLibrary) return; // Loading status takes precedence

        if (PlaybackService.IsPlaying && PlaybackService.CurrentSong != null)
        {
            StatusBarText = $"Playing: {PlaybackService.CurrentSong.Title} - {PlaybackService.CurrentSong.Artist}";
        }
        else if (PlaybackService.CurrentSong != null)
        {
            StatusBarText = $"Paused: {PlaybackService.CurrentSong.Title} - {PlaybackService.CurrentSong.Artist}";
        }
        else
        {
            StatusBarText = $"Sonorize - {Songs.Count} songs loaded.";
        }
    }

    private async Task LoadMusicLibrary()
    {
        if (IsLoadingLibrary)
        {
            Debug.WriteLine("[MainVM] LoadMusicLibrary called while already loading. Skipping.");
            return;
        }
        IsLoadingLibrary = true;

        var settings = _settingsService.LoadSettings();

        // Clear songs on the UI thread before starting background work
        await Dispatcher.UIThread.InvokeAsync(() => {
            Songs.Clear();
            StatusBarText = "Preparing to load music library...";
        });


        if (settings.MusicDirectories.Any())
        {
            Debug.WriteLine($"[MainVM] Starting music library load from {settings.MusicDirectories.Count} directories.");
            // Run the potentially long-running service call on a background thread
            await Task.Run(async () =>
            {
                try
                {
                    await _musicLibraryService.LoadMusicFromDirectoriesAsync(
                        settings.MusicDirectories,
                        song => Songs.Add(song), // songAddedCallback is already marshalled to UI thread by the service
                        status => StatusBarText = status  // statusUpdateCallback is also marshalled
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MainVM] Error during LoadMusicFromDirectoriesAsync background task: {ex}");
                    await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "Error loading library.");
                }
            });

            // Final status update after the Task.Run completes
            await Dispatcher.UIThread.InvokeAsync(() => {
                StatusBarText = $"{Songs.Count} songs loaded. Ready.";
                if (!Songs.Any() && settings.MusicDirectories.Any())
                    StatusBarText = "No songs found in specified directories. Add directories via File > Settings.";
            });
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                StatusBarText = "No music directories configured. Add directories via File > Settings.");
        }
        IsLoadingLibrary = false;
        UpdateStatusBarTextPlayingStatus(); // Refresh status bar based on playback after loading finishes
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner) return;

        var settingsVM = new SettingsViewModel(_settingsService);
        // Pass the CurrentTheme to the SettingsWindow constructor
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme)
        {
            DataContext = settingsVM
        };

        await settingsDialog.ShowDialog(owner);

        if (settingsVM.SettingsChanged)
        {
            await LoadMusicLibrary();
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner) return;

        var dialog = new OpenFolderDialog { Title = "Select Music Directory" };
        // For Avalonia 11, ShowAsync returns string directly.
        // For earlier versions, it returned IStorageFolder. This code assumes Avalonia 11+.
        var resultPath = await dialog.ShowAsync(owner);

        if (!string.IsNullOrEmpty(resultPath))
        {
            var settings = _settingsService.LoadSettings();
            if (!settings.MusicDirectories.Contains(resultPath))
            {
                settings.MusicDirectories.Add(resultPath);
                _settingsService.SaveSettings(settings);
                await LoadMusicLibrary();
            }
        }
    }
}