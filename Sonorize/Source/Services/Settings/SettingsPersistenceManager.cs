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

        // Preserve settings not managed by SettingsViewModel's UI by copying from disk state
        newSettingsToSave.ArtistViewModePreference = settingsOnDisk.ArtistViewModePreference;
        newSettingsToSave.AlbumViewModePreference = settingsOnDisk.AlbumViewModePreference;
        newSettingsToSave.PlaylistViewModePreference = settingsOnDisk.PlaylistViewModePreference;
        newSettingsToSave.LastfmSessionKey = settingsOnDisk.LastfmSessionKey;

        bool actualChangesMade = false;

        // Music Directories
        if (musicDirectoriesSettings.HasChangesFromInitialState)
        {
            newSettingsToSave.MusicDirectories = new List<string>(musicDirectoriesSettings.MusicDirectories);
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsPersistence] Music directories changed. Count: {newSettingsToSave.MusicDirectories.Count}");
        }
        else
        {
            newSettingsToSave.MusicDirectories = new List<string>(settingsOnDisk.MusicDirectories);
        }

        // Theme
        if (themeSettings.HasChangesFromInitialState)
        {
            newSettingsToSave.PreferredThemeFileName = themeSettings.SelectedThemeFile;
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsPersistence] Theme changed to: {themeSettings.SelectedThemeFile}");
        }
        else
        {
            newSettingsToSave.PreferredThemeFileName = settingsOnDisk.PreferredThemeFileName;
        }

        // Appearance
        if (appearanceSettings.HasChangesFromInitialState)
        {
            appearanceSettings.UpdateAppSettings(newSettingsToSave);
            actualChangesMade = true;
            Debug.WriteLine($"[SettingsPersistence] Appearance settings changed.");
        }
        else
        {
            newSettingsToSave.ArtistGridViewImageType = settingsOnDisk.ArtistGridViewImageType;
            newSettingsToSave.AlbumGridViewImageType = settingsOnDisk.AlbumGridViewImageType;
            newSettingsToSave.PlaylistGridViewImageType = settingsOnDisk.PlaylistGridViewImageType;
            newSettingsToSave.PlaybackAreaBackgroundStyle = settingsOnDisk.PlaybackAreaBackgroundStyle;
            newSettingsToSave.ShowArtistInLibrary = settingsOnDisk.ShowArtistInLibrary;
            newSettingsToSave.ShowAlbumInLibrary = settingsOnDisk.ShowAlbumInLibrary;
            newSettingsToSave.ShowDurationInLibrary = settingsOnDisk.ShowDurationInLibrary;
            newSettingsToSave.ShowDateAddedInLibrary = settingsOnDisk.ShowDateAddedInLibrary;
            newSettingsToSave.ShowPlayCountInLibrary = settingsOnDisk.ShowPlayCountInLibrary;
            newSettingsToSave.LibraryRowHeight = settingsOnDisk.LibraryRowHeight;
            newSettingsToSave.EnableAlternatingRowColors = settingsOnDisk.EnableAlternatingRowColors;
            newSettingsToSave.UseCompactPlaybackControls = settingsOnDisk.UseCompactPlaybackControls;
        }

        // Last.fm Settings - Compare UI state against disk state for change detection
        bool lastfmChanged = settingsOnDisk.LastfmScrobblingEnabled != lastfmSettings.LastfmScrobblingEnabled ||
                             settingsOnDisk.LastfmUsername != lastfmSettings.LastfmUsername ||
                             !string.IsNullOrEmpty(lastfmSettings.LastfmPassword) ||
                             settingsOnDisk.ScrobbleThresholdPercentage != lastfmSettings.ScrobbleThresholdPercentage ||
                             settingsOnDisk.ScrobbleThresholdAbsoluteSeconds != lastfmSettings.ScrobbleThresholdAbsoluteSeconds;

        if (lastfmChanged) actualChangesMade = true;

        lastfmSettings.UpdateAppSettings(newSettingsToSave);
        if (lastfmChanged)
        {
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