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

public enum SongDisplayMode
{
    Detailed,
    Compact,
    Grid
}

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

    private SongDisplayMode _currentSongDisplayMode = SongDisplayMode.Detailed;
    public SongDisplayMode CurrentSongDisplayMode
    {
        get => _currentSongDisplayMode;
        set => SetProperty(ref _currentSongDisplayMode, value); // This will trigger UI update for ItemTemplate via MainWindow listener
    }

    // Commands owned by LibraryViewModel
    public ICommand SetDisplayModeCommand { get; }


    public LibraryViewModel(SettingsService settingsService, MusicLibraryService musicLibraryService, LoopDataService loopDataService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        _loopDataService = loopDataService;

        SetDisplayModeCommand = new RelayCommand(
            mode => CurrentSongDisplayMode = (SongDisplayMode)mode!,
            _ => true // Always executable
        );

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
                    Bitmap? defaultSongThumbnail = _musicLibraryService.GetDefaultThumbnail();
                    foreach (var artistName in uniqueArtistNames)
                    {
                        Bitmap? repThumb = _allSongs.FirstOrDefault(s => (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false) && s.Thumbnail != null)?.Thumbnail ?? defaultSongThumbnail;
                        Artists.Add(new ArtistViewModel { Name = artistName, Thumbnail = repThumb });
                    }
                    OnPropertyChanged(nameof(Artists)); // Notify UI Artist list changed

                    // Populate Albums
                    Albums.Clear();
                    Func<Song, (string Album, string Artist)> keySelector = s => (s.Album?.Trim() ?? string.Empty, s.Artist?.Trim() ?? string.Empty);
                    var uniqueAlbumsData = _allSongs
                        .Where(s => !string.IsNullOrWhiteSpace(s.Album) && !string.IsNullOrWhiteSpace(s.Artist))
                        .GroupBy(keySelector, AlbumArtistTupleComparer.Instance)
                        .Select(g => new
                        {
                            AlbumTitle = g.Key.Item1,
                            ArtistName = g.Key.Item2,
                            SongsInAlbum = g.ToList() // Get all songs for this album grouping
                        })
                        .OrderBy(a => a.ArtistName, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.AlbumTitle, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var albumData in uniqueAlbumsData)
                    {
                        var albumVM = new AlbumViewModel
                        {
                            Title = albumData.AlbumTitle,
                            Artist = albumData.ArtistName
                        };

                        var songThumbnailsForGrid = new List<Bitmap?>(new Bitmap?[4]); // Initialize with 4 nulls
                        var distinctSongThumbs = albumData.SongsInAlbum
                                                     .Select(s => s.Thumbnail ?? defaultSongThumbnail)
                                                     // .Where(t => t != null) // defaultSongThumbnail should ensure non-null
                                                     .Distinct() // Ensure distinct thumbnails if multiple songs share same art
                                                     .Take(4)
                                                     .ToList();

                        for (int i = 0; i < distinctSongThumbs.Count; i++)
                        {
                            songThumbnailsForGrid[i] = distinctSongThumbs[i];
                        }
                        albumVM.SongThumbnailsForGrid = songThumbnailsForGrid;

                        albumVM.RepresentativeThumbnail = songThumbnailsForGrid[0] ?? defaultSongThumbnail;

                        Albums.Add(albumVM);
                    }
                    OnPropertyChanged(nameof(Albums));

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
        if (artist?.Name == null) return;
        Debug.WriteLine($"[LibraryVM] Artist selected: {artist.Name}");
        SearchQuery = artist.Name;
    }

    private void OnAlbumSelected(AlbumViewModel album)
    {
        if (album?.Title == null || album.Artist == null) return;
        Debug.WriteLine($"[LibraryVM] Album selected: {album.Title} by {album.Artist}");
        SearchQuery = string.Empty;

        FilteredSongs.Clear();
        var songsInAlbum = _allSongs.Where(s =>
            s.Album.Equals(album.Title, StringComparison.OrdinalIgnoreCase) &&
            s.Artist.Equals(album.Artist, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var song in songsInAlbum)
        {
            FilteredSongs.Add(song);
        }
        UpdateStatusBarText();
    }

    private void ApplyFilter()
    {
        FilteredSongs.Clear();
        var songsToFilter = _allSongs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant().Trim();
            songsToFilter = songsToFilter.Where(s =>
                (s.Title?.ToLowerInvariant().Contains(query) ?? false) ||
                (s.Artist?.ToLowerInvariant().Contains(query) ?? false) ||
                (s.Album?.ToLowerInvariant().Contains(query) ?? false));
        }

        songsToFilter = songsToFilter.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var song in songsToFilter)
        {
            FilteredSongs.Add(song);
        }
        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            SelectedSong = null;
        }
        UpdateStatusBarText();
    }

    public void UpdateStatusBarText()
    {
        if (IsLoadingLibrary) return;

        string status;
        if (_allSongs.Count == 0)
        {
            var settings = _settingsService.LoadSettings();
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
            status = $"{FilteredSongs.Count} of {_allSongs.Count} songs displayed.";
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
}