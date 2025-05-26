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

    // Last.fm Settings Properties
    private bool _lastfmScrobblingEnabled;
    public bool LastfmScrobblingEnabled
    {
        get => _lastfmScrobblingEnabled;
        set { if (SetProperty(ref _lastfmScrobblingEnabled, value)) MarkSettingsChanged(); }
    }

    private string? _lastfmUsername;
    public string? LastfmUsername
    {
        get => _lastfmUsername;
        set { if (SetProperty(ref _lastfmUsername, value)) MarkSettingsChanged(); }
    }

    private string? _lastfmPassword;
    public string? LastfmPassword
    {
        get => _lastfmPassword;
        set { if (SetProperty(ref _lastfmPassword, value)) MarkSettingsChanged(); }
    }

    private int _scrobbleThresholdPercentage;
    public int ScrobbleThresholdPercentage
    {
        get => _scrobbleThresholdPercentage;
        set { if (SetProperty(ref _scrobbleThresholdPercentage, value)) MarkSettingsChanged(); }
    }

    private int _scrobbleThresholdAbsoluteSeconds;
    public int ScrobbleThresholdAbsoluteSeconds
    {
        get => _scrobbleThresholdAbsoluteSeconds;
        set { if (SetProperty(ref _scrobbleThresholdAbsoluteSeconds, value)) MarkSettingsChanged(); }
    }


    public bool SettingsChanged { get; private set; } = false;

    public ICommand AddDirectoryCommand { get; }
    public ICommand RemoveDirectoryCommand { get; }
    public ICommand SaveAndCloseCommand { get; }

    public bool CanRemoveDirectory => SelectedDirectory != null;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _themeService = new ThemeService(null);

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

        // Load Last.fm settings
        _lastfmScrobblingEnabled = settings.LastfmScrobblingEnabled;
        _lastfmUsername = settings.LastfmUsername;
        _lastfmPassword = settings.LastfmPassword;
        _scrobbleThresholdPercentage = settings.ScrobbleThresholdPercentage;
        _scrobbleThresholdAbsoluteSeconds = settings.ScrobbleThresholdAbsoluteSeconds;


        SettingsChanged = false;

        AddDirectoryCommand = new RelayCommand(async owner => await AddDirectory(owner as Window));
        RemoveDirectoryCommand = new RelayCommand(RemoveSelectedDirectory, _ => CanRemoveDirectory);
        SaveAndCloseCommand = new RelayCommand(SaveSettings);

        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(SelectedDirectory))
            {
                (RemoveDirectoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
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
        }
    }

    private void RemoveSelectedDirectory(object? parameter)
    {
        if (SelectedDirectory == null) return;

        MusicDirectories.Remove(SelectedDirectory);
        SelectedDirectory = null;
    }

    private void SaveSettings(object? parameter)
    {
        AppSettings currentSettings = _settingsService.LoadSettings();
        bool actualChangesMade = false;

        if (!InitialMusicDirectories.SequenceEqual(MusicDirectories))
        {
            currentSettings.MusicDirectories = new List<string>(MusicDirectories);
            InitialMusicDirectories = new List<string>(MusicDirectories);
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsVM] Saved directories count: {currentSettings.MusicDirectories.Count}");
        }

        if (currentSettings.PreferredThemeFileName != SelectedThemeFile)
        {
            currentSettings.PreferredThemeFileName = SelectedThemeFile;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsVM] Saved theme: {SelectedThemeFile}");
        }

        if (currentSettings.LastfmScrobblingEnabled != LastfmScrobblingEnabled)
        {
            currentSettings.LastfmScrobblingEnabled = LastfmScrobblingEnabled;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsVM] Saved LastfmScrobblingEnabled: {currentSettings.LastfmScrobblingEnabled}");
        }
        if (currentSettings.LastfmUsername != LastfmUsername)
        {
            currentSettings.LastfmUsername = LastfmUsername;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsVM] Saved LastfmUsername: {currentSettings.LastfmUsername}");
        }
        if (currentSettings.LastfmPassword != LastfmPassword)
        {
            currentSettings.LastfmPassword = LastfmPassword;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsVM] Saved LastfmPassword (length: {currentSettings.LastfmPassword?.Length ?? 0})");
        }
        if (currentSettings.ScrobbleThresholdPercentage != ScrobbleThresholdPercentage)
        {
            currentSettings.ScrobbleThresholdPercentage = ScrobbleThresholdPercentage;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsVM] Saved ScrobbleThresholdPercentage: {currentSettings.ScrobbleThresholdPercentage}");
        }
        if (currentSettings.ScrobbleThresholdAbsoluteSeconds != ScrobbleThresholdAbsoluteSeconds)
        {
            currentSettings.ScrobbleThresholdAbsoluteSeconds = ScrobbleThresholdAbsoluteSeconds;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsVM] Saved ScrobbleThresholdAbsoluteSeconds: {currentSettings.ScrobbleThresholdAbsoluteSeconds}");
        }

        if (actualChangesMade)
        {
            _settingsService.SaveSettings(currentSettings);
            SettingsChanged = true;
        }
        else
        {
            SettingsChanged = false;
        }
    }
}