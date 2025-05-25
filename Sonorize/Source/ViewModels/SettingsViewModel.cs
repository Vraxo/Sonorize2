using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sonorize.Services;

namespace Sonorize.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;

    public ObservableCollection<string> MusicDirectories { get; } = new();
    public List<string> InitialMusicDirectories { get; private set; } // To track changes
    
    public string? SelectedDirectory
    {
        get;

        set => SetProperty(ref field, value, nameof(CanRemoveDirectory));
    }

    public ObservableCollection<string> AvailableThemes { get; } = new();

    public string? SelectedThemeFile
    {
        get;
        
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            SettingsChanged = true;
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
        if (owner?.StorageProvider == null)
        {
            // Handle cases where StorageProvider is not available (e.g., headless environments or older platforms)
            Debug.WriteLine("StorageProvider is not available.");
            // Potentially show an error message to the user
            return;
        }

        var options = new FolderPickerOpenOptions
        {
            Title = "Select Music Directory",
            AllowMultiple = false
        };

        var result = await owner.StorageProvider.OpenFolderPickerAsync(options);

        if (result != null && result.Count > 0)
        {
            var folder = result.FirstOrDefault();
            if (folder == null)
            {
                return;
            }
            // Access the path using Path.LocalPath
            var path = folder.Path.LocalPath;
            if (string.IsNullOrEmpty(path) || MusicDirectories.Contains(path))
            {
                return;
                // SettingsChanged will be set by the CollectionChanged event handler
            }
            MusicDirectories.Add(path);
        }
    }

    private void RemoveSelectedDirectory(object? parameter)
    {
        if (SelectedDirectory == null)
        {
            return;
            // SettingsChanged will be set by the CollectionChanged event handler
        }
        MusicDirectories.Remove(SelectedDirectory);
        SelectedDirectory = null;
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
