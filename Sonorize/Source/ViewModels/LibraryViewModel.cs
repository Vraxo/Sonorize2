using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.LibraryManagement;
using System.ComponentModel; // Required for PropertyChangedEventArgs

namespace Sonorize.ViewModels;

public class LibraryViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService; // Kept for LibraryStatusTextGenerator
    private readonly MusicLibraryService _musicLibraryService;
    private readonly LoopDataService _loopDataService;
    private readonly MainWindowViewModel _parentViewModel;
    private readonly ArtistAlbumCollectionManager _artistAlbumManager;
    private readonly SongFilteringService _songFilteringService;
    private readonly LibraryStatusTextGenerator _statusTextGenerator;
    private readonly LibraryDataOrchestrator _libraryDataOrchestrator;
    private readonly TrackNavigationManager _trackNavigationManager;
    private readonly LibraryDisplayModeService _displayModeService; // New dependency

    private readonly ObservableCollection<Song> _allSongs = [];

    public ObservableCollection<Song> FilteredSongs { get; } = [];
    public ObservableCollection<ArtistViewModel> Artists { get; } = [];
    public ObservableCollection<AlbumViewModel> Albums { get; } = [];

    // Display mode command is now proxied from LibraryDisplayModeService
    public ICommand SetDisplayModeCommand => _displayModeService.SetDisplayModeCommand;
    public ICommand PreviousTrackCommand => _trackNavigationManager.PreviousTrackCommand;
    public ICommand NextTrackCommand => _trackNavigationManager.NextTrackCommand;
    public ICommand EditSongMetadataCommand { get; }


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
            _trackNavigationManager.UpdateSelectedSong(value);
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


    // Display mode properties are now proxies to LibraryDisplayModeService
    public SongDisplayMode LibraryViewMode => _displayModeService.LibraryViewMode;
    public SongDisplayMode ArtistViewMode => _displayModeService.ArtistViewMode;
    public SongDisplayMode AlbumViewMode => _displayModeService.AlbumViewMode;

    public LibraryViewModel(
        MainWindowViewModel parentViewModel,
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        LoopDataService loopDataService,
        LibraryDisplayModeService displayModeService) // Added LibraryDisplayModeService
    {
        _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
        _settingsService = settingsService; // Still needed for status text generator
        _musicLibraryService = musicLibraryService;
        _loopDataService = loopDataService;
        _displayModeService = displayModeService ?? throw new ArgumentNullException(nameof(displayModeService));

        // Subscribe to PropertyChanged on _displayModeService to update proxied properties
        _displayModeService.PropertyChanged += DisplayModeService_PropertyChanged;

        FilteredSongs = new ObservableCollection<Song>();
        _trackNavigationManager = new TrackNavigationManager(FilteredSongs);

        _artistAlbumManager = new ArtistAlbumCollectionManager(Artists, Albums, _musicLibraryService);
        _songFilteringService = new SongFilteringService();
        _statusTextGenerator = new LibraryStatusTextGenerator();
        _libraryDataOrchestrator = new LibraryDataOrchestrator(_musicLibraryService, _artistAlbumManager, _settingsService);

        _musicLibraryService.SongThumbnailUpdated += MusicLibraryService_SongThumbnailUpdated;

        EditSongMetadataCommand = new RelayCommand(ExecuteEditSongMetadata, CanExecuteEditSongMetadata);

        UpdateStatusBarText();
    }

    private void DisplayModeService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Raise PropertyChanged for our proxy properties when the service's properties change
        switch (e.PropertyName)
        {
            case nameof(LibraryDisplayModeService.LibraryViewMode):
                OnPropertyChanged(nameof(LibraryViewMode));
                break;
            case nameof(LibraryDisplayModeService.ArtistViewMode):
                OnPropertyChanged(nameof(ArtistViewMode));
                break;
            case nameof(LibraryDisplayModeService.AlbumViewMode):
                OnPropertyChanged(nameof(AlbumViewMode));
                break;
        }
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
        SelectedSong = null;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _allSongs.Clear();
            FilteredSongs.Clear();
            Artists.Clear();
            Albums.Clear();
            LibraryStatusText = "Preparing to load music...";
        });

        Action<Song> songAddedCallback = song => _allSongs.Add(song);
        Action<string> statusUpdateCallback = status => LibraryStatusText = status;

        await _libraryDataOrchestrator.LoadAndProcessLibraryDataAsync(statusUpdateCallback, songAddedCallback);

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
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
        var currentSelectedSongBeforeFilter = SelectedSong;

        FilteredSongs.Clear();
        var filtered = _songFilteringService.ApplyFilter(_allSongs, SearchQuery, SelectedArtist, SelectedAlbum);
        foreach (var song in filtered)
        {
            FilteredSongs.Add(song);
        }

        if (currentSelectedSongBeforeFilter != null && FilteredSongs.Contains(currentSelectedSongBeforeFilter))
        {
            SelectedSong = currentSelectedSongBeforeFilter;
        }
        else if (currentSelectedSongBeforeFilter != null)
        {
            Debug.WriteLine($"[LibraryVM] Selected song '{currentSelectedSongBeforeFilter.Title}' is no longer in the filtered list. Clearing selection.");
            SelectedSong = null;
        }
        else
        {
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

    private void ExecuteEditSongMetadata(object? parameter)
    {
        if (parameter is Song song)
        {
            Debug.WriteLine($"[LibraryVM] Edit metadata requested for: {song.Title}");
            // Future: Open metadata editing dialog here
        }
    }

    private bool CanExecuteEditSongMetadata(object? parameter)
    {
        return parameter is Song;
    }


    public void RaiseLibraryCommandsCanExecuteChanged()
    {
        // SetDisplayModeCommand CanExecute is handled by LibraryDisplayModeService
        // Navigation commands are handled by TrackNavigationManager
        (EditSongMetadataCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_musicLibraryService != null)
        {
            _musicLibraryService.SongThumbnailUpdated -= MusicLibraryService_SongThumbnailUpdated;
        }
        if (_displayModeService != null)
        {
            _displayModeService.PropertyChanged -= DisplayModeService_PropertyChanged;
            // If LibraryDisplayModeService becomes IDisposable, dispose it here.
        }
    }
}