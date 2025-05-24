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

public class LibraryViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly LoopDataService _loopDataService;

    private readonly ObservableCollection<Song> _allSongs = new();
    public ObservableCollection<Song> FilteredSongs { get; } = new();
    public ObservableCollection<ArtistViewModel> Artists { get; } = new();
    public ObservableCollection<AlbumViewModel> Albums { get; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery { get => _searchQuery; set { if (SetProperty(ref _searchQuery, value)) ApplyFilter(); } }

    private Song? _selectedSongInternal;
    public Song? SelectedSong
    {
        get => _selectedSongInternal;
        set
        {
            if (SetProperty(ref _selectedSongInternal, value))
            {
                Debug.WriteLine($"[LibraryVM] SelectedSong changed to: {value?.Title ?? "null"}");
                // Raise CanExecuteChanged for navigation commands when selection changes
                RaiseNavigationCommandsCanExecuteChanged();
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
    public string LibraryStatusText { get => _libraryStatusText; private set => SetProperty(ref _libraryStatusText, value); }

    private SongDisplayMode _libraryViewMode;
    public SongDisplayMode LibraryViewMode
    {
        get => _libraryViewMode;
        set
        {
            if (SetProperty(ref _libraryViewMode, value))
            {
                var settings = _settingsService.LoadSettings();
                settings.LibraryViewModePreference = value.ToString();
                _settingsService.SaveSettings(settings);
            }
        }
    }

    private SongDisplayMode _artistViewMode;
    public SongDisplayMode ArtistViewMode
    {
        get => _artistViewMode;
        set
        {
            if (SetProperty(ref _artistViewMode, value))
            {
                var settings = _settingsService.LoadSettings();
                settings.ArtistViewModePreference = value.ToString();
                _settingsService.SaveSettings(settings);
            }
        }
    }

    private SongDisplayMode _albumViewMode;
    public SongDisplayMode AlbumViewMode
    {
        get => _albumViewMode;
        set
        {
            if (SetProperty(ref _albumViewMode, value))
            {
                var settings = _settingsService.LoadSettings();
                settings.AlbumViewModePreference = value.ToString();
                _settingsService.SaveSettings(settings);
            }
        }
    }

    public ICommand SetDisplayModeCommand { get; }

    // Navigation Commands
    public ICommand PreviousTrackCommand { get; }
    public ICommand NextTrackCommand { get; }


    public LibraryViewModel(SettingsService settingsService, MusicLibraryService musicLibraryService, LoopDataService loopDataService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        _loopDataService = loopDataService;

        // Load preferences
        var appSettings = _settingsService.LoadSettings();
        _libraryViewMode = Enum.TryParse<SongDisplayMode>(appSettings.LibraryViewModePreference, out var libMode) ? libMode : SongDisplayMode.Detailed;
        _artistViewMode = Enum.TryParse<SongDisplayMode>(appSettings.ArtistViewModePreference, out var artMode) ? artMode : SongDisplayMode.Detailed;
        _albumViewMode = Enum.TryParse<SongDisplayMode>(appSettings.AlbumViewModePreference, out var albMode) ? albMode : SongDisplayMode.Detailed;


        SetDisplayModeCommand = new RelayCommand(
            param =>
            {
                if (param is (string targetView, SongDisplayMode mode))
                {
                    switch (targetView)
                    {
                        // Setters will handle saving
                        case "Library": LibraryViewMode = mode; break;
                        case "Artists": ArtistViewMode = mode; break;
                        case "Albums": AlbumViewMode = mode; break;
                    }
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
        if (SelectedSong == null || !FilteredSongs.Any()) return;

        var currentIndex = FilteredSongs.IndexOf(SelectedSong);
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
        if (SelectedSong == null || !FilteredSongs.Any()) return;

        var currentIndex = FilteredSongs.IndexOf(SelectedSong);
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
        if (SelectedSong == null || !FilteredSongs.Any()) return false;
        var currentIndex = FilteredSongs.IndexOf(SelectedSong);
        return currentIndex != -1 && currentIndex < FilteredSongs.Count - 1;
    }


    public async Task LoadLibraryAsync()
    {
        if (IsLoadingLibrary) return;

        IsLoadingLibrary = true;
        SearchQuery = string.Empty;

        await Dispatcher.UIThread.InvokeAsync(() => {
            SelectedSong = null;
            Artists.Clear();
            Albums.Clear();
            FilteredSongs.Clear();
            _allSongs.Clear();
            LibraryStatusText = "Preparing to load music...";
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
                    foreach (var artistName in uniqueArtistNames)
                    {
                        Bitmap? repThumb = _allSongs.FirstOrDefault(s => (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false) && s.Thumbnail != null)?.Thumbnail ?? defaultSongThumbnail;
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
                        var albumVM = new AlbumViewModel
                        {
                            Title = albumData.AlbumTitle,
                            Artist = albumData.ArtistName
                        };

                        var songThumbnailsForGrid = new List<Bitmap?>(new Bitmap?[4]);
                        var distinctSongThumbs = albumData.SongsInAlbum
                                                     .Select(s => s.Thumbnail ?? defaultSongThumbnail)
                                                     .Distinct()
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
        if (artist?.Name == null) return;
        Debug.WriteLine($"[LibraryVM] Artist selected: {artist.Name}");
        SearchQuery = artist.Name;
        // ApplyFilter will be called by SearchQuery setter, which updates FilteredSongs
    }

    private void OnAlbumSelected(AlbumViewModel album)
    {
        if (album?.Title == null || album.Artist == null) return;
        Debug.WriteLine($"[LibraryVM] Album selected: {album.Title} by {album.Artist}");
        SearchQuery = string.Empty; // Clear search query when selecting album

        FilteredSongs.Clear();
        var songsInAlbum = _allSongs.Where(s =>
            s.Album.Equals(album.Title, StringComparison.OrdinalIgnoreCase) &&
            s.Artist.Equals(album.Artist, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var song in songsInAlbum)
        {
            FilteredSongs.Add(song);
        }
        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            SelectedSong = null; // Clear selection if the previously selected song is not in this album
        }
        UpdateStatusBarText();
        RaiseNavigationCommandsCanExecuteChanged(); // FilteredSongs changed
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
        // If the currently selected song is no longer in the filtered list, clear selection.
        // This is important for playback state synchronization.
        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            Debug.WriteLine($"[LibraryVM] Selected song '{SelectedSong.Title}' is no longer in the filtered list. Clearing selection.");
            SelectedSong = null; // Clearing SelectedSong will trigger its PropertyChanged handler
        }
        else if (SelectedSong != null && FilteredSongs.Contains(SelectedSong))
        {
            // If the selected song is still in the list, its index might have changed.
            // Need to re-evaluate navigation commands.
            RaiseNavigationCommandsCanExecuteChanged();
        }
        // If SelectedSong is null, this filter application didn't select one,
        // and RaiseNavigationCommandsCanExecuteChanged is called because FilteredSongs changed.

        UpdateStatusBarText();
        // Navigation commands also depend on FilteredSongs changing
        RaiseNavigationCommandsCanExecuteChanged();
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
        // Navigation commands are handled by RaiseNavigationCommandsCanExecuteChanged
    }

    public void RaiseNavigationCommandsCanExecuteChanged()
    {
        //Debug.WriteLine("[LibraryVM] Raising navigation command CanExecute changed.");
        (PreviousTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (NextTrackCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}