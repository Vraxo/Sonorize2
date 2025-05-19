using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sonorize.Models;
using Sonorize.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Diagnostics;
using System.Collections.Generic; // Required for List<string>

namespace Sonorize.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService; // For GetAvailableThemeFiles

    public ThemeColors CurrentTheme { get; } // Added to hold the theme

    public ObservableCollection<string> MusicDirectories { get; } = new();
    public List<string> InitialMusicDirectories { get; private set; } // To track changes

    private string? _selectedDirectory;
    public string? SelectedDirectory
    {
        get => _selectedDirectory;
        set => SetProperty(ref _selectedDirectory, value, nameof(CanRemoveDirectory));
    }

    public ObservableCollection<string> AvailableThemes { get; } = new();
    private string? _selectedThemeFile;
    public string? SelectedThemeFile
    {
        get => _selectedThemeFile;
        set
        {
            if (SetProperty(ref _selectedThemeFile, value))
            {
                SettingsChanged = true;
            }
        }
    }

    public bool SettingsChanged { get; private set; } = false;

    public ICommand AddDirectoryCommand { get; }
    public ICommand RemoveDirectoryCommand { get; }
    public ICommand SaveAndCloseCommand { get; }

    public bool CanRemoveDirectory => SelectedDirectory != null;

    public SettingsViewModel(SettingsService settingsService, ThemeColors currentTheme) // Modified constructor
    {
        _settingsService = settingsService;
        CurrentTheme = currentTheme; // Store the passed theme
        _themeService = new ThemeService(null); // Temporary instance to list files, doesn't use CurrentTheme for this purpose

        var settings = _settingsService.LoadSettings();
        InitialMusicDirectories = new List<string>(settings.MusicDirectories);

        foreach (var dir in settings.MusicDirectories)
        {
            MusicDirectories.Add(dir);
        }

        foreach (var themeFile in _themeService.GetAvailableThemeFiles())
        {
            AvailableThemes.Add(themeFile);
        }

        SelectedThemeFile = settings.PreferredThemeFileName ?? ThemeService.DefaultThemeFileName;
        if (!AvailableThemes.Contains(SelectedThemeFile) && AvailableThemes.Any())
        {
            SelectedThemeFile = AvailableThemes.First();
        }

        SettingsChanged = false;

        AddDirectoryCommand = new RelayCommand(async owner => await AddDirectory(owner as Window));
        RemoveDirectoryCommand = new RelayCommand(RemoveSelectedDirectory, _ => CanRemoveDirectory);
        SaveAndCloseCommand = new RelayCommand(SaveSettings);

        PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SelectedDirectory))
            {
                (RemoveDirectoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        };
        MusicDirectories.CollectionChanged += (s, e) => SettingsChanged = true;
    }

    private async Task AddDirectory(Window? owner)
    {
        if (owner == null) return;
        // In Avalonia 11+, use StorageProvider
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel == null) return;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Music Directory",
            AllowMultiple = false
        });

        if (result.Count > 0)
        {
            string? folderPath = result[0].TryGetLocalPath();
            if (!string.IsNullOrEmpty(folderPath) && !MusicDirectories.Contains(folderPath))
            {
                MusicDirectories.Add(folderPath);
            }
        }
    }

    private void RemoveSelectedDirectory(object? parameter)
    {
        if (SelectedDirectory != null)
        {
            MusicDirectories.Remove(SelectedDirectory);
            SelectedDirectory = null;
        }
    }

    private void SaveSettings(object? parameter)
    {
        var currentSettings = _settingsService.LoadSettings();

        bool dirsActuallyChanged = !InitialMusicDirectories.SequenceEqual(MusicDirectories);
        bool themeActuallyChanged = currentSettings.PreferredThemeFileName != SelectedThemeFile;

        if (dirsActuallyChanged)
        {
            currentSettings.MusicDirectories = MusicDirectories.ToList();
            Debug.WriteLine($"[SettingsVM] Saved directories count: {currentSettings.MusicDirectories.Count}");
        }

        if (themeActuallyChanged)
        {
            currentSettings.PreferredThemeFileName = SelectedThemeFile;
            Debug.WriteLine($"[SettingsVM] Saved theme: {SelectedThemeFile}");
        }

        if (dirsActuallyChanged || themeActuallyChanged)
        {
            _settingsService.SaveSettings(currentSettings);
            SettingsChanged = true;
            if (dirsActuallyChanged) InitialMusicDirectories = new List<string>(currentSettings.MusicDirectories);
        }
        else
        {
            SettingsChanged = false;
        }
    }
}