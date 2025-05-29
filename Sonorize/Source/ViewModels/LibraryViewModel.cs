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
using System.Collections.Generic; // Required for IReadOnlyList

namespace Sonorize.ViewModels;

public class LibraryViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService; // Kept for LibraryStatusTextGenerator
    private readonly MusicLibraryService _musicLibraryService;
    // private readonly LoopDataService _loopDataService; // LoopDataService is used by SongFactory, not directly here anymore.
    private readonly MainWindowViewModel _parentViewModel;
    private readonly SongFilteringService _songFilteringService; // Passed to SongListManager
    private readonly LibraryStatusTextGenerator _statusTextGenerator;
    private readonly LibraryDataOrchestrator _libraryDataOrchestrator;
    private readonly TrackNavigationManager _trackNavigationManager;
    private readonly LibraryDisplayModeService _displayModeService; // New dependency
    private readonly LibraryFilterStateManager _filterStateManager; // New dependency
    private readonly SongListManager _songListManager; // New dependency

    public LibraryGroupingsViewModel Groupings { get; } // New ViewModel for Artists/Albums

    // Proxied properties from SongListManager
    public ObservableCollection<Song> FilteredSongs => _songListManager.FilteredSongs;
    public Song? SelectedSong
    {
        get => _songListManager.SelectedSong;
        set
        {
            // Forward to SongListManager, which handles INotifyPropertyChanged for its SelectedSong
            if (_songListManager.SelectedSong != value)
            {
                _songListManager.SelectedSong = value;
                // We still need to notify that LibraryViewModel.SelectedSong has changed for its bindings
                OnPropertyChanged();
                // Let TrackNavigationManager know
                _trackNavigationManager.UpdateSelectedSong(value);
                (EditSongMetadataCommand as RelayCommand)?.RaiseCanExecuteChanged();
                Debug.WriteLine($"[LibraryVM] SelectedSong changed to: {value?.Title ?? "null"} (via SongListManager)");
            }
        }
    }


    // Expose FilterStateManager for binding
    public LibraryFilterStateManager FilterState => _filterStateManager;


    // Display mode command is now proxied from LibraryDisplayModeService
    public ICommand SetDisplayModeCommand => _displayModeService.SetDisplayModeCommand;
    public ICommand PreviousTrackCommand => _trackNavigationManager.PreviousTrackCommand;
    public ICommand NextTrackCommand => _trackNavigationManager.NextTrackCommand;
    public ICommand EditSongMetadataCommand { get; }


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
        LoopDataService loopDataService, // Still needed for SongFactory used by MusicLibraryService indirectly
        LibraryDisplayModeService displayModeService)
    {
        _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        // _loopDataService = loopDataService; // Not directly used here
        _displayModeService = displayModeService ?? throw new ArgumentNullException(nameof(displayModeService));

        Groupings = new LibraryGroupingsViewModel(_musicLibraryService); // Instantiate new VM

        _filterStateManager = new LibraryFilterStateManager();
        _filterStateManager.FilterCriteriaChanged += (s, e) => ApplyFilter();
        _filterStateManager.RequestTabSwitchToLibrary += (s, e) => _parentViewModel.ActiveTabIndex = 0;

        _songFilteringService = new SongFilteringService();
        _songListManager = new SongListManager(_songFilteringService);
        _songListManager.PropertyChanged += SongListManager_PropertyChanged;


        // Subscribe to PropertyChanged on _displayModeService to update proxied properties
        _displayModeService.PropertyChanged += DisplayModeService_PropertyChanged;

        _trackNavigationManager = new TrackNavigationManager(FilteredSongs); // FilteredSongs is now from _songListManager

        // _artistAlbumManager is now encapsulated in Groupings
        _statusTextGenerator = new LibraryStatusTextGenerator();
        // Pass Groupings VM's ArtistAlbumManager instance to LibraryDataOrchestrator
        _libraryDataOrchestrator = new LibraryDataOrchestrator(_musicLibraryService, Groupings.ArtistAlbumManager, _settingsService);


        _musicLibraryService.SongThumbnailUpdated += MusicLibraryService_SongThumbnailUpdated;

        EditSongMetadataCommand = new RelayCommand(ExecuteEditSongMetadata, CanExecuteEditSongMetadata);

        UpdateStatusBarText();
    }

    private void SongListManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SongListManager.SelectedSong))
        {
            // Relay the property change for LibraryViewModel.SelectedSong
            OnPropertyChanged(nameof(SelectedSong));
            _trackNavigationManager.UpdateSelectedSong(_songListManager.SelectedSong);
            (EditSongMetadataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            Debug.WriteLine($"[LibraryVM] SongListManager.SelectedSong changed to: {_songListManager.SelectedSong?.Title ?? "null"}. Updated own SelectedSong.");
        }
        else if (e.PropertyName == nameof(SongListManager.FilteredSongs))
        {
            OnPropertyChanged(nameof(FilteredSongs));
        }
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
            Groupings.HandleSongThumbnailUpdate(updatedSong, _songListManager.GetAllSongsReadOnly());
            // OnPropertyChanged for Groupings.Artists and Groupings.Albums will be handled by LibraryGroupingsViewModel
            // if the collections themselves are replaced, or by ObservableCollection if items are modified.
        });
    }

    public async Task LoadLibraryAsync()
    {
        if (IsLoadingLibrary)
        {
            return;
        }

        IsLoadingLibrary = true;
        _filterStateManager.ClearSelectionsAndSearch(); // Resets SearchQuery, SelectedArtist, SelectedAlbum
        // SelectedSong will be cleared by SongListManager.ClearAllSongs or during filter application

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _songListManager.ClearAllSongs(); // Clears all songs, filtered songs, and selected song
            Groupings.Artists.Clear(); // Clear collections in Groupings VM
            Groupings.Albums.Clear();
            LibraryStatusText = "Preparing to load music...";
        });

        List<Song> loadedRawSongs = new List<Song>();
        Action<Song> songAddedCallback = song => loadedRawSongs.Add(song); // Collect raw songs
        Action<string> statusUpdateCallback = status => LibraryStatusText = status;

        var allLoadedSongsFromOrchestrator = await _libraryDataOrchestrator.LoadAndProcessLibraryDataAsync(statusUpdateCallback, songAddedCallback);
        _songListManager.SetAllSongs(allLoadedSongsFromOrchestrator); // Set all songs in the manager


        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Groupings VM populates its collections
            Groupings.PopulateCollections(_songListManager.GetAllSongsReadOnly());
            ApplyFilter(); // Apply initial filter
        });

        IsLoadingLibrary = false;
        UpdateStatusBarText();
    }

    private void ApplyFilter()
    {
        _songListManager.ApplyFilter(
            _filterStateManager.SearchQuery,
            _filterStateManager.SelectedArtist,
            _filterStateManager.SelectedAlbum);
        // SelectedSong management is now inside _songListManager.ApplyFilter

        // Ensure TrackNavigationManager is aware of the potentially changed SelectedSong from SongListManager
        _trackNavigationManager.UpdateSelectedSong(_songListManager.SelectedSong);
        UpdateStatusBarText();
    }

    public void UpdateStatusBarText()
    {
        if (!IsLoadingLibrary)
        {
            LibraryStatusText = _statusTextGenerator.GenerateStatusText(
                IsLoadingLibrary,
                _songListManager.AllSongsCount, // Use count from SongListManager
                FilteredSongs.Count, // FilteredSongs is a direct proxy
                _filterStateManager.SelectedArtist,
                _filterStateManager.SelectedAlbum,
                _filterStateManager.SearchQuery,
                _settingsService
            );
        }
    }

    private void ExecuteEditSongMetadata(object? parameter)
    {
        if (parameter is Song song && _parentViewModel.OpenEditSongMetadataDialogCommand.CanExecute(song))
        {
            Debug.WriteLine($"[LibraryVM] Delegating Edit metadata for: {song.Title} to MainWindowViewModel.");
            _parentViewModel.OpenEditSongMetadataDialogCommand.Execute(song);
        }
        else
        {
            Debug.WriteLine($"[LibraryVM] Edit metadata requested but parameter is not a Song or parent command cannot execute.");
        }
    }

    private bool CanExecuteEditSongMetadata(object? parameter)
    {
        return parameter is Song && _parentViewModel.OpenEditSongMetadataDialogCommand.CanExecute(parameter);
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
        }
        if (_filterStateManager != null)
        {
            _filterStateManager.FilterCriteriaChanged -= (s, e) => ApplyFilter();
            _filterStateManager.RequestTabSwitchToLibrary -= (s, e) => _parentViewModel.ActiveTabIndex = 0;
        }
        if (_songListManager != null)
        {
            _songListManager.PropertyChanged -= SongListManager_PropertyChanged;
            // If SongListManager implemented IDisposable, dispose it here.
        }
        // Groupings VM does not currently implement IDisposable
    }
}