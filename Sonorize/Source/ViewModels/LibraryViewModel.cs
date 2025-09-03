using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
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

        // Instantiate the new LibraryLoadProcess
        _libraryLoadProcess = new LibraryLoadProcess(
            _components,
            ApplyFilter, // Pass private method as delegate
            UpdateStatusBarText, // Pass private method as delegate
            isLoading => IsLoadingLibrary = isLoading, // Lambda to set property
            status => LibraryStatusText = status,       // Lambda to set property
            Dispatcher.UIThread                         // Pass dispatcher
        );

        // Subscribe to events from components provided by _components
        _components.FilterState.FilterCriteriaChanged += (s, e) => ApplyFilter();
        _components.FilterState.RequestTabSwitchToLibrary += (s, e) => _parentViewModel.ActiveTabIndex = 0;
        _components.SongList.PropertyChanged += SongListManager_PropertyChanged;

        _musicLibraryService.SongThumbnailUpdated += MusicLibraryService_SongThumbnailUpdated;

        EditSongMetadataCommand = new RelayCommand(ExecuteEditSongMetadata, CanExecuteEditSongMetadata);
        SortCommand = new RelayCommand(ExecuteSort);

        UpdateStatusBarText();
    }

    private void ExecuteSort(object? parameter)
    {
        if (parameter is not SortProperty newSortProperty)
        {
            return;
        }

        if (CurrentSortProperty == newSortProperty)
        {
            // If it's the same column, flip the direction
            CurrentSortDirection = CurrentSortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
        }
        else
        {
            // If it's a new column, set it and default to ascending
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
        // Delegate the loading process to the new class
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
        _components.SongList.ApplyFilter(
            _components.FilterState.SearchQuery,
            _components.FilterState.SelectedArtist,
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
            _components.FilterState.FilterCriteriaChanged -= (s, e) => ApplyFilter();
            _components.FilterState.RequestTabSwitchToLibrary -= (s, e) => _parentViewModel.ActiveTabIndex = 0;
        }

        if (_components?.SongList is not null)
        {
            _components.SongList.PropertyChanged -= SongListManager_PropertyChanged;
        }
    }
}