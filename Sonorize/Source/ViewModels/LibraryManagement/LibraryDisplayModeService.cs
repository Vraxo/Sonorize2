using System;
using System.ComponentModel;
using System.Windows.Input;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryDisplayModeService : ViewModelBase
{
    private readonly SettingsService _settingsService;

    private SongDisplayMode _libraryViewMode;
    public SongDisplayMode LibraryViewMode
    {
        get => _libraryViewMode;
        private set // Setter is private, changes are made via command
        {
            if (SetProperty(ref _libraryViewMode, value))
            {
                SavePreference(nameof(AppSettings.LibraryViewModePreference), value);
            }
        }
    }

    private SongDisplayMode _artistViewMode;
    public SongDisplayMode ArtistViewMode
    {
        get => _artistViewMode;
        private set
        {
            if (SetProperty(ref _artistViewMode, value))
            {
                SavePreference(nameof(AppSettings.ArtistViewModePreference), value);
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
                SavePreference(nameof(AppSettings.AlbumViewModePreference), value);
            }
        }
    }

    public ICommand SetDisplayModeCommand { get; }

    public LibraryDisplayModeService(SettingsService settingsService)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        LoadPreferences();

        SetDisplayModeCommand = new RelayCommand(
            param =>
            {
                if (param is not (string targetView, SongDisplayMode mode))
                {
                    return;
                }

                switch (targetView)
                {
                    case "Library": LibraryViewMode = mode; break;
                    case "Artists": ArtistViewMode = mode; break;
                    case "Albums": AlbumViewMode = mode; break;
                }
            },
            _ => true // Command is always executable
        );
    }

    private void LoadPreferences()
    {
        AppSettings settings = _settingsService.LoadSettings();
        _libraryViewMode = Enum.TryParse<SongDisplayMode>(settings.LibraryViewModePreference, out var libMode) ? libMode : SongDisplayMode.Detailed;
        _artistViewMode = Enum.TryParse<SongDisplayMode>(settings.ArtistViewModePreference, out var artMode) ? artMode : SongDisplayMode.Detailed;
        _albumViewMode = Enum.TryParse<SongDisplayMode>(settings.AlbumViewModePreference, out var albMode) ? albMode : SongDisplayMode.Detailed;

        // Initial OnPropertyChanged for any subscribers after loading
        OnPropertyChanged(nameof(LibraryViewMode));
        OnPropertyChanged(nameof(ArtistViewMode));
        OnPropertyChanged(nameof(AlbumViewMode));
    }

    private void SavePreference(string preferenceKey, SongDisplayMode mode)
    {
        AppSettings settings = _settingsService.LoadSettings();
        switch (preferenceKey)
        {
            case nameof(AppSettings.LibraryViewModePreference):
                settings.LibraryViewModePreference = mode.ToString();
                break;
            case nameof(AppSettings.ArtistViewModePreference):
                settings.ArtistViewModePreference = mode.ToString();
                break;
            case nameof(AppSettings.AlbumViewModePreference):
                settings.AlbumViewModePreference = mode.ToString();
                break;
        }
        _settingsService.SaveSettings(settings);
    }
}