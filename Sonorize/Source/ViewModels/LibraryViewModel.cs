using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using System;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.Utils;

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
    private readonly MainWindowViewModel _parentViewModel; // Reference to parent VM

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
            // Raise CanExecuteChanged for navigation commands when selection changes
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
            else
            {
                // If SelectedArtist is cleared (e.g. by selecting an album),
                // and if SearchQuery was previously set to this artist's name,
                // we might want to clear SearchQuery or let ApplyFilter run with the old query.
                // Current behavior: SearchQuery remains, ApplyFilter runs if it changed.
                // If SelectedAlbum is set, it will take precedence in terms of display.
                // For now, do not automatically clear SearchQuery here.
                // ApplyFilter(); // Only if SearchQuery didn't change but context did.
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
            else
            {
                // Similar to SelectedArtist, if SelectedAlbum is cleared.
                // ApplyFilter();
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

    // Added parentViewModel dependency
    public LibraryViewModel(MainWindowViewModel parentViewModel, SettingsService settingsService, MusicLibraryService musicLibraryService, LoopDataService loopDataService)
    {
        _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        _loopDataService = loopDataService;

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
                    // Setters will handle saving
                    case "Library": LibraryViewMode = mode; break;
                    case "Artists": ArtistViewMode = mode; break;
                    case "Albums": AlbumViewMode = mode; break;
                }
            },
            _ => true
        );

        // Initialize Navigation Commands
        PreviousTrackCommand = new RelayCommand(ExecutePreviousTrack, CanExecutePreviousTrack);
        NextTrackCommand = new RelayCommand(ExecuteNextTrack, CanExecuteNextTrack);

        // Subscribe to FilteredSongs changes to update navigation command states
        FilteredSongs.CollectionChanged += (sender, e) => RaiseNavigationCommandsCanExecuteChanged();

        UpdateStatusBarText();
    }

    private async void MusicLibraryService_SongThumbnailUpdated(Song updatedSong)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Update ArtistViewModel
            var artistVM = Artists.FirstOrDefault(a => a.Name == updatedSong.Artist);
            if (artistVM != null && artistVM.Thumbnail != updatedSong.Thumbnail) // Check if update is needed
            {
                // If this song is the representative for the artist, update the artist's thumbnail
                // This simple logic assumes the first song encountered (or its thumbnail) for an artist is representative.
                // A more robust approach might re-evaluate based on all songs by that artist.
                // For now, if the updatedSong's thumbnail is what we'd pick, update.
                var firstSongOfArtistWithThumbnail = _allSongs.FirstOrDefault(s =>
                    (s.Artist?.Equals(artistVM.Name, StringComparison.OrdinalIgnoreCase) ?? false) && s.Thumbnail != _musicLibraryService.GetDefaultThumbnail());

                if (firstSongOfArtistWithThumbnail == updatedSong || (firstSongOfArtistWithThumbnail == null && updatedSong.Thumbnail != _musicLibraryService.GetDefaultThumbnail()))
                {
                    artistVM.Thumbnail = updatedSong.Thumbnail;
                }
            }

            // Update AlbumViewModel
            var albumVM = Albums.FirstOrDefault(al => al.Title == updatedSong.Album && al.Artist == updatedSong.Artist);
            if (albumVM != null)
            {
                // Re-calculate SongThumbnailsForGrid and RepresentativeThumbnail for this album
                var songsInAlbum = _allSongs.Where(s => (s.Album?.Equals(albumVM.Title, StringComparison.OrdinalIgnoreCase) ?? false) &&
                                                        (s.Artist?.Equals(albumVM.Artist, StringComparison.OrdinalIgnoreCase) ?? false))
                                             .ToList();

                Bitmap? defaultSongThumbnail = _musicLibraryService.GetDefaultThumbnail();
                List<Bitmap?> newSongThumbnailsForGrid = new List<Bitmap?>(new Bitmap?[4]);
                List<Bitmap?> distinctSongThumbs = songsInAlbum
                                                     .Select(s => s.Thumbnail ?? defaultSongThumbnail) // Use current thumbnails
                                                     .Distinct()
                                                     .Take(4)
                                                     .ToList();

                for (int i = 0; i < distinctSongThumbs.Count; i++)
                {
                    newSongThumbnailsForGrid[i] = distinctSongThumbs[i];
                }

                albumVM.SongThumbnailsForGrid = newSongThumbnailsForGrid; // This will trigger INPC on AlbumViewModel
                albumVM.RepresentativeThumbnail = newSongThumbnailsForGrid.FirstOrDefault(t => t != null) ?? defaultSongThumbnail; // This also triggers INPC
            }
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
            // Optionally loop to the last track: SelectedSong = FilteredSongs.Last();
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
        else if (currentIndex != -1) // Already at the last track
        {
            Debug.WriteLine("[LibraryVM] Already at the last track.");
            // Optionally loop to the first track: SelectedSong = FilteredSongs.First();
        }
        else // Selected song not found in filtered list - should not happen if SelectedSong is non-null and FilteredSongs contains it
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
        SearchQuery = string.Empty; // Clear search on full reload
        SelectedArtist = null; // Clear artist selection
        SelectedAlbum = null;  // Clear album selection

        await Dispatcher.UIThread.InvokeAsync(() => {
            SelectedSong = null;
            Artists.Clear();
            Albums.Clear();
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
                    Artists.Clear();
                    var uniqueArtistNames = _allSongs
                        .Where(s => !string.IsNullOrWhiteSpace(s.Artist))
                        .Select(s => s.Artist!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    Bitmap? defaultSongThumbnail = _musicLibraryService.GetDefaultThumbnail();
                    foreach (string? artistName in uniqueArtistNames)
                    {
                        // Initial representative thumbnail for artist (might be default)
                        Bitmap? repThumb = _allSongs.FirstOrDefault(s =>
                                               (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false) &&
                                               s.Thumbnail != null && s.Thumbnail != defaultSongThumbnail)?.Thumbnail
                                           ?? defaultSongThumbnail;
                        Artists.Add(new ArtistViewModel { Name = artistName, Thumbnail = repThumb });
                    }
                    OnPropertyChanged(nameof(Artists));

                    Albums.Clear();
                    Func<Song, (string Album, string Artist)> keySelector = s => (s.Album?.Trim() ?? string.Empty, s.Artist?.Trim() ?? string.Empty);
                    var uniqueAlbumsData = _allSongs
                        .Where(s => !string.IsNullOrWhiteSpace(s.Album) && !string.IsNullOrWhiteSpace(s.Artist))
                        .GroupBy(keySelector, AlbumArtistTupleComparer.Instance)
                        .Select(g => new
                        {
                            AlbumTitle = g.Key.Item1,
                            ArtistName = g.Key.Item2,
                            SongsInAlbum = g.ToList()
                        })
                        .OrderBy(a => a.ArtistName, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.AlbumTitle, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var albumData in uniqueAlbumsData)
                    {
                        AlbumViewModel albumVM = new()
                        {
                            Title = albumData.AlbumTitle,
                            Artist = albumData.ArtistName
                        };

                        // Initial thumbnails for album grid (might be default)
                        List<Bitmap?> songThumbnailsForGrid = new(new Bitmap?[4]);
                        List<Bitmap?> distinctSongThumbs = albumData.SongsInAlbum
                                                     .Select(s => s.Thumbnail ?? defaultSongThumbnail)
                                                     .Distinct()
                                                     .Take(4)
                                                     .ToList();

                        for (int i = 0; i < distinctSongThumbs.Count; i++)
                        {
                            songThumbnailsForGrid[i] = distinctSongThumbs[i];
                        }

                        albumVM.SongThumbnailsForGrid = songThumbnailsForGrid;
                        albumVM.RepresentativeThumbnail = songThumbnailsForGrid.FirstOrDefault(t => t != null) ?? defaultSongThumbnail;

                        Albums.Add(albumVM);
                    }
                    OnPropertyChanged(nameof(Albums));

                    ApplyFilter(); // This will populate FilteredSongs and trigger RaiseNavigationCommandsCanExecuteChanged

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
        // Clear Album selection when Artist is selected
        SelectedAlbum = null;

        // Set SearchQuery. This will call ApplyFilter.
        SearchQuery = artist.Name;

        // Switch back to the Library tab (index 0)
        _parentViewModel.ActiveTabIndex = 0;
    }

    private void OnAlbumSelected(AlbumViewModel album)
    {
        if (album?.Title == null || album.Artist == null)
        {
            return;
        }

        Debug.WriteLine($"[LibraryVM] Album selected: {album.Title} by {album.Artist}");
        // Clear Artist selection when Album is selected
        SelectedArtist = null;

        // Set SearchQuery to the album's title.
        // This will populate the search bar and trigger ApplyFilter,
        // which filters songs based on this query and updates UI states.
        SearchQuery = album.Title;

        // Switch back to the Library tab (index 0)
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
            // LibraryStatusText is already being updated during loading by LoadMusicFromDirectoriesAsync callback
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
        // Check if a specific artist or album view is active implicitly by checking SelectedArtist/Album
        // AND if SearchQuery matches their name/title (which OnArtistSelected/OnAlbumSelected ensures).
        else if (SelectedArtist != null && SearchQuery == SelectedArtist.Name)
        {
            status = $"Showing songs by {SelectedArtist.Name}: {FilteredSongs.Count} of {_allSongs.Count} total songs.";
        }
        else if (SelectedAlbum != null && SearchQuery == SelectedAlbum.Title)
        {
            status = $"Showing songs from {SelectedAlbum.Title} by {SelectedAlbum.Artist}: {FilteredSongs.Count} of {_allSongs.Count} total songs.";
        }
        else if (!string.IsNullOrWhiteSpace(SearchQuery)) // General search query active
        {
            status = $"{FilteredSongs.Count} of {_allSongs.Count} songs matching search.";
        }
        else // No specific view, no search query - showing all songs
        {
            status = $"{_allSongs.Count} songs in library.";
        }

        LibraryStatusText = status;
    }


    public void RaiseLibraryCommandsCanExecuteChanged()
    {
        (SetDisplayModeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // Navigation commands are handled by RaiseNavigationCommandsCanExecuteChanged
    }

    public void RaiseNavigationCommandsCanExecuteChanged()
    {
        //Debug.WriteLine("[LibraryVM] Raising navigation command CanExecute changed.");
        (PreviousTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    // It's good practice to unsubscribe from events when the ViewModel is no longer needed,
    // though for a main library view model, this might coincide with application shutdown.
    public void Dispose()
    {
        if (_musicLibraryService != null)
        {
            _musicLibraryService.SongThumbnailUpdated -= MusicLibraryService_SongThumbnailUpdated;
        }
    }
}