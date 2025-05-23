using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Sonorize.ViewModels;

public class LibraryViewModel : ViewModelBase // Ensure this is public
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly LoopDataService _loopDataService; // Needed to load loops during scan

    private readonly ObservableCollection<Song> _allSongs = new();
    public ObservableCollection<Song> FilteredSongs { get; } = new();
    public ObservableCollection<ArtistViewModel> Artists { get; } = new();
    public ObservableCollection<AlbumViewModel> Albums { get; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery { get => _searchQuery; set { if (SetProperty(ref _searchQuery, value)) ApplyFilter(); } }

    private Song? _selectedSongInternal;
    /// <summary>
    /// Represents the currently selected song in the library list.
    /// MainWindowViewModel should subscribe to changes on this property.
    /// </summary>
    public Song? SelectedSong
    {
        get => _selectedSongInternal;
        set
        {
            if (SetProperty(ref _selectedSongInternal, value))
            {
                // The MainWindowViewModel will handle the actual playback when this changes.
                Debug.WriteLine($"[LibraryVM] SelectedSong changed to: {value?.Title ?? "null"}");
                // No need to call HandleSelectedSongChange here, MainVM will react.
            }
        }
    }

    private ArtistViewModel? _selectedArtist;
    public ArtistViewModel? SelectedArtist
    {
        get => _selectedArtist;
        set
        {
            if (SetProperty(ref _selectedArtist, value))
            {
                if (value != null)
                {
                    OnArtistSelected(value);
                }
                else
                {
                    // If artist selection is cleared, reset filter
                    SearchQuery = string.Empty;
                    ApplyFilter();
                }
            }
        }
    }

    private AlbumViewModel? _selectedAlbum;
    public AlbumViewModel? SelectedAlbum
    {
        get => _selectedAlbum;
        set
        {
            if (SetProperty(ref _selectedAlbum, value))
            {
                if (value != null)
                {
                    OnAlbumSelected(value);
                }
                else
                {
                    // If album selection is cleared, reset filter
                    SearchQuery = string.Empty;
                    ApplyFilter();
                }
            }
        }
    }

    private bool _isLoadingLibrary = false;
    public bool IsLoadingLibrary
    {
        get => _isLoadingLibrary;
        private set { if (SetProperty(ref _isLoadingLibrary, value)) RaiseLibraryCommandsCanExecuteChanged(); }
    }

    private string _libraryStatusText = "";
    /// <summary>
    /// Status text related to the library loading or song counts.
    /// </summary>
    public string LibraryStatusText { get => _libraryStatusText; private set => SetProperty(ref _libraryStatusText, value); }

    // --- Library View Mode Properties ---
    private LibraryViewMode _currentViewMode = LibraryViewMode.Detailed; // Default view mode
    public LibraryViewMode CurrentViewMode
    {
        get => _currentViewMode;
        set => SetProperty(ref _currentViewMode, value);
    }

    // Collection for the ComboBox binding
    public List<LibraryViewMode> AvailableViewModes { get; } = Enum.GetValues(typeof(LibraryViewMode)).Cast<LibraryViewMode>().ToList();


    // Commands owned by LibraryViewModel
    // AddDirectoryAndRefreshCommand is kept on MainWindowViewModel because it needs Window owner for file picker.
    // LoadInitialDataCommand is kept on MainWindowViewModel as it's a top-level initialization action.
    // OpenSettingsCommand is kept on MainWindowViewModel as it opens a modal window.


    public LibraryViewModel(SettingsService settingsService, MusicLibraryService musicLibraryService, LoopDataService loopDataService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        _loopDataService = loopDataService;

        // Initial state
        UpdateStatusBarText(); // Initialize status text
    }

    /// <summary>
    /// Loads the music library from configured directories. Designed to be called by the MainWindowViewModel.
    /// </summary>
    public async Task LoadLibraryAsync()
    {
        if (IsLoadingLibrary) return;

        IsLoadingLibrary = true;
        SearchQuery = string.Empty; // Clear search when reloading library
        SelectedArtist = null; // Clear artist selection
        SelectedAlbum = null; // Clear album selection

        await Dispatcher.UIThread.InvokeAsync(() => {
            SelectedSong = null;
            Artists.Clear();
            Albums.Clear();
            FilteredSongs.Clear();
            _allSongs.Clear();
            LibraryStatusText = "Preparing to load music...";
            // MainWindowViewModel will update its overall status text
        });

        var settings = _settingsService.LoadSettings();
        if (!settings.MusicDirectories.Any())
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
                        s => Dispatcher.UIThread.InvokeAsync(() => LibraryStatusText = s)); // Update status text here
                });

                await Dispatcher.UIThread.InvokeAsync(() => {
                    // Populate Artists
                    Artists.Clear();
                    var uniqueArtistNames = _allSongs
                        .Where(s => !string.IsNullOrWhiteSpace(s.Artist))
                        .Select(s => s.Artist!) // We filtered out null/whitespace
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    Bitmap? defaultThumb = _musicLibraryService.GetDefaultThumbnail();
                    foreach (var artistName in uniqueArtistNames)
                    {
                        Bitmap? repThumb = _allSongs.FirstOrDefault(s => (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false) && s.Thumbnail != null)?.Thumbnail ?? defaultThumb;
                        Artists.Add(new ArtistViewModel { Name = artistName, Thumbnail = repThumb });
                    }
                    OnPropertyChanged(nameof(Artists)); // Notify UI Artist list changed

                    // Populate Albums
                    Albums.Clear();
                    Func<Song, (string Album, string Artist)> keySelector = s => (s.Album?.Trim() ?? string.Empty, s.Artist?.Trim() ?? string.Empty);
                    var uniqueAlbums = _allSongs
                        .Where(s => !string.IsNullOrWhiteSpace(s.Album) && !string.IsNullOrWhiteSpace(s.Artist))
                        .GroupBy(keySelector, AlbumArtistTupleComparer.Instance)
                        .Select(g => new { AlbumTitle = g.Key.Item1, ArtistName = g.Key.Item2, ThumbSong = g.FirstOrDefault(s => s.Thumbnail != null) })
                        .OrderBy(a => a.ArtistName, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.AlbumTitle, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    foreach (var albumData in uniqueAlbums)
                    {
                        Albums.Add(new AlbumViewModel { Title = albumData.AlbumTitle, Artist = albumData.ArtistName, Thumbnail = albumData.ThumbSong?.Thumbnail ?? defaultThumb });
                    }
                    OnPropertyChanged(nameof(Albums)); // Notify UI Album list changed

                    ApplyFilter(); // Apply initial filter (empty search)

                    // Final status update
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
        // MainWindowViewModel will update its overall status text
    }

    private void OnArtistSelected(ArtistViewModel artist)
    {
        if (artist?.Name == null) return;
        Debug.WriteLine($"[LibraryVM] Artist selected: {artist.Name}");
        // Clear album selection when artist is selected
        SelectedAlbum = null;
        SearchQuery = artist.Name; // Set search query to filter songs
        // MainWindowViewModel will handle switching tabs if needed
    }

    private void OnAlbumSelected(AlbumViewModel album)
    {
        if (album?.Title == null || album.Artist == null) return;
        Debug.WriteLine($"[LibraryVM] Album selected: {album.Title} by {album.Artist}");
        // Clear artist selection when album is selected
        SelectedArtist = null;
        // When an album is selected, clear general search and filter specifically by album/artist
        SearchQuery = string.Empty; // Clear general search

        FilteredSongs.Clear();
        var songsInAlbum = _allSongs.Where(s =>
            s.Album.Equals(album.Title, StringComparison.OrdinalIgnoreCase) &&
            s.Artist.Equals(album.Artist, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase); // Always sort by title within an album

        foreach (var song in songsInAlbum)
        {
            FilteredSongs.Add(song);
        }
        UpdateStatusBarText(); // Update status with filtered count
        // MainWindowViewModel will handle switching tabs if needed
    }

    private void ApplyFilter()
    {
        FilteredSongs.Clear();
        var songsToFilter = _allSongs.AsEnumerable();

        // Check if an Artist or Album filter is active
        if (SelectedArtist != null && !string.IsNullOrWhiteSpace(SelectedArtist.Name))
        {
            // Filter by selected artist (already handled by OnArtistSelected setting SearchQuery)
            // Nothing needed here if OnArtistSelected correctly sets SearchQuery and calls ApplyFilter
        }
        else if (SelectedAlbum != null && !string.IsNullOrWhiteSpace(SelectedAlbum.Title) && !string.IsNullOrWhiteSpace(SelectedAlbum.Artist))
        {
            // Filter by selected album (already handled by OnAlbumSelected calling ApplyFilter with specific logic)
            // Nothing needed here if OnAlbumSelected correctly filters and calls ApplyFilter
        }
        else if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            // Apply general search filter only if no specific artist/album is selected
            var query = SearchQuery.ToLowerInvariant().Trim();
            songsToFilter = songsToFilter.Where(s =>
                (s.Title?.ToLowerInvariant().Contains(query) ?? false) ||
                (s.Artist?.ToLowerInvariant().Contains(query) ?? false) ||
                (s.Album?.ToLowerInvariant().Contains(query) ?? false));
        }
        // Else: No filter applied, all songs are candidates

        // Sorting for the main song list (Library tab) - Sort by Title unless album view is active
        // When an album is selected, sorting by title is done in OnAlbumSelected before adding to FilteredSongs.
        if (SelectedAlbum == null)
        {
            songsToFilter = songsToFilter.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);
        }
        // If album is selected, songs are already sorted in OnAlbumSelected

        // Add filtered/sorted songs to the ObservableCollection
        foreach (var song in songsToFilter)
        {
            FilteredSongs.Add(song);
        }

        // If the previously selected song is no longer in the filtered list, clear the selection
        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            SelectedSong = null;
        }
        UpdateStatusBarText(); // Update status with filtered count
    }

    /// <summary>
    /// Updates the status text displayed, primarily showing song counts.
    /// </summary>
    public void UpdateStatusBarText()
    {
        if (IsLoadingLibrary) return; // Status text set by loading process

        string status;
        if (_allSongs.Count == 0)
        {
            var settings = _settingsService.LoadSettings(); // Need settings to check for configured dirs
            if (!settings.MusicDirectories.Any())
            {
                status = "Library empty. Add directories via File menu.";
            }
            else
            {
                status = "No songs found in configured directories.";
            }
        }
        else if (!string.IsNullOrWhiteSpace(SearchQuery) || SelectedAlbum != null || SelectedArtist != null)
        {
            // Refine this to be more specific based on the active filter type
            string filterType = "filtered";
            if (!string.IsNullOrWhiteSpace(SearchQuery) && SelectedArtist == null && SelectedAlbum == null) filterType = "matching search";
            else if (SelectedArtist != null) filterType = $"by {SelectedArtist.Name}";
            else if (SelectedAlbum != null) filterType = $"in {SelectedAlbum.Title} by {SelectedAlbum.Artist}";

            status = $"{FilteredSongs.Count} of {_allSongs.Count} songs {filterType} displayed.";
        }
        else
        {
            status = $"{_allSongs.Count} songs in library.";
        }
        LibraryStatusText = status;
    }


    /// <summary>
    /// Raises CanExecuteChanged for commands owned by this ViewModel.
    /// Designed to be called by the MainWindowViewModel if needed.
    /// </summary>
    public void RaiseLibraryCommandsCanExecuteChanged()
    {
        // No commands directly on this VM yet that need external triggering besides init state
        // If we added commands like "Reload Library", they would go here.
    }
}