using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.LibraryManagement; // Added for ArtistAlbumCollectionManager

namespace Sonorize.ViewModels;

public enum SongDisplayMode
{
    Detailed,
    Compact,
    Grid
}

public class LibraryViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly LoopDataService _loopDataService;
    private readonly MainWindowViewModel _parentViewModel;
    private readonly ArtistAlbumCollectionManager _artistAlbumManager; // Added manager

    private readonly ObservableCollection<Song> _allSongs = [];

    public ObservableCollection<Song> FilteredSongs { get; } = [];
    public ObservableCollection<ArtistViewModel> Artists { get; } = [];
    public ObservableCollection<AlbumViewModel> Albums { get; } = [];

    public ICommand SetDisplayModeCommand { get; }
    public ICommand PreviousTrackCommand { get; }
    public ICommand NextTrackCommand { get; }

    public string SearchQuery
    {
        get;

        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            ApplyFilter();
        }
    } = string.Empty;

    public Song? SelectedSong
    {
        get;

        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }
            Debug.WriteLine($"[LibraryVM] SelectedSong changed to: {value?.Title ?? "null"}");
            RaiseNavigationCommandsCanExecuteChanged();
        }
    }

    public ArtistViewModel? SelectedArtist
    {
        get;

        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            if (value != null)
            {
                OnArtistSelected(value);
            }
        }
    }

    public AlbumViewModel? SelectedAlbum
    {
        get;

        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            if (value is not null)
            {
                OnAlbumSelected(value);
            }
        }
    }

    public bool IsLoadingLibrary
    {
        get;

        private set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            RaiseLibraryCommandsCanExecuteChanged();
        }
    } = false;

    public string LibraryStatusText
    {
        get;

        private set
        {
            SetProperty(ref field, value);
        }
    } = "";

    public SongDisplayMode LibraryViewMode
    {
        get;

        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            AppSettings settings = _settingsService.LoadSettings();
            settings.LibraryViewModePreference = value.ToString();
            _settingsService.SaveSettings(settings);
        }
    }

    public SongDisplayMode ArtistViewMode
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            AppSettings settings = _settingsService.LoadSettings();
            settings.ArtistViewModePreference = value.ToString();
            _settingsService.SaveSettings(settings);
        }
    }

    public SongDisplayMode AlbumViewMode
    {
        get;

        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            AppSettings settings = _settingsService.LoadSettings();
            settings.AlbumViewModePreference = value.ToString();
            _settingsService.SaveSettings(settings);
        }
    }

    public LibraryViewModel(MainWindowViewModel parentViewModel, SettingsService settingsService, MusicLibraryService musicLibraryService, LoopDataService loopDataService)
    {
        _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        _loopDataService = loopDataService;

        // Initialize the manager
        _artistAlbumManager = new ArtistAlbumCollectionManager(Artists, Albums, _musicLibraryService);

        _musicLibraryService.SongThumbnailUpdated += MusicLibraryService_SongThumbnailUpdated;

        AppSettings appSettings = _settingsService.LoadSettings();

        LibraryViewMode = Enum.TryParse<SongDisplayMode>(appSettings.LibraryViewModePreference, out var libMode)
            ? libMode
            : SongDisplayMode.Detailed;

        ArtistViewMode = Enum.TryParse<SongDisplayMode>(appSettings.ArtistViewModePreference, out var artMode)
            ? artMode
            : SongDisplayMode.Detailed;

        AlbumViewMode = Enum.TryParse<SongDisplayMode>(appSettings.AlbumViewModePreference, out var albMode)
            ? albMode
            : SongDisplayMode.Detailed;


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
            _ => true
        );

        PreviousTrackCommand = new RelayCommand(ExecutePreviousTrack, CanExecutePreviousTrack);
        NextTrackCommand = new RelayCommand(ExecuteNextTrack, CanExecuteNextTrack);

        FilteredSongs.CollectionChanged += (sender, e) => RaiseNavigationCommandsCanExecuteChanged();

        UpdateStatusBarText();
    }

    private async void MusicLibraryService_SongThumbnailUpdated(Song updatedSong)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _artistAlbumManager.UpdateCollectionsForSongThumbnail(updatedSong, _allSongs);
            // Notify that Artists and Albums might have changed (though manager handles internal property changes)
            OnPropertyChanged(nameof(Artists));
            OnPropertyChanged(nameof(Albums));
        });
    }


    private void ExecutePreviousTrack(object? parameter)
    {
        if (SelectedSong is null || !FilteredSongs.Any())
        {
            return;
        }

        int currentIndex = FilteredSongs.IndexOf(SelectedSong);

        if (currentIndex > 0)
        {
            SelectedSong = FilteredSongs[currentIndex - 1];
            Debug.WriteLine($"[LibraryVM] Moved to previous track: {SelectedSong.Title}");
        }
        else
        {
            Debug.WriteLine("[LibraryVM] Already at the first track.");
        }
    }

    private bool CanExecutePreviousTrack(object? parameter)
    {
        if (SelectedSong == null || !FilteredSongs.Any()) return false;
        return FilteredSongs.IndexOf(SelectedSong) > 0;
    }

    private void ExecuteNextTrack(object? parameter)
    {
        if (SelectedSong is null || !FilteredSongs.Any())
        {
            return;
        }

        int currentIndex = FilteredSongs.IndexOf(SelectedSong);
        if (currentIndex < FilteredSongs.Count - 1 && currentIndex != -1)
        {
            SelectedSong = FilteredSongs[currentIndex + 1];
            Debug.WriteLine($"[LibraryVM] Moved to next track: {SelectedSong.Title}");
        }
        else if (currentIndex != -1)
        {
            Debug.WriteLine("[LibraryVM] Already at the last track.");
        }
        else
        {
            Debug.WriteLine("[LibraryVM] Selected song not found in filtered list.");
        }
    }

    private bool CanExecuteNextTrack(object? parameter)
    {
        if (SelectedSong is null || !FilteredSongs.Any())
        {
            return false;
        }

        int currentIndex = FilteredSongs.IndexOf(SelectedSong);

        return currentIndex != -1 && currentIndex < FilteredSongs.Count - 1;
    }

    public async Task LoadLibraryAsync()
    {
        if (IsLoadingLibrary)
        {
            return;
        }

        IsLoadingLibrary = true;
        SearchQuery = string.Empty;
        SelectedArtist = null;
        SelectedAlbum = null;

        await Dispatcher.UIThread.InvokeAsync(() => {
            SelectedSong = null;
            // Clearing is now handled by ArtistAlbumManager internally if needed,
            // or here if we want to ensure they are visually cleared before population starts.
            // Artists.Clear(); 
            // Albums.Clear();
            FilteredSongs.Clear();
            _allSongs.Clear();
            LibraryStatusText = "Preparing to load music...";
        });

        AppSettings settings = _settingsService.LoadSettings();

        if (settings.MusicDirectories.Count == 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() => {
                LibraryStatusText = "No music directories configured.";
            });
        }
        else
        {
            try
            {
                await Task.Run(async () => {
                    await _musicLibraryService.LoadMusicFromDirectoriesAsync(
                        settings.MusicDirectories,
                        song => Dispatcher.UIThread.InvokeAsync(() => _allSongs.Add(song)),
                        s => Dispatcher.UIThread.InvokeAsync(() => LibraryStatusText = s));
                });

                await Dispatcher.UIThread.InvokeAsync(() => {
                    // Delegate population to the manager
                    _artistAlbumManager.PopulateCollections(_allSongs);
                    OnPropertyChanged(nameof(Artists)); // Notify UI that Artists collection has been repopulated
                    OnPropertyChanged(nameof(Albums));  // Notify UI that Albums collection has been repopulated

                    ApplyFilter();
                    UpdateStatusBarText();
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LibraryVM] Error loading library: {ex}");
                await Dispatcher.UIThread.InvokeAsync(() => LibraryStatusText = "Error loading music library.");
            }
        }
        IsLoadingLibrary = false;
    }

    private void OnArtistSelected(ArtistViewModel artist)
    {
        if (artist?.Name == null)
        {
            return;
        }

        Debug.WriteLine($"[LibraryVM] Artist selected: {artist.Name}");
        SelectedAlbum = null;
        SearchQuery = artist.Name;
        _parentViewModel.ActiveTabIndex = 0;
    }

    private void OnAlbumSelected(AlbumViewModel album)
    {
        if (album?.Title == null || album.Artist == null)
        {
            return;
        }

        Debug.WriteLine($"[LibraryVM] Album selected: {album.Title} by {album.Artist}");
        SelectedArtist = null;
        SearchQuery = album.Title;
        _parentViewModel.ActiveTabIndex = 0;
    }

    private void ApplyFilter()
    {
        FilteredSongs.Clear();
        IEnumerable<Song> songsToFilter = _allSongs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            string query = SearchQuery.ToLowerInvariant().Trim();

            songsToFilter = songsToFilter.Where(s =>
                (s.Title?.ToLowerInvariant().Contains(query, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                (s.Artist?.ToLowerInvariant().Contains(query, StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                (s.Album?.ToLowerInvariant().Contains(query, StringComparison.InvariantCultureIgnoreCase) ?? false));
        }

        songsToFilter = songsToFilter.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var song in songsToFilter)
        {
            FilteredSongs.Add(song);
        }

        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            Debug.WriteLine($"[LibraryVM] Selected song '{SelectedSong.Title}' is no longer in the filtered list. Clearing selection.");
            SelectedSong = null;
        }
        else if (SelectedSong != null && FilteredSongs.Contains(SelectedSong))
        {
            RaiseNavigationCommandsCanExecuteChanged();
        }

        UpdateStatusBarText();
        RaiseNavigationCommandsCanExecuteChanged();
    }

    public void UpdateStatusBarText()
    {
        if (IsLoadingLibrary)
        {
            return;
        }

        string status;

        if (_allSongs.Count == 0)
        {
            AppSettings settings = _settingsService.LoadSettings();

            if (!settings.MusicDirectories.Any())
            {
                status = "Library empty. Add directories via File menu.";
            }
            else
            {
                status = "No songs found in configured directories.";
            }
        }
        else if (SelectedArtist != null && SearchQuery == SelectedArtist.Name)
        {
            status = $"Showing songs by {SelectedArtist.Name}: {FilteredSongs.Count} of {_allSongs.Count} total songs.";
        }
        else if (SelectedAlbum != null && SearchQuery == SelectedAlbum.Title)
        {
            status = $"Showing songs from {SelectedAlbum.Title} by {SelectedAlbum.Artist}: {FilteredSongs.Count} of {_allSongs.Count} total songs.";
        }
        else if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            status = $"{FilteredSongs.Count} of {_allSongs.Count} songs matching search.";
        }
        else
        {
            status = $"{_allSongs.Count} songs in library.";
        }

        LibraryStatusText = status;
    }


    public void RaiseLibraryCommandsCanExecuteChanged()
    {
        (SetDisplayModeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void RaiseNavigationCommandsCanExecuteChanged()
    {
        (PreviousTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_musicLibraryService != null)
        {
            _musicLibraryService.SongThumbnailUpdated -= MusicLibraryService_SongThumbnailUpdated;
        }
    }
}