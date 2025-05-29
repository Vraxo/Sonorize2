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

    private SettingsViewSection _currentSettingsViewSection = SettingsViewSection.Directories;
    public SettingsViewSection CurrentSettingsViewSection
    {
        get => _currentSettingsViewSection;
        set => SetProperty(ref _currentSettingsViewSection, value);
    }

    public ICommand AddDirectoryCommand { get; }
    public ICommand RemoveDirectoryCommand { get; }
    public ICommand SaveAndCloseCommand { get; }
    public ICommand ShowDirectoriesSettingsCommand { get; }
    public ICommand ShowThemeSettingsCommand { get; }
    public ICommand ShowScrobblingSettingsCommand { get; }


    public bool CanRemoveDirectory => SelectedDirectory != null;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _themeService = new ThemeService(null); // ThemeService used for GetAvailableThemeFiles
        _settingsPersistenceManager = new SettingsPersistenceManager(settingsService);

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

        ShowDirectoriesSettingsCommand = new RelayCommand(_ => CurrentSettingsViewSection = SettingsViewSection.Directories);
        ShowThemeSettingsCommand = new RelayCommand(_ => CurrentSettingsViewSection = SettingsViewSection.Theme);
        ShowScrobblingSettingsCommand = new RelayCommand(_ => CurrentSettingsViewSection = SettingsViewSection.Scrobbling);


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
            Debug.WriteLine("[SettingsVM] Settings marked as changed (UI interaction).");
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
        AppSettings settingsOnDisk = _settingsService.LoadSettings();

        bool changesPersisted = _settingsPersistenceManager.ApplyAndSaveChanges(
            settingsOnDisk,
            this.MusicDirectories,
            this.InitialMusicDirectories,
            this.SelectedThemeFile,
            this.LastfmSettings
        );

        if (changesPersisted)
        {
            // Update the baseline for future comparisons if changes were actually saved
            this.InitialMusicDirectories = new List<string>(this.MusicDirectories);
            // SettingsChanged flag is used by ApplicationInteractionService to determine if post-save processing is needed.
            // If ApplyAndSaveChanges persisted anything, then SettingsChanged should be true.
            // If MarkSettingsChanged() was called due to UI interaction but no actual changes were persisted,
            // then SettingsChanged should reflect that no *persistent* change happened.
            this.SettingsChanged = true;
            Debug.WriteLine("[SettingsVM] Changes were persisted by SettingsPersistenceManager.");
        }
        else
        {
            // If no changes were persisted, ensure SettingsChanged reflects this,
            // even if UI interactions previously set it to true.
            // This is important because SettingsChanged is checked *after* this method by the caller.
            this.SettingsChanged = false;
            Debug.WriteLine("[SettingsVM] No changes were persisted by SettingsPersistenceManager.");
        }
    }
}