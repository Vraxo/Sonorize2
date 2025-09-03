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

    public ApplicationInteractionService(
        SettingsService settingsService,
        SettingsChangeProcessorService settingsChangeProcessorService,
        ThemeColors currentTheme)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _settingsChangeProcessorService = settingsChangeProcessorService ?? throw new ArgumentNullException(nameof(settingsChangeProcessorService));
        _currentTheme = currentTheme ?? throw new ArgumentNullException(nameof(currentTheme));
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
        if (!settings.General.MusicDirectories.Any(d => string.Equals(d, folderPath, StringComparison.OrdinalIgnoreCase)))
        {
            settings.General.MusicDirectories.Add(folderPath);
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
}