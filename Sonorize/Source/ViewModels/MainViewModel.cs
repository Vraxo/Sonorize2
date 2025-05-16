using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sonorize.Models;
using Sonorize.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System;
using System.Diagnostics; // For Debug.WriteLine
using Avalonia.Threading; // For Dispatcher

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    public PlaybackService PlaybackService { get; }

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
                // Potentially disable UI elements that shouldn't be used during loading
                (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }


    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand PlaySongCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }


    public MainWindowViewModel(SettingsService settingsService, MusicLibraryService musicLibraryService, PlaybackService playbackService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;

        LoadInitialDataCommand = new RelayCommand(async _ => await LoadMusicLibrary());
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        PlaySongCommand = new RelayCommand(song => PlaybackService.Play((Song)song!), song => song is Song);
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !IsLoadingLibrary);

        PlaybackService.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(PlaybackService.CurrentSong) || args.PropertyName == nameof(PlaybackService.IsPlaying))
            {
                UpdateStatusBarTextPlayingStatus(); // Renamed for clarity
            }
        };
    }

    private void UpdateStatusBarTextPlayingStatus()
    {
        if (IsLoadingLibrary) return; // Don't overwrite loading status

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
        if (IsLoadingLibrary) return;
        IsLoadingLibrary = true;

        var settings = _settingsService.LoadSettings();
        Songs.Clear(); // Clear existing songs before reload
        StatusBarText = "Preparing to load music library...";

        if (settings.MusicDirectories.Any())
        {
            await Task.Run(async () => // Ensure the MusicLibraryService call itself is on a background thread
            {
                try
                {
                    await _musicLibraryService.LoadMusicFromDirectoriesAsync(
                        settings.MusicDirectories,
                        song => Songs.Add(song), // Action<Song> songAddedCallback (already on UI thread from service)
                        status => StatusBarText = status // Action<string> statusUpdateCallback (already on UI thread)
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error during LoadMusicFromDirectoriesAsync: {ex}");
                    await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "Error loading library.");
                }
            });

            StatusBarText = $"{Songs.Count} songs loaded. Ready.";
            if (!Songs.Any() && settings.MusicDirectories.Any())
                StatusBarText = "No songs found in specified directories. Add directories via File > Settings.";
        }
        else
        {
            StatusBarText = "No music directories configured. Add directories via File > Settings.";
        }
        IsLoadingLibrary = false;
        UpdateStatusBarTextPlayingStatus(); // Refresh status bar based on playback after loading
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner) return;

        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow
        {
            DataContext = settingsVM
        };

        await settingsDialog.ShowDialog(owner);

        if (settingsVM.SettingsChanged)
        {
            await LoadMusicLibrary(); // This will now clear and reload incrementally
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner) return;

        var dialog = new OpenFolderDialog { Title = "Select Music Directory" };
        var result = await dialog.ShowAsync(owner);

        if (result != null && !string.IsNullOrEmpty(result))
        {
            var settings = _settingsService.LoadSettings();
            if (!settings.MusicDirectories.Contains(result))
            {
                settings.MusicDirectories.Add(result);
                _settingsService.SaveSettings(settings);
                await LoadMusicLibrary();
            }
        }
    }
}