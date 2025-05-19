using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sonorize.Models;
using Sonorize.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Diagnostics;
using System.Collections.Generic;
using System; // Required for List<string>

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
        // Use Avalonia's StorageProvider for modern file/folder picking
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Music Directory",
            AllowMultiple = false
        });

        if (result != null && result.Count > 0)
        {
            // Get the path from the first selected folder
            // Try LocalPath first, then fall back to parsing the URI if needed
            string? folderPath = result[0].Path.LocalPath;

            // Fallback for systems/providers where LocalPath might be null or incorrect
            if (string.IsNullOrEmpty(folderPath) && result[0].Path.IsAbsoluteUri)
            {
                try
                {
                    // Attempt to convert the URI to a local path string
                    folderPath = new Uri(result[0].Path.ToString()).LocalPath;
                }
                catch (Exception ex)
                {
                    // Log any errors during URI conversion
                    Debug.WriteLine($"[SettingsVM] Error converting folder URI to local path: {result[0].Path}. Exception: {ex.Message}");
                    folderPath = null; // Ensure folderPath is null if conversion fails
                }
            }

            if (!string.IsNullOrEmpty(folderPath))
            {
                Debug.WriteLine($"[SettingsVM] Selected directory via StorageProvider: {folderPath}");
                // Check if already exists before adding
                if (!MusicDirectories.Contains(folderPath))
                {
                    MusicDirectories.Add(folderPath);
                    // SettingsChanged will be set by the CollectionChanged event handler
                }
                else
                {
                    Debug.WriteLine($"[SettingsVM] Directory '{folderPath}' already in list. Not adding.");
                    // Optionally, provide user feedback (e.g., via a status message in the settings window)
                }
            }
            else
            {
                Debug.WriteLine($"[SettingsVM] Could not get a valid path from the selected folder.");
                // Optionally, provide user feedback
            }
        }
        else
        {
            Debug.WriteLine("[SettingsVM] Folder picker dialog cancelled or returned no result.");
        }
    }


    private void RemoveSelectedDirectory(object? parameter)
    {
        if (SelectedDirectory != null)
        {
            MusicDirectories.Remove(SelectedDirectory);
            SelectedDirectory = null; // Clear selection after removing
            // SettingsChanged will be set by the CollectionChanged event handler
        }
    }

    private void SaveSettings(object? parameter)
    {
        // Load current settings to preserve other settings if any (e.g., future settings)
        var currentSettings = _settingsService.LoadSettings();

        // Check if directories actually changed from their initial state when the dialog opened
        bool dirsActuallyChanged = !InitialMusicDirectories.OrderBy(d => d).SequenceEqual(MusicDirectories.OrderBy(d => d)); // Compare sorted lists

        // Check if the selected theme file name actually changed
        bool themeActuallyChanged = currentSettings.PreferredThemeFileName != SelectedThemeFile;

        // Update settings object only if there were actual changes
        if (dirsActuallyChanged)
        {
            currentSettings.MusicDirectories = MusicDirectories.ToList();
            Debug.WriteLine($"[SettingsVM] Directories changed. Saving {currentSettings.MusicDirectories.Count} directories.");
        }

        if (themeActuallyChanged)
        {
            currentSettings.PreferredThemeFileName = SelectedThemeFile;
            Debug.WriteLine($"[SettingsVM] Preferred theme changed. Saving: {SelectedThemeFile}");
        }

        // Save the settings object back to the file only if any changes occurred
        if (dirsActuallyChanged || themeActuallyChanged)
        {
            _settingsService.SaveSettings(currentSettings);
            // Keep SettingsChanged true to signal the main window that something relevant happened
            SettingsChanged = true;

            // Update InitialMusicDirectories to reflect the saved state for subsequent comparisons
            if (dirsActuallyChanged)
            {
                InitialMusicDirectories = new List<string>(currentSettings.MusicDirectories);
            }
        }
        else
        {
            // If no directories or theme changed, but the property `SettingsChanged` was true
            // due to intermediate ObservableCollection changes, explicitly set it back to false
            // because no critical settings requiring reload/restart were saved.
            SettingsChanged = false;
            Debug.WriteLine("[SettingsVM] No changes to directories or theme detected. Not saving.");
        }

        // The dialog window will be closed by the SaveButton_Click handler in SettingsWindow.axaml.cs
    }
}
