using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.Utils; // For AlbumArtistTupleComparer

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService; // Keep LoopDataService for library loading persistence

    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    // Expose the new LoopEditorViewModel
    public LoopEditorViewModel LoopEditor { get; }

    public bool HasCurrentSong => PlaybackService.CurrentSong != null;

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
            if (_selectedSongInternal != value)
            {
                var previousSong = _selectedSongInternal;
                if (SetProperty(ref _selectedSongInternal, value))
                {
                    HandleSelectedSongChange(previousSong, _selectedSongInternal);
                }
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
            }
        }
    }

    private int _activeTabIndex = 0;
    public int ActiveTabIndex { get => _activeTabIndex; set => SetProperty(ref _activeTabIndex, value); }

    // Loop related properties moved to LoopEditorViewModel

    private string _statusBarText = "Welcome to Sonorize!";
    public string StatusBarText { get => _statusBarText; set => SetProperty(ref _statusBarText, value); }

    private bool _isLoadingLibrary = false;
    public bool IsLoadingLibrary { get => _isLoadingLibrary; set { if (SetProperty(ref _isLoadingLibrary, value)) OnIsLoadingLibraryChanged(); } }

    private bool _isAdvancedPanelVisible;
    public bool IsAdvancedPanelVisible { get => _isAdvancedPanelVisible; set { if (SetProperty(ref _isAdvancedPanelVisible, value)) OnAdvancedPanelVisibleChanged(); } }

    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed { get => _playbackSpeed; set { value = Math.Clamp(value, 0.5, 2.0); if (SetProperty(ref _playbackSpeed, value)) { PlaybackService.PlaybackRate = (float)value; OnPropertyChanged(nameof(PlaybackSpeedDisplay)); } } }
    public string PlaybackSpeedDisplay => $"{PlaybackSpeed:F2}x";

    private double _playbackPitch = 0.0;
    public double PlaybackPitch { get => _playbackPitch; set { value = Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2.0; value = Math.Clamp(value, -4.0, 4.0); if (SetProperty(ref _playbackPitch, value)) { PlaybackService.PitchSemitones = (float)_playbackPitch; OnPropertyChanged(nameof(PlaybackPitchDisplay)); } } }
    public string PlaybackPitchDisplay => $"{PlaybackPitch:+0.0;-0.0;0} st";

    public ObservableCollection<WaveformPoint> WaveformRenderData { get; } = new();
    private bool _isWaveformLoading = false;
    public bool IsWaveformLoading { get => _isWaveformLoading; private set => SetProperty(ref _isWaveformLoading, value); }

    public double SliderPositionSeconds
    {
        get => PlaybackService.CurrentPosition.TotalSeconds;
        set
        {
            if (PlaybackService.HasCurrentSong && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
            {
                if (Math.Abs(PlaybackService.CurrentPosition.TotalSeconds - value) > 0.1)
                {
                    Debug.WriteLine($"[MainVM.SliderPositionSeconds.set] User seeking via slider to: {value:F2}s. Current playback pos: {PlaybackService.CurrentPosition.TotalSeconds:F2}s");
                    PlaybackService.Seek(TimeSpan.FromSeconds(value));
                }
            }
        }
    }

    public string CurrentTimeTotalTimeDisplay
    {
        get
        {
            if (PlaybackService.CurrentSong != null && PlaybackService.CurrentSongDuration.TotalSeconds > 0)
            {
                return $"{PlaybackService.CurrentPosition:mm\\:ss} / {PlaybackService.CurrentSongDuration:mm\\:ss}";
            }
            return "--:-- / --:--";
        }
    }

    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }
    public ICommand ToggleAdvancedPanelCommand { get; }
    // Loop commands moved to LoopEditorViewModel

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService,
        LoopDataService loopDataService) // Keep LoopDataService for library loading persistence
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;

        // Initialize the new LoopEditorViewModel
        LoopEditor = new LoopEditorViewModel(PlaybackService, loopDataService);


        LoadInitialDataCommand = new RelayCommand(async _ => await LoadMusicLibrary(), _ => !IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !IsLoadingLibrary);
        ToggleAdvancedPanelCommand = new RelayCommand(_ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible, _ => (SelectedSong != null || PlaybackService.CurrentSong != null) && !IsLoadingLibrary);

        PlaybackService.PropertyChanged += OnPlaybackServicePropertyChanged;
        PlaybackSpeed = 1.0;
        PlaybackPitch = 0.0;

        // Call UpdateAllUIDependentStates to sync UI initially (including loop editor state)
        UpdateAllUIDependentStates();
    }

    private void OnIsLoadingLibraryChanged()
    {
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        if (_isLoadingLibrary) IsAdvancedPanelVisible = false;
    }

    private void OnAdvancedPanelVisibleChanged()
    {
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // Load waveform when panel becomes visible, if a song is loaded and waveform isn't already there/loading
        if (IsAdvancedPanelVisible && PlaybackService.CurrentSong != null && (!WaveformRenderData.Any() || !IsWaveformLoading))
        {
            _ = LoadWaveformForCurrentSong();
        }
    }

    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(HasCurrentSong));
        // Loop editor state is updated by LoopEditorViewModel's internal logic/PlaybackService handler
        UpdateStatusBarText();
        OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay));
        RaiseAllCommandsCanExecuteChanged();
    }

    private void RaiseAllCommandsCanExecuteChanged()
    {
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();

        // Trigger CanExecute changes for LoopEditor commands via the LoopEditor instance
        LoopEditor.RaiseLoopCommandCanExecuteChanged();
    }

    private void HandleSelectedSongChange(Song? oldSong, Song? newSong)
    {
        Debug.WriteLine($"[MainVM] HandleSelectedSongChange. Old: {oldSong?.Title ?? "null"}, New: {newSong?.Title ?? "null"}");

        // Play the new song if different or if stopped
        if (newSong != null)
        {
            if (newSong != PlaybackService.CurrentSong || PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
            {
                PlaybackService.Play(newSong); // PlaybackService.CurrentSong will be updated here
                // The LoopEditorViewModel listens to PlaybackService.CurrentSong changes
                // and updates its state accordingly, including subscribing/unsubscribing
                // from Song.PropertyChanged for SavedLoop/IsLoopActive.
            }
            // If newSong is the same as the current playing song, no Play() call is needed.
            // LoopEditorViewModel's state is already in sync via PlaybackService.CurrentSong changes.
        }
        else
        {
            // UI deselected song, don't automatically stop playback
            Debug.WriteLine($"[MainVM] UI deselected a song (newSong is null). Current playing song '{PlaybackService.CurrentSong?.Title ?? "null"}' will continue if it was playing.");
        }

        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        UpdateAllUIDependentStates(); // Update non-loop specific UI state
    }

    // Renamed to be specific about PlaybackService changes
    private void OnPlaybackServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (args.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    // When CurrentSong changes in the service, HandleSelectedSongChange or Play() was called.
                    // LoopEditorViewModel already listens to this property and updates its state.
                    // We only need to update MainVM-specific UI elements and trigger waveform loading.
                    UpdateAllUIDependentStates();
                    OnPropertyChanged(nameof(SliderPositionSeconds));
                    OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay));
                    // Trigger waveform load for the new song if Advanced Panel is visible
                    if (PlaybackService.CurrentSong != null && IsAdvancedPanelVisible)
                    {
                        _ = LoadWaveformForCurrentSong();
                    }
                    else if (PlaybackService.CurrentSong == null)
                    {
                        // Clear waveform if no song is loaded
                        WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData)); IsWaveformLoading = false;
                    }
                    break;

                case nameof(PlaybackService.IsPlaying):
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    UpdateStatusBarText();
                    RaiseAllCommandsCanExecuteChanged();
                    // LoopEditorViewModel also listens to PlaybackService status for command CanExecute
                    break;

                case nameof(PlaybackService.CurrentPosition):
                    // Slider binding handles this directly.
                    // LoopEditorViewModel also listens to CurrentPosition for candidate updates and CanSaveLoopRegion.
                    OnPropertyChanged(nameof(SliderPositionSeconds));
                    OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay));
                    break;

                case nameof(PlaybackService.CurrentSongDuration):
                    // Slider binding handles this directly.
                    // LoopEditorViewModel also listens to CurrentSongDuration for CanSaveLoopRegion.
                    OnPropertyChanged(nameof(SliderPositionSeconds));
                    OnPropertyChanged(nameof(CurrentTimeTotalTimeDisplay));
                    break;
            }
        });
    }

    // Song.PropertyChanged handlers moved to LoopEditorViewModel (CurrentSong_PropertyChanged)
    // ToggleCurrentSongLoopActive moved to LoopEditorViewModel

    private void OnArtistSelected(ArtistViewModel artist)
    {
        if (artist?.Name == null) return;
        Debug.WriteLine($"[MainVM] Artist selected: {artist.Name}");
        SearchQuery = artist.Name;
        ActiveTabIndex = 0;
    }

    private void OnAlbumSelected(AlbumViewModel album)
    {
        if (album?.Title == null || album.Artist == null) return;
        Debug.WriteLine($"[MainVM] Album selected: {album.Title} by {album.Artist}");
        SearchQuery = string.Empty;

        FilteredSongs.Clear();
        var songsInAlbum = _allSongs.Where(s =>
            s.Album.Equals(album.Title, StringComparison.OrdinalIgnoreCase) &&
            s.Artist.Equals(album.Artist, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Title); // Always sort by title within an album

        foreach (var song in songsInAlbum)
        {
            FilteredSongs.Add(song);
        }
        UpdateStatusBarText();
        ActiveTabIndex = 0;
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

        // Sorting for the main song list (Library tab) - Sort by Title
        songsToFilter = songsToFilter.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase);

        foreach (var song in songsToFilter)
        {
            FilteredSongs.Add(song);
        }
        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            Debug.WriteLine($"[MainVM ApplyFilter] Current SelectedSong '{SelectedSong.Title}' is no longer in FilteredSongs. ListBox should update selection.");
        }
        UpdateStatusBarText();
    }

    private async Task LoadWaveformForCurrentSong()
    {
        var songToLoadWaveformFor = PlaybackService.CurrentSong;
        if (songToLoadWaveformFor == null || string.IsNullOrEmpty(songToLoadWaveformFor.FilePath))
        {
            WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData)); IsWaveformLoading = false; return;
        }
        IsWaveformLoading = true;
        try
        {
            Debug.WriteLine($"[MainVM] Requesting waveform for: {songToLoadWaveformFor.Title}");
            // Target points should probably be based on control width or a fixed resolution
            var points = await _waveformService.GetWaveformAsync(songToLoadWaveformFor.FilePath, 1000); // Use a reasonable fixed number for now
            // Check if the song is still the same after the async operation
            if (PlaybackService.CurrentSong == songToLoadWaveformFor)
            {
                WaveformRenderData.Clear(); foreach (var p in points) WaveformRenderData.Add(p); OnPropertyChanged(nameof(WaveformRenderData));
                Debug.WriteLine($"[MainVM] Waveform loaded for: {songToLoadWaveformFor.Title}, {points.Count} points.");
            }
            else
            {
                Debug.WriteLine($"[MainVM] Waveform for {songToLoadWaveformFor.Title} loaded, but current song is now {PlaybackService.CurrentSong?.Title ?? "null"}. Discarding.");
                WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData));
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[MainVM] Failed to load waveform for {songToLoadWaveformFor.Title}: {ex.Message}"); WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData)); }
        finally { IsWaveformLoading = false; }
    }

    // UpdateLoopEditorForCurrentSong moved logic into LoopEditorViewModel's internal state management
    // ClearLoopCandidateInputs moved to LoopEditorViewModel
    // CanSaveLoopRegion logic moved to LoopEditorViewModel
    // SaveLoopAction moved to LoopEditorViewModel
    // ClearSavedLoopAction moved to LoopEditorViewModel
    // UpdateActiveLoopDisplayText moved to LoopEditorViewModel

    private void UpdateStatusBarText()
    {
        if (IsLoadingLibrary) return; string status; var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong != null)
        {
            string stateStr = PlaybackService.CurrentPlaybackStatus switch { PlaybackStateStatus.Playing => "Playing", PlaybackStateStatus.Paused => "Paused", PlaybackStateStatus.Stopped => "Stopped", _ => "Idle" };
            status = $"{stateStr}: {currentServiceSong.Title}";
            // Get active loop status from LoopEditor
            if (LoopEditor.IsCurrentLoopActiveUiBinding && currentServiceSong.SavedLoop != null)
            {
                status += $" (Loop Active)";
            }
        }
        else
        {
            status = $"Sonorize - {FilteredSongs.Count} of {_allSongs.Count} songs displayed.";
            var settings = _settingsService.LoadSettings(); // Load settings just for this status check
            if (_allSongs.Count == 0 && !IsLoadingLibrary && !settings.MusicDirectories.Any())
            {
                status = "Sonorize - Library empty. Add directories via File menu.";
            }
            else if (_allSongs.Count == 0 && !IsLoadingLibrary && settings.MusicDirectories.Any())
            {
                status = "Sonorize - No songs found in configured directories.";
            }
        }
        StatusBarText = status;
    }

    private async Task LoadMusicLibrary()
    {
        if (IsLoadingLibrary) return; IsAdvancedPanelVisible = false; IsLoadingLibrary = true; SearchQuery = string.Empty; var settings = _settingsService.LoadSettings();
        await Dispatcher.UIThread.InvokeAsync(() => {
            SelectedSong = null; _allSongs.Clear(); Artists.Clear(); Albums.Clear(); FilteredSongs.Clear(); WaveformRenderData.Clear(); OnPropertyChanged(nameof(WaveformRenderData)); StatusBarText = "Preparing to load music..."; UpdateAllUIDependentStates();
        });
        if (settings.MusicDirectories.Any())
        {
            try
            {
                // Pass the existing LoopDataService instance to MusicLibraryService during song loading
                await Task.Run(async () => { await _musicLibraryService.LoadMusicFromDirectoriesAsync(settings.MusicDirectories, song => Dispatcher.UIThread.InvokeAsync(() => _allSongs.Add(song)), s => Dispatcher.UIThread.InvokeAsync(() => StatusBarText = s)); });
                await Dispatcher.UIThread.InvokeAsync(() => {
                    Artists.Clear(); var uniqueArtistNames = _allSongs.Select(s => s.Artist).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
                    Bitmap? defaultThumb = _musicLibraryService.GetDefaultThumbnail(); foreach (var artistName in uniqueArtistNames!) { Bitmap? repThumb = _allSongs.FirstOrDefault(s => (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false) && s.Thumbnail != null)?.Thumbnail ?? defaultThumb; Artists.Add(new ArtistViewModel { Name = artistName, Thumbnail = repThumb }); }
                    OnPropertyChanged(nameof(Artists));
                    Albums.Clear(); Func<Song, (string Album, string Artist)> keySelector = s => (s.Album?.Trim() ?? string.Empty, s.Artist?.Trim() ?? string.Empty);
                    var uniqueAlbums = _allSongs.Where(s => !string.IsNullOrWhiteSpace(s.Album) && !string.IsNullOrWhiteSpace(s.Artist)).GroupBy(keySelector, AlbumArtistTupleComparer.Instance)
                        .Select(g => new { AlbumTitle = g.First().Album, ArtistName = g.First().Artist, ThumbSong = g.FirstOrDefault(s => s.Thumbnail != null) })
                        .OrderBy(a => a.ArtistName, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.AlbumTitle, StringComparer.OrdinalIgnoreCase).ToList(); // Keep album sort by artist, then album title
                    foreach (var albumData in uniqueAlbums) Albums.Add(new AlbumViewModel { Title = albumData.AlbumTitle, Artist = albumData.ArtistName, Thumbnail = albumData.ThumbSong?.Thumbnail ?? defaultThumb }); OnPropertyChanged(nameof(Albums));
                    ApplyFilter(); // Apply filter with the new sorting logic
                });
            }
            catch (Exception ex) { Debug.WriteLine($"[MainVM] Error loading library: {ex}"); await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "Error loading music library."); }
        }
        IsLoadingLibrary = false; UpdateStatusBarText();
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || IsLoadingLibrary) return; IsAdvancedPanelVisible = false; var currentSettingsBeforeDialog = _settingsService.LoadSettings();
        var settingsVM = new SettingsViewModel(_settingsService); var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };
        await settingsDialog.ShowDialog(owner);
        if (settingsVM.SettingsChanged)
        {
            var newSettingsAfterDialog = _settingsService.LoadSettings();
            bool dirsActuallyChanged = !currentSettingsBeforeDialog.MusicDirectories.SequenceEqual(newSettingsAfterDialog.MusicDirectories);
            bool themeActuallyChanged = currentSettingsBeforeDialog.PreferredThemeFileName != newSettingsAfterDialog.PreferredThemeFileName;

            if (dirsActuallyChanged) { await LoadMusicLibrary(); }
            if (themeActuallyChanged) { StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect."; }
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || IsLoadingLibrary) return; IsAdvancedPanelVisible = false;
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Music Directory", AllowMultiple = false });
        if (result != null && result.Count > 0)
        {
            string? folderPath = result[0].Path.LocalPath;
            if (string.IsNullOrEmpty(folderPath) && result[0].Path.IsAbsoluteUri) { try { folderPath = new Uri(result[0].Path.ToString()).LocalPath; } catch { folderPath = null; Debug.WriteLine($"[MainVM] Could not convert folder URI: {result[0].Path}"); } }
            if (!string.IsNullOrEmpty(folderPath))
            {
                var settings = _settingsService.LoadSettings(); if (!settings.MusicDirectories.Contains(folderPath)) { settings.MusicDirectories.Add(folderPath); _settingsService.SaveSettings(settings); await LoadMusicLibrary(); }
            }
            else { Debug.WriteLine($"[MainVM] Selected folder path could not be determined: {result[0].Name}"); }
        }
    }
}