using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.LibraryManagement;

namespace Sonorize.ViewModels;

public class LibraryViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly MainWindowViewModel _parentViewModel;
    private readonly TrackNavigationManager _trackNavigationManager;
    private readonly LibraryComponentProvider _components;
    private readonly LibraryLoadProcess _libraryLoadProcess;

    public LibraryViewOptionsViewModel ViewOptions { get; }

    public LibraryGroupingsViewModel Groupings => _components.Groupings;
    public ObservableCollection<Song> FilteredSongs => _components.SongList.FilteredSongs;
    public LibraryFilterStateManager FilterState => _components.FilterState;

    // Drill-Down State
    public ArtistViewModel? ArtistDrillDownTarget { get; private set; }
    public ObservableCollection<Song> SongsForArtistDrillDown { get; } = new();
    public ICommand GoBackToArtistListCommand { get; }


    public ICommand PreviousTrackCommand => _trackNavigationManager.PreviousTrackCommand;
    public ICommand NextTrackCommand => _trackNavigationManager.NextTrackCommand;
    public ICommand EditSongMetadataCommand { get; }
    public ICommand SortCommand { get; }

    public SortProperty CurrentSortProperty { get; private set; } = SortProperty.Title;
    public SortDirection CurrentSortDirection { get; private set; } = SortDirection.Ascending;

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

    public Song? SelectedSong
    {
        get => _components.SongList.SelectedSong;
        set
        {
            if (_components.SongList.SelectedSong == value)
            {
                return;
            }

            _components.SongList.SelectedSong = value;
            OnPropertyChanged();
            _trackNavigationManager.UpdateSelectedSong(value);
            (EditSongMetadataCommand as RelayCommand)?.RaiseCanExecuteChanged();

            Debug.WriteLine($"[LibraryVM] SelectedSong changed to: {value?.Title ?? "null"} (via SongListManager)");
        }
    }

    public LibraryViewModel(
        MainWindowViewModel parentViewModel,
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        LoopDataService loopDataService)
    {
        _parentViewModel = parentViewModel ?? throw new ArgumentNullException(nameof(parentViewModel));
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;

        ViewOptions = new LibraryViewOptionsViewModel();
        ViewOptions.LoadFromSettings(_settingsService.LoadSettings());

        _components = new LibraryComponentProvider(musicLibraryService, settingsService);
        _trackNavigationManager = new TrackNavigationManager(_components.SongList.FilteredSongs);

        _libraryLoadProcess = new LibraryLoadProcess(
            _components,
            ApplyFilter,
            UpdateStatusBarText,
            isLoading => IsLoadingLibrary = isLoading,
            status => LibraryStatusText = status,
            Dispatcher.UIThread
        );

        _components.FilterState.PropertyChanged += FilterState_PropertyChanged;
        _components.SongList.PropertyChanged += SongListManager_PropertyChanged;

        _musicLibraryService.SongThumbnailUpdated += MusicLibraryService_SongThumbnailUpdated;

        GoBackToArtistListCommand = new RelayCommand(ExecuteGoBackToArtistList);
        EditSongMetadataCommand = new RelayCommand(ExecuteEditSongMetadata, CanExecuteEditSongMetadata);
        SortCommand = new RelayCommand(ExecuteSort);

        UpdateStatusBarText();
    }

    private void FilterState_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // Artist selection is now for drill-down view, not for filtering the main library
            case nameof(LibraryFilterStateManager.SelectedArtist):
                HandleArtistSelection(FilterState.SelectedArtist);
                break;

            // Playlist and Album selections will still filter the main library view
            case nameof(LibraryFilterStateManager.SelectedAlbum):
            case nameof(LibraryFilterStateManager.SelectedPlaylist):
                ApplyFilter();
                // If a selection was made, switch to the library tab to show the results
                if (FilterState.SelectedAlbum != null || FilterState.SelectedPlaylist != null)
                {
                    _parentViewModel.ActiveTabIndex = 0;
                }
                break;

            // Search query always filters the main library view
            case nameof(LibraryFilterStateManager.SearchQuery):
                ApplyFilter();
                break;
        }
    }

    private void HandleArtistSelection(ArtistViewModel? artist)
    {
        if (artist == null) return;

        ArtistDrillDownTarget = artist;
        OnPropertyChanged(nameof(ArtistDrillDownTarget));

        SongsForArtistDrillDown.Clear();
        var songsByArtist = _components.SongList.GetAllSongsReadOnly()
            .Where(s => s.Artist != null && s.Artist.Equals(artist.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Album)
            .ThenBy(s => s.Title);

        foreach (var song in songsByArtist)
        {
            SongsForArtistDrillDown.Add(song);
        }
    }

    private void ExecuteGoBackToArtistList(object? _ = null)
    {
        ArtistDrillDownTarget = null;
        OnPropertyChanged(nameof(ArtistDrillDownTarget));
        // Clear selection to allow re-selecting the same artist
        FilterState.SelectedArtist = null;
    }

    private void ExecuteSort(object? parameter)
    {
        if (parameter is not SortProperty newSortProperty)
        {
            return;
        }

        if (CurrentSortProperty == newSortProperty)
        {
            CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
        }
        else
        {
            CurrentSortProperty = newSortProperty;
            CurrentSortDirection = SortDirection.Ascending;
        }

        OnPropertyChanged(nameof(CurrentSortProperty));
        OnPropertyChanged(nameof(CurrentSortDirection));

        ApplyFilter(); // Re-apply filter and sorting
    }

    private void SongListManager_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SongListManager.SelectedSong))
        {
            OnPropertyChanged(nameof(SelectedSong));
            _trackNavigationManager.UpdateSelectedSong(_components.SongList.SelectedSong);
            (EditSongMetadataCommand as RelayCommand)?.RaiseCanExecuteChanged();
            Debug.WriteLine($"[LibraryVM] SongListManager.SelectedSong changed to: {_components.SongList.SelectedSong?.Title ?? "null"}. Updated own SelectedSong.");
        }
        else if (e.PropertyName == nameof(SongListManager.FilteredSongs))
        {
            OnPropertyChanged(nameof(FilteredSongs));
        }
    }

    private async void MusicLibraryService_SongThumbnailUpdated(Song updatedSong)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            _components.Groupings.HandleSongThumbnailUpdate(updatedSong, _components.SongList.GetAllSongsReadOnly());
        });
    }

    public async Task LoadLibraryAsync()
    {
        if (IsLoadingLibrary)
        {
            return;
        }
        Debug.WriteLine("[LibraryVM] LoadLibraryAsync: Starting library load process.");
        await _libraryLoadProcess.ExecuteLoadAsync();
        Debug.WriteLine("[LibraryVM] LoadLibraryAsync: Library load process finished.");
    }

    public void RefreshAutoPlaylists()
    {
        if (IsLoadingLibrary)
        {
            return;
        }

        Debug.WriteLine("[LibraryVM] RefreshAutoPlaylists called.");
        _components.AutoPlaylistManager.RefreshAutoPlaylists();
    }

    private void ApplyFilter()
    {
        // Artist selection no longer filters the main library, it triggers a drill-down view.
        // So we pass null for the artist parameter.
        _components.SongList.ApplyFilter(
            _components.FilterState.SearchQuery,
            null,
            _components.FilterState.SelectedAlbum,
            _components.FilterState.SelectedPlaylist,
            CurrentSortProperty,
            CurrentSortDirection,
            ViewOptions);

        _trackNavigationManager.UpdateSelectedSong(_components.SongList.SelectedSong);
        UpdateStatusBarText();
    }

    public void UpdateStatusBarText()
    {
        if (IsLoadingLibrary)
        {
            return;
        }

        LibraryStatusText = _components.StatusTextGenerator.GenerateStatusText(
            IsLoadingLibrary,
            _components.SongList.AllSongsCount,
            FilteredSongs.Count,
            _components.FilterState.SelectedArtist,
            _components.FilterState.SelectedAlbum,
            _components.FilterState.SelectedPlaylist,
            _components.FilterState.SearchQuery,
            _settingsService
        );
    }

    private void ExecuteEditSongMetadata(object? parameter)
    {
        if (parameter is not Song song || !_parentViewModel.OpenEditSongMetadataDialogCommand.CanExecute(song))
        {
            Debug.WriteLine($"[LibraryVM] Edit metadata requested but parameter is not a Song or parent command cannot execute.");
            return;
        }

        Debug.WriteLine($"[LibraryVM] Delegating Edit metadata for: {song.Title} to MainWindowViewModel.");
        _parentViewModel.OpenEditSongMetadataDialogCommand.Execute(song);
    }

    private bool CanExecuteEditSongMetadata(object? parameter)
    {
        return parameter is Song
            && _parentViewModel.OpenEditSongMetadataDialogCommand.CanExecute(parameter);
    }


    public void RaiseLibraryCommandsCanExecuteChanged()
    {
        (EditSongMetadataCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_musicLibraryService is not null)
        {
            _musicLibraryService.SongThumbnailUpdated -= MusicLibraryService_SongThumbnailUpdated;
        }

        if (_components?.FilterState is not null)
        {
            _components.FilterState.PropertyChanged -= FilterState_PropertyChanged;
        }

        if (_components?.SongList is not null)
        {
            _components.SongList.PropertyChanged -= SongListManager_PropertyChanged;
        }
    }
}