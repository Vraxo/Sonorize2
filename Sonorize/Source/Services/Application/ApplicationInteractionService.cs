using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Services;

public class ApplicationInteractionService
{
    private readonly SettingsService _settingsService;
    private readonly SettingsChangeProcessorService _settingsChangeProcessorService;
    private readonly ThemeColors _currentTheme;
    private readonly SongMetadataService _songMetadataService; // Added

    public ApplicationInteractionService(
        SettingsService settingsService,
        SettingsChangeProcessorService settingsChangeProcessorService,
        ThemeColors currentTheme,
        SongMetadataService songMetadataService) // Added
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _settingsChangeProcessorService = settingsChangeProcessorService ?? throw new ArgumentNullException(nameof(settingsChangeProcessorService));
        _currentTheme = currentTheme ?? throw new ArgumentNullException(nameof(currentTheme));
        _songMetadataService = songMetadataService ?? throw new ArgumentNullException(nameof(songMetadataService)); // Added
    }

    public async Task<(List<string> statusMessages, bool settingsChanged)> HandleOpenSettingsDialogAsync(Window owner)
    {
        var currentSettingsBeforeDialog = _settingsService.LoadSettings();
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(_currentTheme) { DataContext = settingsVM };

        await settingsDialog.ShowDialog(owner);

        List<string> statusMessages = [];
        bool overallSettingsChanged = false;

        if (settingsVM.SettingsChanged)
        {
            Debug.WriteLine("[AppInteractionService] Settings changed detected after dialog closed. Processing changes...");
            var newSettingsAfterDialog = _settingsService.LoadSettings(); // Get the latest saved settings

            statusMessages = await _settingsChangeProcessorService.ProcessChangesAndGetStatus(
                currentSettingsBeforeDialog,
                newSettingsAfterDialog
            );
            overallSettingsChanged = true; // Indicate that settings were processed
        }
        else
        {
            Debug.WriteLine("[AppInteractionService] Settings dialog closed, no changes reported by SettingsViewModel.");
        }
        return (statusMessages, overallSettingsChanged);
    }

    public async Task<(bool directoryAddedAndLibraryRefreshNeeded, string statusMessage)> HandleAddMusicDirectoryAsync(Window owner)
    {
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Music Directory", AllowMultiple = false });

        if (result == null || result.Count == 0)
        {
            Debug.WriteLine("[AppInteractionService] Folder picker cancelled or returned no results.");
            return (false, "Folder selection cancelled.");
        }

        string? folderPath = null;
        try
        {
            folderPath = Path.GetFullPath(result[0].Path.LocalPath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppInteractionService] Error getting full path for selected directory: {ex.Message}");
            return (false, "Error getting path for selected directory.");
        }

        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            Debug.WriteLine($"[AppInteractionService] Selected directory path is invalid or does not exist: {folderPath}");
            return (false, "Invalid directory selected.");
        }

        var settings = _settingsService.LoadSettings();
        if (!settings.MusicDirectories.Any(d => string.Equals(d, folderPath, StringComparison.OrdinalIgnoreCase)))
        {
            settings.MusicDirectories.Add(folderPath);
            _settingsService.SaveSettings(settings);
            Debug.WriteLine($"[AppInteractionService] Added new directory: {folderPath}. Library refresh needed.");
            return (true, $"Added directory: {Path.GetFileName(folderPath)}. Library refreshing...");
        }
        else
        {
            Debug.WriteLine($"[AppInteractionService] Directory already exists: {folderPath}");
            return (false, "Directory already in library.");
        }
    }

    public async Task<bool> HandleEditSongMetadataDialogAsync(Song song, Window owner)
    {
        if (song == null)
        {
            Debug.WriteLine("[AppInteractionService] HandleEditSongMetadataDialogAsync: Song is null.");
            return false;
        }

        var editorViewModel = new SongMetadataEditorViewModel(song);
        var editorDialog = new Sonorize.Views.SongMetadataEditorWindow(_currentTheme)
        {
            DataContext = editorViewModel
        };

        await editorDialog.ShowDialog(owner);

        if (editorViewModel.DialogResult) // True if "Save" was clicked
        {
            Debug.WriteLine($"[AppInteractionService] Metadata editor closed with Save. Attempting to save metadata for {song.Title}.");
            bool success = await _songMetadataService.SaveMetadataAsync(song);
            if (success)
            {
                // The song object's properties (Title, Artist, Album) were already updated
                // by the two-way binding in SongMetadataEditorViewModel.
                // These changes will be reflected in the UI because Song is a ViewModelBase.
                Debug.WriteLine($"[AppInteractionService] Metadata for {song.Title} saved to file successfully.");
                // Note: If Artist/Album names change, Artists/Albums collections in LibraryVM are not auto-updated here.
                // This would require more complex logic or a library re-scan for full consistency.
            }
            else
            {
                Debug.WriteLine($"[AppInteractionService] Failed to save metadata for {song.Title} to file.");
            }
            return success;
        }

        Debug.WriteLine($"[AppInteractionService] Metadata editor for {song.Title} closed without saving (Cancelled or closed).");
        return false;
    }
}