using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
// Removed: using Avalonia.Platform.Storage; // No longer directly used here
using Sonorize.Models; // Required for AppSettings type
using Sonorize.Services;

namespace Sonorize.ViewModels;

public enum SettingsViewSection
{
    Directories,
    Theme,
    Scrobbling
}

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService;
    private readonly SettingsPersistenceManager _settingsPersistenceManager;

    // Music Directories are now managed by a child ViewModel
    public MusicDirectoriesSettingsViewModel MusicDirectoriesSettings { get; }

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

    private SettingsViewSection _currentSettingsViewSection = SettingsViewSection.Directories;
    public SettingsViewSection CurrentSettingsViewSection
    {
        get => _currentSettingsViewSection;
        set => SetProperty(ref _currentSettingsViewSection, value);
    }

    // Commands related to music directories are now in MusicDirectoriesSettingsViewModel
    // public ICommand AddDirectoryCommand { get; } // Moved
    // public ICommand RemoveDirectoryCommand { get; } // Moved
    public ICommand SaveAndCloseCommand { get; }
    public ICommand ShowDirectoriesSettingsCommand { get; }
    public ICommand ShowThemeSettingsCommand { get; }
    public ICommand ShowScrobblingSettingsCommand { get; }

    // public bool CanRemoveDirectory => SelectedDirectory != null; // Moved

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _themeService = new ThemeService(null); // ThemeService used for GetAvailableThemeFiles
        _settingsPersistenceManager = new SettingsPersistenceManager(settingsService);

        var settings = _settingsService.LoadSettings();

        // Initialize the new child ViewModel for music directories
        MusicDirectoriesSettings = new MusicDirectoriesSettingsViewModel(settings.MusicDirectories, MarkSettingsChanged);

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

        // AddDirectoryCommand and RemoveDirectoryCommand are now part of MusicDirectoriesSettings
        SaveAndCloseCommand = new RelayCommand(SaveSettings);

        ShowDirectoriesSettingsCommand = new RelayCommand(_ => CurrentSettingsViewSection = SettingsViewSection.Directories);
        ShowThemeSettingsCommand = new RelayCommand(_ => CurrentSettingsViewSection = SettingsViewSection.Theme);
        ShowScrobblingSettingsCommand = new RelayCommand(_ => CurrentSettingsViewSection = SettingsViewSection.Scrobbling);

        // PropertyChanged handler for SelectedDirectory is now within MusicDirectoriesSettingsViewModel
        // MusicDirectories.CollectionChanged is now handled by MusicDirectoriesSettingsViewModel
    }

    private void MarkSettingsChanged()
    {
        if (!SettingsChanged)
        {
            SettingsChanged = true;
            Debug.WriteLine("[SettingsVM] Settings marked as changed (UI interaction or child VM).");
        }
    }

    // AddDirectory method moved to MusicDirectoriesSettingsViewModel
    // RemoveSelectedDirectory method moved to MusicDirectoriesSettingsViewModel

    private void SaveSettings(object? parameter)
    {
        AppSettings settingsOnDisk = _settingsService.LoadSettings();

        // Pass data from the child MusicDirectoriesSettingsViewModel
        bool changesPersisted = _settingsPersistenceManager.ApplyAndSaveChanges(
            settingsOnDisk,
            MusicDirectoriesSettings.MusicDirectories, // From child VM
            MusicDirectoriesSettings.InitialMusicDirectories, // From child VM
            this.SelectedThemeFile,
            this.LastfmSettings
        );

        if (changesPersisted)
        {
            // SettingsChanged flag is used by ApplicationInteractionService to determine if post-save processing is needed.
            // If ApplyAndSaveChanges persisted anything, then SettingsChanged should be true.
            this.SettingsChanged = true; // Ensure this reflects that persistent changes were made
            Debug.WriteLine("[SettingsVM] Changes were persisted by SettingsPersistenceManager.");
        }
        else
        {
            // If no changes were persisted, ensure SettingsChanged reflects this,
            // even if UI interactions previously set it to true.
            this.SettingsChanged = false;
            Debug.WriteLine("[SettingsVM] No changes were persisted by SettingsPersistenceManager.");
        }
    }
}