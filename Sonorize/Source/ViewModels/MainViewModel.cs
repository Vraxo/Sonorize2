using Avalonia.Controls; // For OpenFolderDialog
using Avalonia.Platform.Storage; // For IStorageFolder
using Sonorize.Models;
using Sonorize.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System; // Required for Action

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
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner));
        ExitCommand = new RelayCommand(_ => Environment.Exit(0)); // Or Application.Current.Shutdown() if more complex cleanup
        PlaySongCommand = new RelayCommand(song => PlaybackService.Play((Song)song!), song => song is Song);
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner));

        // Subscribe to PlaybackService property changes to update status bar or other UI elements
        PlaybackService.PropertyChanged += (sender, args) =>
        {
            if (args.PropertyName == nameof(PlaybackService.CurrentSong) || args.PropertyName == nameof(PlaybackService.IsPlaying))
            {
                UpdateStatusBarText();
            }
        };
    }

    private void UpdateStatusBarText()
    {
        if (PlaybackService.IsPlaying && PlaybackService.CurrentSong != null)
        {
            StatusBarText = $"Playing: {PlaybackService.CurrentSong.Title} - {PlaybackService.CurrentSong.Artist}";
        }
        else if (PlaybackService.CurrentSong != null) // Paused or stopped but a song is loaded
        {
            StatusBarText = $"Paused: {PlaybackService.CurrentSong.Title} - {PlaybackService.CurrentSong.Artist}";
        }
        else
        {
            StatusBarText = "Sonorize - Ready";
        }
    }


    private async Task LoadMusicLibrary()
    {
        var settings = _settingsService.LoadSettings();
        Songs.Clear();
        if (settings.MusicDirectories.Any())
        {
            StatusBarText = "Loading music library...";
            var songs = await _musicLibraryService.LoadMusicFromDirectoriesAsync(settings.MusicDirectories);
            foreach (var song in songs)
            {
                Songs.Add(song);
            }
            StatusBarText = $"{Songs.Count} songs loaded.";
            if (!Songs.Any()) StatusBarText = "No songs found in specified directories. Add directories via File > Settings.";
        }
        else
        {
            StatusBarText = "No music directories configured. Add directories via File > Settings.";
        }
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

        if (settingsVM.SettingsChanged) // A flag you might set in SettingsViewModel upon saving
        {
            await LoadMusicLibrary();
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner) return;

        var dialog = new OpenFolderDialog { Title = "Select Music Directory" };
        var result = await dialog.ShowAsync(owner);

        if (result != null && !string.IsNullOrEmpty(result)) // Avalonia 11 returns string path
        {
            var settings = _settingsService.LoadSettings();
            if (!settings.MusicDirectories.Contains(result))
            {
                settings.MusicDirectories.Add(result);
                _settingsService.SaveSettings(settings);
                await LoadMusicLibrary(); // Refresh music library
            }
        }
    }
}