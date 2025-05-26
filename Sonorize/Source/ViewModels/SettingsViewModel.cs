using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sonorize.Models; // Required for AppSettings type
using Sonorize.Services;

namespace Sonorize.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;

    public ObservableCollection<string> MusicDirectories { get; } = new();
    public List<string> InitialMusicDirectories { get; private set; }

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
            MarkSettingsChanged();
        }
    }

    // Encapsulated Last.fm Settings
    public LastfmSettingsViewModel LastfmSettings { get; }


    public bool SettingsChanged { get; private set; } = false;

    public ICommand AddDirectoryCommand { get; }
    public ICommand RemoveDirectoryCommand { get; }
    public ICommand SaveAndCloseCommand { get; }

    public bool CanRemoveDirectory => SelectedDirectory != null;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _themeService = new ThemeService(null); // ThemeService used for GetAvailableThemeFiles

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

        // Initialize Last.fm settings module
        LastfmSettings = new LastfmSettingsViewModel();
        LastfmSettings.LoadFromSettings(settings);
        LastfmSettings.PropertyChanged += (s, e) => MarkSettingsChanged();


        SettingsChanged = false; // Initial state is unchanged

        AddDirectoryCommand = new RelayCommand(async owner => await AddDirectory(owner as Window));
        RemoveDirectoryCommand = new RelayCommand(RemoveSelectedDirectory, _ => CanRemoveDirectory);
        SaveAndCloseCommand = new RelayCommand(SaveSettings);

        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SelectedDirectory))
            {
                (RemoveDirectoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            // Changes to SelectedThemeFile already call MarkSettingsChanged in their setter
        };
        MusicDirectories.CollectionChanged += (s, e) => MarkSettingsChanged();
    }

    private void MarkSettingsChanged()
    {
        if (!SettingsChanged)
        {
            SettingsChanged = true;
            Debug.WriteLine("[SettingsVM] Settings marked as changed.");
        }
    }


    private async Task AddDirectory(Window? owner)
    {
        if (owner?.StorageProvider == null)
        {
            Debug.WriteLine("StorageProvider is not available.");
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
            if (folder == null) return;

            var path = folder.Path.LocalPath;
            if (string.IsNullOrEmpty(path) || MusicDirectories.Contains(path)) return;

            MusicDirectories.Add(path);
            // MarkSettingsChanged() is called by CollectionChanged handler
        }
    }

    private void RemoveSelectedDirectory(object? parameter)
    {
        if (SelectedDirectory == null) return;

        MusicDirectories.Remove(SelectedDirectory);
        SelectedDirectory = null;
        // MarkSettingsChanged() is called by CollectionChanged handler
    }

    private void SaveSettings(object? parameter)
    {
        // Load the settings as they are currently on disk to compare against
        AppSettings settingsOnDisk = _settingsService.LoadSettings();
        // Create a new AppSettings object or clone to store the new state
        AppSettings newSettingsToSave = new AppSettings
        {
            // Copy non-UI managed settings or settings that are not part of this VM directly
            LibraryViewModePreference = settingsOnDisk.LibraryViewModePreference,
            ArtistViewModePreference = settingsOnDisk.ArtistViewModePreference,
            AlbumViewModePreference = settingsOnDisk.AlbumViewModePreference,
            LastfmSessionKey = settingsOnDisk.LastfmSessionKey // Preserve session key
        };

        bool actualChangesMade = false;

        // Music Directories
        if (!InitialMusicDirectories.SequenceEqual(MusicDirectories) || // If they were initially different
            !settingsOnDisk.MusicDirectories.SequenceEqual(MusicDirectories)) // Or different from current disk state
        {
            newSettingsToSave.MusicDirectories = new List<string>(MusicDirectories);
            InitialMusicDirectories = new List<string>(MusicDirectories); // Update initial state for next open
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsVM] Saved directories count: {newSettingsToSave.MusicDirectories.Count}");
        }
        else
        {
            newSettingsToSave.MusicDirectories = new List<string>(settingsOnDisk.MusicDirectories);
        }

        // Theme
        if (settingsOnDisk.PreferredThemeFileName != SelectedThemeFile)
        {
            newSettingsToSave.PreferredThemeFileName = SelectedThemeFile;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsVM] Saved theme: {SelectedThemeFile}");
        }
        else
        {
            newSettingsToSave.PreferredThemeFileName = settingsOnDisk.PreferredThemeFileName;
        }

        // Last.fm Settings - Compare with disk state and apply LastfmSettings VM state
        if (settingsOnDisk.LastfmScrobblingEnabled != LastfmSettings.LastfmScrobblingEnabled) { actualChangesMade = true; }
        if (settingsOnDisk.LastfmUsername != LastfmSettings.LastfmUsername) { actualChangesMade = true; }
        if (settingsOnDisk.LastfmPassword != LastfmSettings.LastfmPassword && !string.IsNullOrEmpty(LastfmSettings.LastfmPassword)) { actualChangesMade = true; }
        if (settingsOnDisk.ScrobbleThresholdPercentage != LastfmSettings.ScrobbleThresholdPercentage) { actualChangesMade = true; }
        if (settingsOnDisk.ScrobbleThresholdAbsoluteSeconds != LastfmSettings.ScrobbleThresholdAbsoluteSeconds) { actualChangesMade = true; }

        // Apply changes from LastfmSettingsViewModel to newSettingsToSave
        LastfmSettings.UpdateAppSettings(newSettingsToSave);
        if (actualChangesMade)
        {
            Debug.WriteLine($"[SettingsVM] Last.fm settings updated: " +
                           $"Scrobbling={newSettingsToSave.LastfmScrobblingEnabled}, " +
                           $"User={newSettingsToSave.LastfmUsername}, " +
                           $"PassLen={(newSettingsToSave.LastfmPassword?.Length ?? 0)}, " +
                           $"Thresh%={newSettingsToSave.ScrobbleThresholdPercentage}, " +
                           $"ThreshAbsSec={newSettingsToSave.ScrobbleThresholdAbsoluteSeconds}");
        }


        if (actualChangesMade)
        {
            _settingsService.SaveSettings(newSettingsToSave);
            SettingsChanged = true; // This signals that persistent changes were made
        }
        else
        {
            // Even if MarkSettingsChanged was called (UI was touched), if the final state
            // matches what's on disk, then no "actual change" for the purpose of post-save actions.
            SettingsChanged = false;
        }
    }
}