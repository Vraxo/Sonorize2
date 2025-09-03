using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryDisplayModeService : ViewModelBase
{
    private readonly SettingsService _settingsService;

    private SongDisplayMode _artistViewMode;
    public SongDisplayMode ArtistViewMode
    {
        get => _artistViewMode;
        private set
        {
            if (SetProperty(ref _artistViewMode, value))
            {
                Debug.WriteLine($"[DisplayModeService] ArtistViewMode changed to: {value}");
                SavePreference(nameof(AppSettings.ArtistViewModePreference), value.ToString());
            }
        }
    }

    private SongDisplayMode _albumViewMode;
    public SongDisplayMode AlbumViewMode
    {
        get => _albumViewMode;
        private set
        {
            if (SetProperty(ref _albumViewMode, value))
            {
                Debug.WriteLine($"[DisplayModeService] AlbumViewMode changed to: {value}");
                SavePreference(nameof(AppSettings.AlbumViewModePreference), value.ToString());
            }
        }
    }

    private SongDisplayMode _playlistViewMode;
    public SongDisplayMode PlaylistViewMode
    {
        get => _playlistViewMode;
        private set
        {
            if (SetProperty(ref _playlistViewMode, value))
            {
                Debug.WriteLine($"[DisplayModeService] PlaylistViewMode changed to: {value}");
                SavePreference(nameof(AppSettings.PlaylistViewModePreference), value.ToString());
            }
        }
    }

    public GridViewImageType ArtistGridDisplayType { get; private set; }
    public GridViewImageType AlbumGridDisplayType { get; private set; }
    public GridViewImageType PlaylistGridDisplayType { get; private set; }

    public ICommand SetDisplayModeCommand { get; }

    public LibraryDisplayModeService(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        LoadDisplayPreferences();

        SetDisplayModeCommand = new RelayCommand(
            param =>
            {
                if (param is not (string targetView, SongDisplayMode mode))
                {
                    return;
                }

                switch (targetView)
                {
                    case "Artists": ArtistViewMode = mode; break;
                    case "Albums": AlbumViewMode = mode; break;
                    case "Playlists": PlaylistViewMode = mode; break;
                }
            },
            _ => true // Command is always executable
        );
    }

    public void ReloadDisplayPreferences()
    {
        LoadDisplayPreferences();
    }

    private void LoadDisplayPreferences()
    {
        AppSettings settings = _settingsService.LoadSettings();
        _artistViewMode = Enum.TryParse<SongDisplayMode>(settings.ArtistViewModePreference, out var artMode) ? artMode : SongDisplayMode.Detailed;
        _albumViewMode = Enum.TryParse<SongDisplayMode>(settings.AlbumViewModePreference, out var albMode) ? albMode : SongDisplayMode.Detailed;
        _playlistViewMode = Enum.TryParse<SongDisplayMode>(settings.PlaylistViewModePreference, out var playMode) ? playMode : SongDisplayMode.Detailed;

        ArtistGridDisplayType = Enum.TryParse<GridViewImageType>(settings.ArtistGridViewImageType, out var artistGridType) ? artistGridType : GridViewImageType.Composite;
        AlbumGridDisplayType = Enum.TryParse<GridViewImageType>(settings.AlbumGridViewImageType, out var albumGridType) ? albumGridType : GridViewImageType.Composite;
        PlaylistGridDisplayType = Enum.TryParse<GridViewImageType>(settings.PlaylistGridViewImageType, out var playlistGridType) ? playlistGridType : GridViewImageType.Composite;

        // Initial OnPropertyChanged for any subscribers after loading
        OnPropertyChanged(nameof(ArtistViewMode));
        OnPropertyChanged(nameof(AlbumViewMode));
        OnPropertyChanged(nameof(PlaylistViewMode));
        OnPropertyChanged(nameof(ArtistGridDisplayType));
        OnPropertyChanged(nameof(AlbumGridDisplayType));
        OnPropertyChanged(nameof(PlaylistGridDisplayType));
    }

    private void SavePreference(string preferenceKey, string value)
    {
        AppSettings settings = _settingsService.LoadSettings();
        var prop = typeof(AppSettings).GetProperty(preferenceKey);
        if (prop != null && prop.CanWrite)
        {
            prop.SetValue(settings, value, null);
        }
        _settingsService.SaveSettings(settings);
    }
}