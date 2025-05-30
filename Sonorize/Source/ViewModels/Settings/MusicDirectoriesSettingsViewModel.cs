using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Platform.Storage;

namespace Sonorize.ViewModels;

public class MusicDirectoriesSettingsViewModel : ViewModelBase
{
    private readonly Action _notifyParentSettingsChanged;

    public ObservableCollection<string> MusicDirectories { get; }
    public List<string> InitialMusicDirectories { get; }

    public string? SelectedDirectory
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                (RemoveDirectoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand AddDirectoryCommand { get; }
    public ICommand RemoveDirectoryCommand { get; }

    public bool CanRemoveDirectory => SelectedDirectory != null;

    // This property can be used by the parent to quickly check if this specific section has changes
    // compared to its initial state when the settings dialog was opened.
    public bool HasChangesFromInitialState => !InitialMusicDirectories.SequenceEqual(MusicDirectories);

    public MusicDirectoriesSettingsViewModel(IEnumerable<string> initialDirs, Action notifyParentSettingsChanged)
    {
        _notifyParentSettingsChanged = notifyParentSettingsChanged ?? throw new ArgumentNullException(nameof(notifyParentSettingsChanged));
        InitialMusicDirectories = new List<string>(initialDirs);
        MusicDirectories = new ObservableCollection<string>(initialDirs);

        AddDirectoryCommand = new RelayCommand(async owner => await AddDirectoryAsync(owner as Window));
        RemoveDirectoryCommand = new RelayCommand(RemoveSelectedDirectoryAction, _ => CanRemoveDirectory);

        MusicDirectories.CollectionChanged += (s, e) => {
            _notifyParentSettingsChanged(); // Notify parent that some UI interaction happened
            OnPropertyChanged(nameof(HasChangesFromInitialState)); // Notify that this section's state might have changed
        };
    }

    private async Task AddDirectoryAsync(Window? owner)
    {
        if (owner?.StorageProvider == null)
        {
            Debug.WriteLine("[MusicDirSettingsVM] StorageProvider is not available for AddDirectory.");
            return;
        }

        var options = new FolderPickerOpenOptions
        {
            Title = "Select Music Directory",
            AllowMultiple = false
        };

        var result = await owner.StorageProvider.OpenFolderPickerAsync(options);

        if (result?.Count > 0)
        {
            var folder = result.FirstOrDefault();
            if (folder == null) return;

            string? path = null;
            try
            {
                // Attempt to get a usable local path
                if (folder.Path.IsAbsoluteUri) path = folder.Path.LocalPath;
                else path = folder.Name; // Fallback or handle relative paths if necessary

                if (!string.IsNullOrEmpty(path))
                {
                    path = Path.GetFullPath(path); // Normalize
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MusicDirSettingsVM] Error processing path {folder.Path}: {ex.Message}");
                // Optionally inform user about path issue
                return;
            }

            if (!string.IsNullOrEmpty(path) && Directory.Exists(path) && !MusicDirectories.Contains(path))
            {
                MusicDirectories.Add(path); // Triggers CollectionChanged
            }
            else if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                Debug.WriteLine($"[MusicDirSettingsVM] Selected path does not exist or is not a directory: {path}");
                // Optionally inform user
            }
        }
    }

    private void RemoveSelectedDirectoryAction(object? parameter)
    {
        if (SelectedDirectory is null)
        {
            return;
        }

        MusicDirectories.Remove(SelectedDirectory); // Triggers CollectionChanged
        SelectedDirectory = null; // Triggers PropertyChanged for UI
    }
}