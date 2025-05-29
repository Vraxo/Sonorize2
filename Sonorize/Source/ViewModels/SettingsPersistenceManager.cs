using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels;

public class SettingsPersistenceManager
{
    private readonly SettingsService _settingsService;

    public SettingsPersistenceManager(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new System.ArgumentNullException(nameof(settingsService));
    }

    public bool ApplyAndSaveChanges(
        AppSettings settingsOnDisk,
        IEnumerable<string> currentUiMusicDirs,
        IEnumerable<string> initialUiMusicDirs, // Used to detect if UI list changed from its initial state
        string? currentUiSelectedTheme,
        LastfmSettingsViewModel currentUiLastfmSettings)
    {
        AppSettings newSettingsToSave = new();

        // Preserve settings not managed by SettingsViewModel's UI by copying from disk state
        newSettingsToSave.LibraryViewModePreference = settingsOnDisk.LibraryViewModePreference;
        newSettingsToSave.ArtistViewModePreference = settingsOnDisk.ArtistViewModePreference;
        newSettingsToSave.AlbumViewModePreference = settingsOnDisk.AlbumViewModePreference;
        newSettingsToSave.LastfmSessionKey = settingsOnDisk.LastfmSessionKey;
        // Add any other unmanaged settings here if AppSettings grows

        bool actualChangesMade = false;

        // Music Directories
        // A change is made if the current UI list is different from its initial state OR different from disk state
        if (!initialUiMusicDirs.SequenceEqual(currentUiMusicDirs) ||
            !settingsOnDisk.MusicDirectories.SequenceEqual(currentUiMusicDirs))
        {
            newSettingsToSave.MusicDirectories = new List<string>(currentUiMusicDirs);
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsPersistence] Music directories changed. Count: {newSettingsToSave.MusicDirectories.Count}");
        }
        else
        {
            newSettingsToSave.MusicDirectories = new List<string>(settingsOnDisk.MusicDirectories);
        }

        // Theme
        if (settingsOnDisk.PreferredThemeFileName != currentUiSelectedTheme)
        {
            newSettingsToSave.PreferredThemeFileName = currentUiSelectedTheme;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsPersistence] Theme changed to: {currentUiSelectedTheme}");
        }
        else
        {
            newSettingsToSave.PreferredThemeFileName = settingsOnDisk.PreferredThemeFileName;
        }

        // Last.fm Settings - Compare UI state against disk state for change detection
        if (settingsOnDisk.LastfmScrobblingEnabled != currentUiLastfmSettings.LastfmScrobblingEnabled) actualChangesMade = true;
        if (settingsOnDisk.LastfmUsername != currentUiLastfmSettings.LastfmUsername) actualChangesMade = true;
        // Password comparison: change if UI has a new password (and it's different from disk, though disk doesn't store it long-term)
        // Or, more simply, if the password field in UI was touched and is not null.
        // The original logic was: settingsOnDisk.LastfmPassword != LastfmSettings.LastfmPassword && !string.IsNullOrEmpty(LastfmSettings.LastfmPassword)
        // This implies if LastfmSettings.LastfmPassword is set, it's a change (as disk password is nulled after session key).
        if (!string.IsNullOrEmpty(currentUiLastfmSettings.LastfmPassword)) actualChangesMade = true; // If a password was entered, consider it a change for re-auth.
        if (settingsOnDisk.ScrobbleThresholdPercentage != currentUiLastfmSettings.ScrobbleThresholdPercentage) actualChangesMade = true;
        if (settingsOnDisk.ScrobbleThresholdAbsoluteSeconds != currentUiLastfmSettings.ScrobbleThresholdAbsoluteSeconds) actualChangesMade = true;

        // Apply UI Last.fm settings to newSettingsToSave object
        currentUiLastfmSettings.UpdateAppSettings(newSettingsToSave);
        if (actualChangesMade)
        {
            // Log Last.fm specific changes if any were part of overall changes
            Debug.WriteLine($"[SettingsPersistence] Last.fm settings potentially updated in newSettingsToSave: " +
                          $"Scrobbling={newSettingsToSave.LastfmScrobblingEnabled}, " +
                          $"User={newSettingsToSave.LastfmUsername}, " +
                          $"PassLen={(newSettingsToSave.LastfmPassword?.Length ?? 0)}, " +
                          $"Thresh%={newSettingsToSave.ScrobbleThresholdPercentage}, " +
                          $"ThreshAbsSec={newSettingsToSave.ScrobbleThresholdAbsoluteSeconds}");
        }


        if (actualChangesMade)
        {
            _settingsService.SaveSettings(newSettingsToSave);
            Debug.WriteLine("[SettingsPersistence] Actual changes detected and settings saved.");
            return true;
        }

        Debug.WriteLine("[SettingsPersistence] No actual changes to persist.");
        return false;
    }
}