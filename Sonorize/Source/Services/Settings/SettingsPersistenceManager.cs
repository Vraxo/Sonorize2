using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.Settings;

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
        MusicDirectoriesSettingsViewModel musicDirectoriesSettings,
        ThemeSettingsViewModel themeSettings,
        LastfmSettingsViewModel lastfmSettings,
        AppearanceSettingsViewModel appearanceSettings)
    {
        AppSettings newSettingsToSave = new();

        // Preserve the existing session key, as the UI doesn't manage it directly.
        newSettingsToSave.Lastfm.SessionKey = settingsOnDisk.Lastfm.SessionKey;

        bool actualChangesMade = false;

        // --- General Settings ---
        if (musicDirectoriesSettings.HasChangesFromInitialState)
        {
            newSettingsToSave.General.MusicDirectories = new List<string>(musicDirectoriesSettings.MusicDirectories);
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsPersistence] Music directories changed.");
        }
        else
        {
            newSettingsToSave.General.MusicDirectories = new List<string>(settingsOnDisk.General.MusicDirectories);
        }

        if (themeSettings.HasChangesFromInitialState)
        {
            newSettingsToSave.General.PreferredThemeFileName = themeSettings.SelectedThemeFile;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsPersistence] Theme changed.");
        }
        else
        {
            newSettingsToSave.General.PreferredThemeFileName = settingsOnDisk.General.PreferredThemeFileName;
        }

        // --- Appearance Settings ---
        if (appearanceSettings.HasChangesFromInitialState)
        {
            appearanceSettings.UpdateAppSettings(newSettingsToSave.Appearance);
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsPersistence] Appearance settings changed.");
        }
        else
        {
            newSettingsToSave.Appearance = settingsOnDisk.Appearance;
        }

        // --- Last.fm Settings ---
        bool lastfmChanged = settingsOnDisk.Lastfm.ScrobblingEnabled != lastfmSettings.LastfmScrobblingEnabled ||
                             settingsOnDisk.Lastfm.Username != lastfmSettings.LastfmUsername ||
                             !string.IsNullOrEmpty(lastfmSettings.LastfmPassword) || // Password field being non-empty is a change
                             settingsOnDisk.Lastfm.ScrobbleThresholdPercentage != lastfmSettings.ScrobbleThresholdPercentage ||
                             settingsOnDisk.Lastfm.ScrobbleThresholdAbsoluteSeconds != lastfmSettings.ScrobbleThresholdAbsoluteSeconds;

        if (lastfmChanged)
        {
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsPersistence] Last.fm settings changed.");
        }

        // Always update from the view model to capture all values, even if just one changed.
        lastfmSettings.UpdateLastfmSettings(newSettingsToSave.Lastfm);


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