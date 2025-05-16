using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sonorize.Models;
using Sonorize.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Diagnostics;

namespace Sonorize.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly ThemeService _themeService; // Inject ThemeService for listing themes

    public ObservableCollection<string> MusicDirectories { get; } = new();

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
                SettingsChanged = true; // Changing theme is a setting change
            }
        }
    }

    public bool SettingsChanged { get; private set; } = false;

    public ICommand AddDirectoryCommand { get; }
    public ICommand RemoveDirectoryCommand { get; }
    public ICommand SaveAndCloseCommand { get; }

    public bool CanRemoveDirectory => SelectedDirectory != null;

    // Constructor now needs ThemeService
    public SettingsViewModel(SettingsService settingsService) // Keep original signature for MainViewModel for now
    {
        _settingsService = settingsService;
        _themeService = new ThemeService(null); // Create a temporary instance to list files
                                                // A better DI approach would pass it from App.cs

        var settings = _settingsService.LoadSettings();
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


        AddDirectoryCommand = new RelayCommand(async owner => await AddDirectory(owner as Window));
        RemoveDirectoryCommand = new RelayCommand(RemoveSelectedDirectory, _ => CanRemoveDirectory);
        SaveAndCloseCommand = new RelayCommand(SaveSettings);

        PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SelectedDirectory))
            {
                (RemoveDirectoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        };
    }

    private async Task AddDirectory(Window? owner)
    {
        // ... (AddDirectory remains the same) ...
        if (owner == null) return;
        var dialog = new OpenFolderDialog() { Title = "Select Music Directory" };
        var result = await dialog.ShowAsync(owner);
        if (result != null && !string.IsNullOrEmpty(result))
        {
            if (!MusicDirectories.Contains(result))
            {
                MusicDirectories.Add(result);
                SettingsChanged = true;
            }
        }
    }

    private void RemoveSelectedDirectory(object? parameter)
    {
        // ... (RemoveSelectedDirectory remains the same) ...
        if (SelectedDirectory != null)
        {
            MusicDirectories.Remove(SelectedDirectory);
            SelectedDirectory = null;
            SettingsChanged = true;
        }
    }

    private void SaveSettings(object? parameter)
    {
        var currentSettings = _settingsService.LoadSettings(); // Load current to preserve other settings if any
        currentSettings.MusicDirectories = MusicDirectories.ToList();
        currentSettings.PreferredThemeFileName = SelectedThemeFile;

        _settingsService.SaveSettings(currentSettings);
        SettingsChanged = true;
        Debug.WriteLine($"[SettingsVM] Saved theme: {SelectedThemeFile}");
    }
}