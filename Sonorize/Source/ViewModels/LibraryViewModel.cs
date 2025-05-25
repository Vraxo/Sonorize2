using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.LibraryManagement;

namespace Sonorize.ViewModels;

public class LibraryViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly LoopDataService _loopDataService;
    private readonly MainWindowViewModel _parentViewModel;
    private readonly ArtistAlbumCollectionManager _artistAlbumManager;
    private readonly SongFilteringService _songFilteringService;
    private readonly LibraryStatusTextGenerator _statusTextGenerator;
    private readonly LibraryDataOrchestrator _libraryDataOrchestrator;
    private readonly TrackNavigationManager _trackNavigationManager; // Added manager

    private readonly ObservableCollection<Song> _allSongs = [];

    public ObservableCollection<Song> FilteredSongs { get; } = [];
    public ObservableCollection<ArtistViewModel> Artists { get; } = [];
    public ObservableCollection<AlbumViewModel> Albums { get; } = [];

    public ICommand SetDisplayModeCommand { get; }
    // Navigation commands are now exposed from TrackNavigationManager
    public ICommand PreviousTrackCommand => _trackNavigationManager.PreviousTrackCommand;
    public ICommand NextTrackCommand => _trackNavigationManager.NextTrackCommand;

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
                // Navigation command CanExecute is now handled by TrackNavigationManager
            }

            Debug.WriteLine($"[LibraryVM] SelectedSong changed to: {value?.Title ?? "null"}");
            _trackNavigationManager.UpdateSelectedSong(value); // Inform the manager
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
                ApplyFilter();
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

            if (value != null)
            {
                OnAlbumSelected(value);
            }
            else
            {
                ApplyFilter();
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
        get => _libraryStatusText;
        private set => SetProperty(ref _libraryStatusText, value);
    }
    private string _libraryStatusText = "";


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

        FilteredSongs = new ObservableCollection<Song>(); // Ensure it's initialized before passing
        _trackNavigationManager = new TrackNavigationManager(FilteredSongs);
        // If TrackNavigationManager needs to change LibraryViewModel.SelectedSong, subscribe to an event from it:
        // _trackNavigationManager.ManagedSelectionChanged += (newSelection) => SelectedSong = newSelection;


        _artistAlbumManager = new ArtistAlbumCollectionManager(Artists, Albums, _musicLibraryService);
        _songFilteringService = new SongFilteringService();
        _statusTextGenerator = new LibraryStatusTextGenerator();
        _libraryDataOrchestrator = new LibraryDataOrchestrator(_musicLibraryService, _artistAlbumManager, _settingsService);

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

        // Previous/Next track commands are now handled by _trackNavigationManager
        // FilteredSongs.CollectionChanged still needs to inform TrackNavigationManager
        // This is handled inside TrackNavigationManager's constructor.

        UpdateStatusBarText();
    }

    private async void MusicLibraryService_SongThumbnailUpdated(Song updatedSong)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _artistAlbumManager.UpdateCollectionsForSongThumbnail(updatedSong, _allSongs);
            OnPropertyChanged(nameof(Artists));
            OnPropertyChanged(nameof(Albums));
        });
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
        // SelectedSong will be set to null as part of ApplyFilter if it's no longer in the filtered list,
        // or explicitly set to null here if we want to ensure it's cleared before loading.
        SelectedSong = null; // Explicitly clear selection before load

        await Dispatcher.UIThread.InvokeAsync(() => {
            // SelectedSong already cleared
            _allSongs.Clear();
            FilteredSongs.Clear(); // TrackNavigationManager will see this change
            Artists.Clear();
            Albums.Clear();
            LibraryStatusText = "Preparing to load music...";
        });

        Action<Song> songAddedCallback = song => _allSongs.Add(song);
        Action<string> statusUpdateCallback = status => LibraryStatusText = status;

        await _libraryDataOrchestrator.LoadAndProcessLibraryDataAsync(statusUpdateCallback, songAddedCallback);

        await Dispatcher.UIThread.InvokeAsync(() => {
            OnPropertyChanged(nameof(Artists));
            OnPropertyChanged(nameof(Albums));
            ApplyFilter();
        });

        IsLoadingLibrary = false;
        UpdateStatusBarText();
    }

    private void OnArtistSelected(ArtistViewModel artist)
    {
        if (artist?.Name is null)
        {
            return;
        }

        Debug.WriteLine($"[LibraryVM] Artist selected: {artist.Name}");

        SelectedAlbum = null;
        OnPropertyChanged(nameof(SelectedAlbum));
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
        OnPropertyChanged(nameof(SelectedArtist));
        SearchQuery = album.Title;

        _parentViewModel.ActiveTabIndex = 0;
    }

    private void ApplyFilter()
    {
        var currentSelectedSongBeforeFilter = SelectedSong; // Preserve current selection

        FilteredSongs.Clear(); // This will notify TrackNavigationManager
        var filtered = _songFilteringService.ApplyFilter(_allSongs, SearchQuery, SelectedArtist, SelectedAlbum);
        foreach (var song in filtered)
        {
            FilteredSongs.Add(song); // This will notify TrackNavigationManager
        }

        // Restore selection if still valid, or clear it
        if (currentSelectedSongBeforeFilter != null && FilteredSongs.Contains(currentSelectedSongBeforeFilter))
        {
            // If the setter for SelectedSong doesn't re-notify _trackNavigationManager when value is the same,
            // we might need to manually ensure _trackNavigationManager is synced.
            // However, our current SelectedSong setter will re-notify if `value` is different from `_selectedSong`.
            // If `_selectedSong` was already `currentSelectedSongBeforeFilter`, no notification happens.
            // So, we need to explicitly update the manager if the song instance is the same but list context changed.
            SelectedSong = currentSelectedSongBeforeFilter; // Ensure TrackNavigationManager is aware
        }
        else if (currentSelectedSongBeforeFilter != null) // Was selected, but no longer in list
        {
            Debug.WriteLine($"[LibraryVM] Selected song '{currentSelectedSongBeforeFilter.Title}' is no longer in the filtered list. Clearing selection.");
            SelectedSong = null;
        }
        else // Was not selected, or selection was cleared
        {
            // Ensure TrackNavigationManager knows selection is null if it wasn't already
            if (SelectedSong != null) SelectedSong = null;
            else _trackNavigationManager.UpdateSelectedSong(null);
        }
        UpdateStatusBarText();
    }

    public void UpdateStatusBarText()
    {
        if (!IsLoadingLibrary)
        {
            LibraryStatusText = _statusTextGenerator.GenerateStatusText(
                IsLoadingLibrary,
                _allSongs.Count,
                FilteredSongs.Count,
                SelectedArtist,
                SelectedAlbum,
                SearchQuery,
                _settingsService
            );
        }
    }


    public void RaiseLibraryCommandsCanExecuteChanged()
    {
        (SetDisplayModeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // Navigation commands are handled by TrackNavigationManager
    }

    // This method is no longer strictly needed here if TrackNavigationManager handles its own commands
    // public void RaiseNavigationCommandsCanExecuteChanged()
    // {
    //     _trackNavigationManager.RaiseCanExecuteChangedForAllCommands();
    // }

    public void Dispose()
    {
        if (_musicLibraryService != null)
        {
            _musicLibraryService.SongThumbnailUpdated -= MusicLibraryService_SongThumbnailUpdated;
        }
        // If TrackNavigationManager subscribed to events or needs disposal, handle here
        // e.g., _trackNavigationManager.Dispose(); (if it implements IDisposable)
    }
}