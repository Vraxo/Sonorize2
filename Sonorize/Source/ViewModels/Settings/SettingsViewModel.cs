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
    // private readonly ThemeService _themeService; // ThemeService is now injected into ThemeSettingsViewModel
    private readonly SettingsPersistenceManager _settingsPersistenceManager;

    // Music Directories are now managed by a child ViewModel
    public MusicDirectoriesSettingsViewModel MusicDirectoriesSettings { get; }
    // Theme settings are now managed by a child ViewModel
    public ThemeSettingsViewModel ThemeSettings { get; }

    // public ObservableCollection<string> AvailableThemes { get; } = new(); // Moved
    // public string? SelectedThemeFile { get; set; } // Moved

    // Encapsulated Last.fm Settings
    public LastfmSettingsViewModel LastfmSettings { get; }


    public bool SettingsChanged { get; private set; } = false;

    private SettingsViewSection _currentSettingsViewSection = SettingsViewSection.Directories;
    public SettingsViewSection CurrentSettingsViewSection
    {
        get => _currentSettingsViewSection;
        set => SetProperty(ref _currentSettingsViewSection, value);
    }

    public ICommand SaveAndCloseCommand { get; }
    public ICommand ShowDirectoriesSettingsCommand { get; }
    public ICommand ShowThemeSettingsCommand { get; }
    public ICommand ShowScrobblingSettingsCommand { get; }


    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settingsPersistenceManager = new SettingsPersistenceManager(settingsService);

        var settings = _settingsService.LoadSettings();

        // Initialize the child ViewModel for music directories
        MusicDirectoriesSettings = new MusicDirectoriesSettingsViewModel(settings.MusicDirectories, MarkSettingsChanged);

        // Initialize the child ViewModel for theme settings
        // ThemeService instance is created here for ThemeSettingsViewModel as it's specific to this part
        var themeServiceForChild = new ThemeService(settings.PreferredThemeFileName); // Pass current pref to ensure it loads correctly
        ThemeSettings = new ThemeSettingsViewModel(settings.PreferredThemeFileName, themeServiceForChild, MarkSettingsChanged);


        // Initialize Last.fm settings module
        LastfmSettings = new LastfmSettingsViewModel();
        LastfmSettings.LoadFromSettings(settings);
        LastfmSettings.PropertyChanged += (s, e) => MarkSettingsChanged();


        SettingsChanged = false; // Initial state is unchanged

        SaveAndCloseCommand = new RelayCommand(SaveSettings);

        ShowDirectoriesSettingsCommand = new RelayCommand(_ => CurrentSettingsViewSection = SettingsViewSection.Directories);
        ShowThemeSettingsCommand = new RelayCommand(_ => CurrentSettingsViewSection = SettingsViewSection.Theme);
        ShowScrobblingSettingsCommand = new RelayCommand(_ => CurrentSettingsViewSection = SettingsViewSection.Scrobbling);
    }

    private void MarkSettingsChanged()
    {
        if (SettingsChanged)
        {
            return;
        }

        SettingsChanged = true;
        Debug.WriteLine("[SettingsVM] Settings marked as changed (UI interaction or child VM).");
    }

    private void SaveSettings(object? parameter)
    {
        AppSettings settingsOnDisk = _settingsService.LoadSettings();

        // Pass data from the child ViewModels
        bool changesPersisted = _settingsPersistenceManager.ApplyAndSaveChanges(
            settingsOnDisk,
            MusicDirectoriesSettings.MusicDirectories,
            MusicDirectoriesSettings.InitialMusicDirectories,
            ThemeSettings.SelectedThemeFile, // From ThemeSettingsViewModel
            this.LastfmSettings
        );

        if (changesPersisted)
        {
            this.SettingsChanged = true;
            Debug.WriteLine("[SettingsVM] Changes were persisted by SettingsPersistenceManager.");
        }
        else
        {
            this.SettingsChanged = false;
            Debug.WriteLine("[SettingsVM] No changes were persisted by SettingsPersistenceManager.");
        }
    }
}