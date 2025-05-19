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
    private readonly ThemeService _themeService;

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

    // This flag indicates if any setting that requires action (like reload or restart) has changed.
    public bool SettingsChanged { get; private set; } = false;

    public ICommand AddDirectoryCommand { get; }
    public ICommand RemoveDirectoryCommand { get; }
    public ICommand SaveAndCloseCommand { get; }

    public bool CanRemoveDirectory => SelectedDirectory != null;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _themeService = new ThemeService(null); // Create a temporary instance to list files

        var settings = _settingsService.LoadSettings();
        InitialMusicDirectories = new List<string>(settings.MusicDirectories); // Store initial state

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
            SelectedThemeFile = AvailableThemes.First(); // Fallback if saved theme not found
        }

        // Reset SettingsChanged after initial load, as SelectedThemeFile property set might trigger it
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
        // Any change to MusicDirectories collection or SelectedThemeFile marks settings as changed.
        MusicDirectories.CollectionChanged += (s, e) => SettingsChanged = true;
    }

    private async Task AddDirectory(Window? owner)
    {
        if (owner == null) return;
        var dialog = new OpenFolderDialog() { Title = "Select Music Directory" };
        var result = await dialog.ShowAsync(owner);
        if (result != null && !string.IsNullOrEmpty(result))
        {
            if (!MusicDirectories.Contains(result))
            {
                MusicDirectories.Add(result);
                // SettingsChanged will be set by the CollectionChanged event handler
            }
        }
    }

    private void RemoveSelectedDirectory(object? parameter)
    {
        if (SelectedDirectory != null)
        {
            MusicDirectories.Remove(SelectedDirectory);
            SelectedDirectory = null;
            // SettingsChanged will be set by the CollectionChanged event handler
        }
    }

    private void SaveSettings(object? parameter)
    {
        var currentSettings = _settingsService.LoadSettings(); // Load current to preserve other settings if any

        // Check if directories actually changed before saving and marking
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
            SettingsChanged = true; // Ensure this is true if anything was actually saved
                                    // Update InitialMusicDirectories to reflect the saved state for subsequent checks if dialog is reopened
            if (dirsActuallyChanged) InitialMusicDirectories = new List<string>(currentSettings.MusicDirectories);
        }
        else
        {
            SettingsChanged = false; // No actual changes were made that need saving.
        }
    }
}