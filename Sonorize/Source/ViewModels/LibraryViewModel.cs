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

    // _allSongs must now hold the master list of songs that MusicLibraryService will populate
    private readonly ObservableCollection<Song> _allSongs = [];

    public ObservableCollection<Song> FilteredSongs { get; } = [];
    public ObservableCollection<ArtistViewModel> Artists { get; } = [];
    public ObservableCollection<AlbumViewModel> Albums { get; } = [];

    public ICommand SetDisplayModeCommand { get; }
    public ICommand PreviousTrackCommand { get; }
    public ICommand NextTrackCommand { get; }

    public event EventHandler? RequestViewModeRefresh;

    public string SearchQuery
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value)) return;
            ApplyFilter();
        }
    } = string.Empty;

    public Song? SelectedSong
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value)) return;
            Debug.WriteLine($"[LibraryVM] SelectedSong changed to: {value?.Title ?? "null"}");
            // Raise CanExecuteChanged for navigation commands when selection changes
            RaiseNavigationCommandsCanExecuteChanged();
        }
    }

    public ArtistViewModel? SelectedArtist
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value)) return;

            if (value != null)
            {
                OnArtistSelected(value);
            }
            else
            {
                SearchQuery = string.Empty;
                ApplyFilter();
            }
        }
    }

    public AlbumViewModel? SelectedAlbum
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value)) return;

            if (value is not null)
            {
                OnAlbumSelected(value);
            }
            else
            {
                SearchQuery = string.Empty;
                ApplyFilter();
            }
        }
    }

    public bool IsLoadingLibrary
    {
        get => field;
        private set
        {
            if (!SetProperty(ref field, value)) return;
            RaiseLibraryCommandsCanExecuteChanged();
        }
    } = false;

    public string LibraryStatusText
    {
        get => field;
        private set => SetProperty(ref field, value);
    } = "";

    public SongDisplayMode LibraryViewMode
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value)) return;
            AppSettings settings = _settingsService.LoadSettings();
            settings.LibraryViewModePreference = value.ToString();
            _settingsService.SaveSettings(settings);
            RequestViewModeRefresh?.Invoke(this, EventArgs.Empty);
        }
    }

    public SongDisplayMode ArtistViewMode
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value)) return;
            AppSettings settings = _settingsService.LoadSettings();
            settings.ArtistViewModePreference = value.ToString();
            _settingsService.SaveSettings(settings);
            RequestViewModeRefresh?.Invoke(this, EventArgs.Empty);
        }
    }

    public SongDisplayMode AlbumViewMode
    {
        get => field;
        set
        {
            if (!SetProperty(ref field, value)) return;
            AppSettings settings = _settingsService.LoadSettings();
            settings.AlbumViewModePreference = value.ToString();
            _settingsService.SaveSettings(settings);
            RequestViewModeRefresh?.Invoke(this, EventArgs.Empty);
        }
    }

    // Added parentViewModel dependency
    public LibraryViewModel(MainWindowViewModel parentViewModel, SettingsService settingsService, MusicLibraryService musicLibraryService, LoopDataService loopDataService)
    {
        _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        _loopDataService = loopDataService;

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
                if (param is not (string targetView, SongDisplayMode mode)) return;
                switch (targetView)
                {
                    // Setters will handle saving and raising RequestViewModeRefresh
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

    private void ExecutePreviousTrack(object? parameter)
    {
        if (SelectedSong is null || !FilteredSongs.Any()) return;

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
        if (SelectedSong is null || !FilteredSongs.Any()) return;

        int currentIndex = FilteredSongs.IndexOf(SelectedSong);
        if (currentIndex != -1 && currentIndex < FilteredSongs.Count - 1)
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
        if (SelectedSong is null || !FilteredSongs.Any()) return false;
        int currentIndex = FilteredSongs.IndexOf(SelectedSong);
        return currentIndex != -1 && currentIndex < FilteredSongs.Count - 1;
    }

    public async Task LoadLibraryAsync()
    {
        if (IsLoadingLibrary) return;
        IsLoadingLibrary = true;
        SearchQuery = string.Empty; // Clear search on full reload

        await Dispatcher.UIThread.InvokeAsync(() => {
            SelectedSong = null;
            Artists.Clear();
            Albums.Clear();
            FilteredSongs.Clear();
            _allSongs.Clear(); // Clear the master list
            LibraryStatusText = "Preparing to load music...";
        });

        AppSettings settings = _settingsService.LoadSettings();

        if (settings.MusicDirectories.Count == 0)
        {
            await Dispatcher.UIThread.InvokeAsync(() => {
                LibraryStatusText = "No music directories configured.";
            });
            IsLoadingLibrary = false; // Exit loading state if no directories
            return;
        }

        try
        {
            // Use a callback that adds songs to _allSongs on the UI thread
            // This callback ensures songs are added one by one as they are processed
            Action<Song> songAddedToList = (song) =>
            {
                _allSongs.Add(song);
                // Apply filter incrementally? No, better to apply once after scan.
                // Adding to _allSongs doesn't auto-update FilteredSongs.
            };

            // Call the music library service to load basic data quickly.
            // The service will call songAddedToList for each song with default thumbnail.
            // It will call thumbnailLoadingStartCallback with the list of ALL songs once the scan is done.
            await _musicLibraryService.LoadMusicFromDirectoriesAsync(
                settings.MusicDirectories,
                songAddedToList, // Use the callback to add to _allSongs
                s => LibraryStatusText = s, // Update status text on UI thread (via InvokeAsync in Service)
                songsForThumbnails =>
                {
                    // This callback runs on the UI thread AFTER the basic scan in Task.Run is complete
                    Debug.WriteLine($"[LibraryVM] Initial scan complete. Received {_allSongs.Count} songs. Updating Artists/Albums and starting thumbnail load...");

                    // Update Artist/Album lists and apply initial filter immediately
                    // This populates FilteredSongs, allowing the main list box to show songs quickly
                    UpdateArtistsAndAlbums();
                    ApplyFilter(); // This populates FilteredSongs
                    UpdateStatusBarText(); // Update status based on FilteredSongs/AllSongs counts

                    // Trigger background thumbnail loading *asynchronously* and *do not await it*.
                    // This lets LoadLibraryAsync finish and the UI thread remain responsive.
                    // Use _ = to suppress warning about not awaiting
                    _ = StartThumbnailLoading(songsForThumbnails);
                }
            );

            // LoadLibraryAsync is now considered "finished" once the initial metadata is loaded,
            // the UI list (_allSongs -> FilteredSongs) is populated with default thumbnails,
            // and the background thumbnail loading process has been *started*.
            // The UI will update incrementally as thumbnails load via the Song.Thumbnail PropertyChanged notifications.
            Debug.WriteLine("[LibraryVM] LoadLibraryAsync: Initial metadata load and UI population complete. Background thumbnail loading initiated.");


        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LibraryVM] Error loading library: {ex}");
            await Dispatcher.UIThread.InvokeAsync(() => LibraryStatusText = "Error loading music library.");
        }
        finally
        {
            // This is outside the try/catch to ensure it always runs.
            // However, because the thumbnail loading is fire-and-forget, IsLoadingLibrary
            // might be set to false before all thumbnails are visually loaded.
            // This is the desired behavior for perceived performance.
            IsLoadingLibrary = false;
            Debug.WriteLine("[LibraryVM] LoadLibraryAsync finished (IsLoadingLibrary set to false).");
        }
    }

    // New method to trigger thumbnail loading in MusicLibraryService
    // This is called by the callback from MusicLibraryService on the UI thread.
    private async Task StartThumbnailLoading(List<Song> songs)
    {
        // This method is already invoked on the UI thread by the callback.
        Debug.WriteLine($"[LibraryVM] StartThumbnailLoading: Triggering background thumbnail loading for {songs.Count} songs.");
        // Call the service method to handle background loading
        await _musicLibraryService.LoadThumbnailsInBackgroundAsync(songs);

        // After thumbnails are loaded (gradually over time), we might need to refresh Artist/Album lists
        // if their representative thumbnails depended on specific songs getting thumbnails.
        // Re-running UpdateArtistsAndAlbums is safe and will update the VM properties,
        // triggering UI updates for artist/album thumbnails.
        Debug.WriteLine("[LibraryVM] StartThumbnailLoading: Thumbnail loading tasks finished. Re-updating Artists and Albums lists for final thumbnails.");
        await Dispatcher.UIThread.InvokeAsync(() => UpdateArtistsAndAlbums());
    }


    private void UpdateArtistsAndAlbums()
    {
        Debug.WriteLine("[LibraryVM] Updating Artists and Albums lists...");
        // Update Artists list
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
            // Find the first song by this artist that has a non-default thumbnail (or default if none)
            Bitmap? repThumb = _allSongs.FirstOrDefault(s =>
                (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false)
                && s.Thumbnail != null // Ensure thumbnail property is not null
                && s.Thumbnail != _musicLibraryService.GetDefaultThumbnail())?.Thumbnail ?? defaultSongThumbnail;
            Artists.Add(new ArtistViewModel { Name = artistName, Thumbnail = repThumb });
        }
        OnPropertyChanged(nameof(Artists));

        // Update Albums list
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

            List<Bitmap?> songThumbnailsForGrid = new(new Bitmap?[4]);
            // Collect distinct non-default thumbnails from songs in this album
            List<Bitmap?> distinctSongThumbs = albumData.SongsInAlbum
                                         .Where(s => s.Thumbnail != null && s.Thumbnail != _musicLibraryService.GetDefaultThumbnail())
                                         .Select(s => s.Thumbnail)
                                         .Distinct()
                                         .Take(4)
                                         .ToList()!; // Filtered nulls and default above

            for (int i = 0; i < distinctSongThumbs.Count; i++)
            {
                songThumbnailsForGrid[i] = distinctSongThumbs[i];
            }
            // Fill remaining slots with default if needed
            for (int i = distinctSongThumbs.Count; i < 4; i++) // Corrected loop condition
            {
                songThumbnailsForGrid[i] = defaultSongThumbnail;
            }


            albumVM.SongThumbnailsForGrid = songThumbnailsForGrid;

            // Representative thumbnail is the first available non-default thumbnail, or the default
            albumVM.RepresentativeThumbnail = albumData.SongsInAlbum
                                                   .Where(s => s.Thumbnail != null && s.Thumbnail != _musicLibraryService.GetDefaultThumbnail())
                                                   .Select(s => s.Thumbnail)
                                                   .FirstOrDefault() ?? defaultSongThumbnail;

            Albums.Add(albumVM);
        }
        OnPropertyChanged(nameof(Albums));
        Debug.WriteLine("[LibraryVM] Artists and Albums lists updated.");
    }


    private void OnArtistSelected(ArtistViewModel artist)
    {
        if (artist?.Name == null) return;
        Debug.WriteLine($"[LibraryVM] Artist selected: {artist.Name}");
        SelectedAlbum = null; // Clear Album selection when Artist is selected
        SearchQuery = artist.Name; // Filter by artist name
        // ApplyFilter called by SearchQuery setter
        _parentViewModel.ActiveTabIndex = 0; // Switch back to the Library tab
    }

    private void OnAlbumSelected(AlbumViewModel album)
    {
        if (album?.Title == null || album.Artist == null) return;
        Debug.WriteLine($"[LibraryVM] Album selected: {album.Title} by {album.Artist}");
        SelectedArtist = null; // Clear Artist selection when Album is selected
        SearchQuery = string.Empty; // Clear search query when selecting album

        // Manually filter _allSongs based on selected album
        FilteredSongs.Clear();
        IEnumerable<Song> songsInAlbum = _allSongs.Where(s =>
            s.Album.Equals(album.Title, StringComparison.OrdinalIgnoreCase) &&
            s.Artist.Equals(album.Artist, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var song in songsInAlbum)
        {
            FilteredSongs.Add(song);
        }

        // Clear song selection if it's not in the new filtered list
        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            SelectedSong = null;
        }

        UpdateStatusBarText();
        RaiseNavigationCommandsCanExecuteChanged(); // FilteredSongs changed
        _parentViewModel.ActiveTabIndex = 0; // Switch back to the Library tab
    }

    private void ApplyFilter()
    {
        Debug.WriteLine($"[LibraryVM] ApplyFilter called with SearchQuery: '{SearchQuery}'");
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

        // Use AddRange for efficiency when adding multiple items
        var filteredList = songsToFilter.ToList();
        if (filteredList.Any())
        {
            foreach (var song in filteredList)
            {
                FilteredSongs.Add(song);
            }
        }
        Debug.WriteLine($"[LibraryVM] ApplyFilter added {FilteredSongs.Count} songs to FilteredSongs.");


        // Ensure SelectedSong is still valid after filter
        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            Debug.WriteLine($"[LibraryVM] Selected song '{SelectedSong.Title}' is no longer in the filtered list. Clearing selection.");
            SelectedSong = null;
        }

        UpdateStatusBarText();
        RaiseNavigationCommandsCanExecuteChanged();
    }

    public void UpdateStatusBarText()
    {
        // Always use LibraryStatusText from LibraryVM for status bar when loading or not playing
        // When playing, MainWindowViewModel will override with playback status
        // However, LibraryStatusText itself needs to reflect counts correctly based on load state
        string status;
        if (IsLoadingLibrary)
        {
            // MusicLibraryService updates its own status text during the quick scan via a callback,
            // which sets LibraryStatusText directly.
            status = LibraryStatusText; // Use the status set by the service callback
        }
        else if (_allSongs.Count == 0)
        {
            AppSettings settings = _settingsService.LoadSettings();
            status = settings.MusicDirectories.Count == 0 ? "Library empty. Add directories via File menu." : "No songs found in configured directories.";
        }
        else if (!string.IsNullOrWhiteSpace(SearchQuery) || SelectedAlbum != null || SelectedArtist != null)
        {
            status = $"{FilteredSongs.Count} of {_allSongs.Count} songs displayed.";
        }
        else
        {
            status = $"{_allSongs.Count} songs in library.";
        }
        LibraryStatusText = status; // Update LibraryStatusText property
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
}