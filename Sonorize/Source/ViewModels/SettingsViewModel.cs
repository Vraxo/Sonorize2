using Avalonia.Controls; // For OpenFolderDialog
using Avalonia.Platform.Storage; // For IStorageFolder
using Sonorize.Models;
using Sonorize.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sonorize.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    public ObservableCollection<string> MusicDirectories { get; } = new();

    private string? _selectedDirectory;
    public string? SelectedDirectory
    {
        get => _selectedDirectory;
        set => SetProperty(ref _selectedDirectory, value, nameof(CanRemoveDirectory));
    }

    public bool SettingsChanged { get; private set; } = false;

    public ICommand AddDirectoryCommand { get; }
    public ICommand RemoveDirectoryCommand { get; }
    public ICommand SaveAndCloseCommand { get; } // Save implies close for this simple dialog

    public bool CanRemoveDirectory => SelectedDirectory != null;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var settings = _settingsService.LoadSettings();
        foreach (var dir in settings.MusicDirectories)
        {
            MusicDirectories.Add(dir);
        }

        AddDirectoryCommand = new RelayCommand(async owner => await AddDirectory(owner as Window));
        RemoveDirectoryCommand = new RelayCommand(RemoveSelectedDirectory, _ => CanRemoveDirectory);
        SaveAndCloseCommand = new RelayCommand(SaveSettings);

        // This is to ensure CanExecute for RemoveDirectoryCommand is re-evaluated
        // when SelectedDirectory changes. Manual update for simple RelayCommand.
        PropertyChanged += (s, e) => {
            if (e.PropertyName == nameof(SelectedDirectory))
            {
                (RemoveDirectoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        };
    }

    private async Task AddDirectory(Window? owner)
    {
        if (owner == null) return; // Should always have an owner for a dialog

        var dialog = new OpenFolderDialog() { Title = "Select Music Directory" };
        var result = await dialog.ShowAsync(owner); // string path

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
        if (SelectedDirectory != null)
        {
            MusicDirectories.Remove(SelectedDirectory);
            SelectedDirectory = null; // Clear selection
            SettingsChanged = true;
        }
    }

    private void SaveSettings(object? parameter)
    {
        var settings = new AppSettings
        {
            MusicDirectories = MusicDirectories.ToList()
        };
        _settingsService.SaveSettings(settings);
        SettingsChanged = true; // Ensure flag is set even if only removals occurred

        // The window will be closed by its own code-behind after this command
        // if this VM is used in a ShowDialog context where the dialog handles its lifecycle.
        // If the command needs to close the window, it would need a reference to it.
        // For now, SettingsWindow will handle its closure.
    }
}