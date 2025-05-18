// Path: Source/ViewModels/MainViewModel.cs
using Avalonia.Controls;
using Sonorize.Models;
using Sonorize.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System;
using System.Diagnostics;
using Avalonia.Threading;
using System.ComponentModel;
using System.Collections.Generic;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Sonorize.Utils; // For IStorageProvider, FolderPickerOpenOptions, IStorageFolder

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    // This property will be derived from PlaybackService.CurrentSong
    public bool HasCurrentSong => PlaybackService.CurrentSong != null;

    private readonly ObservableCollection<Song> _allSongs = new();
    public ObservableCollection<Song> FilteredSongs { get; } = new();
    public ObservableCollection<ArtistViewModel> Artists { get; } = new();
    public ObservableCollection<AlbumViewModel> Albums { get; } = new();

    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

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
    public int ActiveTabIndex
    {
        get => _activeTabIndex;
        set => SetProperty(ref _activeTabIndex, value);
    }

    private async void HandleSelectedSongChange(Song? oldSong, Song? newSong)
    {
        Debug.WriteLine($"[MainVM] HandleSelectedSongChange. Old: {oldSong?.Title ?? "null"}, New: {newSong?.Title ?? "null"}");
        if (oldSong != null)
        {
            oldSong.PropertyChanged -= OnCurrentSongSavedLoopChanged;
        }

        if (newSong != null)
        {
            if (newSong != PlaybackService.CurrentSong) // Only play if it's different from what service currently has
            {
                PlaybackService.Play(newSong);
            }
            // Subscribe regardless, as this newSong is now the *UI selected* one
            newSong.PropertyChanged += OnCurrentSongSavedLoopChanged;
            // Waveform and other UI updates will be triggered by OnPlaybackServicePropertyChanged
            // when PlaybackService.CurrentSong is actually set by the Play method.
        }
        else
        {
            PlaybackService.Stop();
        }
        // UI state updates are largely driven by OnPlaybackServicePropertyChanged now.
        // However, some command states might depend directly on the UI selection (_selectedSongInternal).
        RaiseLoopCommandCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Depends on UI selected song state
    }

    private void RaiseLoopCommandCanExecuteChanged()
    {
        (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

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
            .OrderBy(s => s.Title);
        foreach (var song in songsInAlbum)
        {
            FilteredSongs.Add(song);
        }
        UpdateStatusBarText();
        ActiveTabIndex = 0;
    }

    private string _statusBarText = "Welcome to Sonorize!";
    public string StatusBarText { get => _statusBarText; set => SetProperty(ref _statusBarText, value); }

    private bool _isLoadingLibrary = false;
    public bool IsLoadingLibrary
    {
        get => _isLoadingLibrary;
        set { if (SetProperty(ref _isLoadingLibrary, value)) OnIsLoadingLibraryChanged(); }
    }

    private void OnIsLoadingLibraryChanged()
    {
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        if (_isLoadingLibrary) IsAdvancedPanelVisible = false;
    }

    private bool _isAdvancedPanelVisible;
    public bool IsAdvancedPanelVisible
    {
        get => _isAdvancedPanelVisible;
        set
        {
            if (SetProperty(ref _isAdvancedPanelVisible, value))
            {
                (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                if (value && PlaybackService.CurrentSong != null && !WaveformRenderData.Any())
                {
                    _ = LoadWaveformForCurrentSong();
                }
            }
        }
    }

    private double _playbackSpeed = 1.0;
    public double PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            value = Math.Clamp(value, 0.5, 2.0);
            if (SetProperty(ref _playbackSpeed, value))
            {
                PlaybackService.PlaybackRate = (float)value;
                OnPropertyChanged(nameof(PlaybackSpeedDisplay));
            }
        }
    }
    public string PlaybackSpeedDisplay => $"{PlaybackSpeed:F2}x";

    private double _playbackPitch = 0.0;
    public double PlaybackPitch
    {
        get => _playbackPitch;
        set
        {
            value = Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2.0;
            value = Math.Clamp(value, -4.0, 4.0);
            if (SetProperty(ref _playbackPitch, value))
            {
                PlaybackService.PitchSemitones = (float)_playbackPitch;
                OnPropertyChanged(nameof(PlaybackPitchDisplay));
            }
        }
    }
    public string PlaybackPitchDisplay => $"{PlaybackPitch:+0.0;-0.0;0} st";

    public ObservableCollection<WaveformPoint> WaveformRenderData { get; } = new();
    private bool _isWaveformLoading = false;
    public bool IsWaveformLoading { get => _isWaveformLoading; private set => SetProperty(ref _isWaveformLoading, value); }

    private TimeSpan? _newLoopStartCandidate;
    public TimeSpan? NewLoopStartCandidate { get => _newLoopStartCandidate; set { SetProperty(ref _newLoopStartCandidate, value); OnPropertyChanged(nameof(CanSaveLoopRegion)); OnPropertyChanged(nameof(NewLoopStartCandidateDisplay)); } }

    private TimeSpan? _newLoopEndCandidate;
    public TimeSpan? NewLoopEndCandidate { get => _newLoopEndCandidate; set { SetProperty(ref _newLoopEndCandidate, value); OnPropertyChanged(nameof(CanSaveLoopRegion)); OnPropertyChanged(nameof(NewLoopEndCandidateDisplay)); } }

    public string NewLoopStartCandidateDisplay => _newLoopStartCandidate.HasValue ? $"{_newLoopStartCandidate.Value:mm\\:ss\\.ff}" : "Not set";
    public string NewLoopEndCandidateDisplay => _newLoopEndCandidate.HasValue ? $"{_newLoopEndCandidate.Value:mm\\:ss\\.ff}" : "Not set";

    private string _activeLoopDisplayText = "No active loop.";
    public string ActiveLoopDisplayText { get => _activeLoopDisplayText; set => SetProperty(ref _activeLoopDisplayText, value); }

    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }
    public ICommand ToggleAdvancedPanelCommand { get; }
    public ICommand CaptureLoopStartCandidateCommand { get; }
    public ICommand CaptureLoopEndCandidateCommand { get; }
    public ICommand SaveLoopCommand { get; }
    public ICommand ClearLoopCommand { get; }
    public ICommand WaveformSeekCommand { get; }

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService,
        LoopDataService loopDataService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme;
        _waveformService = waveformService;
        _loopDataService = loopDataService;

        LoadInitialDataCommand = new RelayCommand(async _ => await LoadMusicLibrary(), _ => !IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !IsLoadingLibrary);

        ToggleAdvancedPanelCommand = new RelayCommand(
            _ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible,
            // This command's CanExecute should depend on whether a song is selected in the UI
            // or if a song is actively playing in the service, to allow opening even if UI selection is lost.
            _ => (SelectedSong != null || PlaybackService.CurrentSong != null) && !IsLoadingLibrary);


        CaptureLoopStartCandidateCommand = new RelayCommand(
            _ => NewLoopStartCandidate = PlaybackService.CurrentPosition,
            _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentPlaybackStatus != PlaybackStateStatus.Stopped);

        CaptureLoopEndCandidateCommand = new RelayCommand(
            _ => NewLoopEndCandidate = PlaybackService.CurrentPosition,
            _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentPlaybackStatus != PlaybackStateStatus.Stopped);

        SaveLoopCommand = new RelayCommand(SaveLoopAction, _ => CanSaveLoopRegion);
        ClearLoopCommand = new RelayCommand(ClearSavedLoopAction, _ => PlaybackService.CurrentSong?.SavedLoop != null);
        WaveformSeekCommand = new RelayCommand(
            timeSpanObj => { if (timeSpanObj is TimeSpan ts && PlaybackService.CurrentSong != null) PlaybackService.Seek(ts); },
            _ => PlaybackService.CurrentSong != null);

        PlaybackService.PropertyChanged += OnPlaybackServicePropertyChanged;
        PlaybackSpeed = 1.0;
        PlaybackPitch = 0.0;
        UpdateStatusBarText();
        UpdateActiveLoopDisplayText();
        RaiseLoopCommandCanExecuteChanged();
    }

    private void OnPlaybackServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (args.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    Debug.WriteLine($"[MainVM_PSPChanged] CurrentSong in service changed to: {PlaybackService.CurrentSong?.Title ?? "null"}. Updating UI states.");
                    OnPropertyChanged(nameof(HasCurrentSong)); // Notify VM's HasCurrentSong
                    UpdateLoopEditorForCurrentSong();
                    UpdateActiveLoopDisplayText();
                    if (IsAdvancedPanelVisible || PlaybackService.CurrentSong != _selectedSongInternal) // Load waveform if panel is visible or if service changed song away from UI selection
                    {
                        _ = LoadWaveformForCurrentSong();
                    }
                    (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    RaiseLoopCommandCanExecuteChanged();
                    UpdateStatusBarText();
                    break;

                case nameof(PlaybackService.IsPlaying):
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    UpdateStatusBarText();
                    RaiseLoopCommandCanExecuteChanged();
                    break;

                case nameof(PlaybackService.CurrentPosition):
                case nameof(PlaybackService.CurrentSongDuration):
                    OnPropertyChanged(nameof(CanSaveLoopRegion));
                    RaiseLoopCommandCanExecuteChanged();
                    break;
            }
        });
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
        foreach (var song in songsToFilter.OrderBy(s => s.Artist).ThenBy(s => s.Album).ThenBy(s => s.Title))
        {
            FilteredSongs.Add(song);
        }
        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            // If the UI selected song is filtered out, only clear UI selection.
            // Do not stop playback if PlaybackService.CurrentSong is different.
            // Let HandleSelectedSongChange(SelectedSong, null) manage playback if UI selection becomes null.
            SelectedSong = null;
        }
        UpdateStatusBarText();
    }

    private async Task LoadWaveformForCurrentSong()
    {
        var songToLoadWaveformFor = PlaybackService.CurrentSong;
        if (songToLoadWaveformFor == null || string.IsNullOrEmpty(songToLoadWaveformFor.FilePath))
        {
            WaveformRenderData.Clear();
            OnPropertyChanged(nameof(WaveformRenderData));
            IsWaveformLoading = false;
            return;
        }

        IsWaveformLoading = true;
        try
        {
            Debug.WriteLine($"[MainVM] Requesting waveform for: {songToLoadWaveformFor.Title}");
            var points = await _waveformService.GetWaveformAsync(songToLoadWaveformFor.FilePath, 1000);
            if (PlaybackService.CurrentSong == songToLoadWaveformFor) // Still the same song?
            {
                WaveformRenderData.Clear();
                foreach (var p in points) WaveformRenderData.Add(p);
                OnPropertyChanged(nameof(WaveformRenderData));
                Debug.WriteLine($"[MainVM] Waveform loaded for: {songToLoadWaveformFor.Title}, {points.Count} points.");
            }
            else
            {
                Debug.WriteLine($"[MainVM] Waveform for {songToLoadWaveformFor.Title} loaded, but current song is now {PlaybackService.CurrentSong?.Title ?? "null"}. Discarding.");
                WaveformRenderData.Clear();
                OnPropertyChanged(nameof(WaveformRenderData));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainVM] Failed to load waveform for {songToLoadWaveformFor.Title}: {ex.Message}");
            WaveformRenderData.Clear();
            OnPropertyChanged(nameof(WaveformRenderData));
        }
        finally
        {
            IsWaveformLoading = false;
        }
    }

    private void OnCurrentSongSavedLoopChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Song.SavedLoop))
        {
            Debug.WriteLine($"[MainVM_SongPChanged] Song.SavedLoop changed for {PlaybackService.CurrentSong?.Title}. Updating display and commands.");
            Dispatcher.UIThread.InvokeAsync(() => // Ensure UI updates on UI thread
            {
                UpdateActiveLoopDisplayText();
                UpdateStatusBarText();
                (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
        }
    }

    private void UpdateLoopEditorForCurrentSong()
    {
        var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong?.SavedLoop != null)
        {
            NewLoopStartCandidate = currentServiceSong.SavedLoop.Start;
            NewLoopEndCandidate = currentServiceSong.SavedLoop.End;
        }
        else
        {
            NewLoopStartCandidate = null;
            NewLoopEndCandidate = null;
        }
        OnPropertyChanged(nameof(CanSaveLoopRegion));
        RaiseLoopCommandCanExecuteChanged();
    }

    private void ClearLoopCandidateInputs()
    {
        NewLoopStartCandidate = null;
        NewLoopEndCandidate = null;
        RaiseLoopCommandCanExecuteChanged();
    }

    public bool CanSaveLoopRegion =>
        PlaybackService.CurrentSong != null &&
        NewLoopStartCandidate.HasValue &&
        NewLoopEndCandidate.HasValue &&
        NewLoopEndCandidate.Value > NewLoopStartCandidate.Value &&
        NewLoopEndCandidate.Value <= PlaybackService.CurrentSongDuration &&
        NewLoopStartCandidate.Value >= TimeSpan.Zero;

    private void SaveLoopAction(object? param)
    {
        var currentServiceSong = PlaybackService.CurrentSong;
        if (!CanSaveLoopRegion || currentServiceSong == null || !NewLoopStartCandidate.HasValue || !NewLoopEndCandidate.HasValue) return;

        var newLoop = new LoopRegion(NewLoopStartCandidate.Value, NewLoopEndCandidate.Value, "User Loop");
        currentServiceSong.SavedLoop = newLoop; // This should trigger OnCurrentSongSavedLoopChanged
        _loopDataService.SetLoop(currentServiceSong.FilePath, newLoop.Start, newLoop.End);
        Debug.WriteLine($"[MainVM] Loop saved for {currentServiceSong.Title}. Start: {newLoop.Start}, End: {newLoop.End}");
        // UI updates (text, button states) will flow from PropertyChanged events
    }

    private void ClearSavedLoopAction(object? param)
    {
        var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong != null)
        {
            var filePath = currentServiceSong.FilePath;
            currentServiceSong.SavedLoop = null; // This should trigger OnCurrentSongSavedLoopChanged
            if (!string.IsNullOrEmpty(filePath))
            {
                _loopDataService.ClearLoop(filePath);
            }
            Debug.WriteLine($"[MainVM] Loop cleared for {currentServiceSong.Title}.");
        }
        ClearLoopCandidateInputs(); // Also clear the A/B markers in UI
    }

    private void UpdateActiveLoopDisplayText()
    {
        var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong?.SavedLoop != null)
        {
            var loop = currentServiceSong.SavedLoop;
            ActiveLoopDisplayText = $"Active Loop: {loop.Start:mm\\:ss\\.f} - {loop.End:mm\\:ss\\.f}";
        }
        else ActiveLoopDisplayText = "No active loop.";
    }

    private void UpdateStatusBarText()
    {
        if (IsLoadingLibrary) return;
        string status;
        var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong != null)
        {
            string stateStr = PlaybackService.CurrentPlaybackStatus switch
            {
                PlaybackStateStatus.Playing => "Playing",
                PlaybackStateStatus.Paused => "Paused",
                PlaybackStateStatus.Stopped => "Stopped",
                _ => "Idle"
            };
            status = $"{stateStr}: {currentServiceSong.Title}";
            if (currentServiceSong.SavedLoop != null) status += $" (Loop Active)";
        }
        else
        {
            status = $"Sonorize - {FilteredSongs.Count} of {_allSongs.Count} songs displayed.";
            if (_allSongs.Count == 0 && !IsLoadingLibrary && !_settingsService.LoadSettings().MusicDirectories.Any())
            {
                status = "Sonorize - Library empty. Add directories via File menu.";
            }
            else if (_allSongs.Count == 0 && !IsLoadingLibrary && _settingsService.LoadSettings().MusicDirectories.Any())
            {
                status = "Sonorize - No songs found in configured directories.";
            }
        }
        StatusBarText = status;
    }

    private async Task LoadMusicLibrary()
    {
        if (IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;
        IsLoadingLibrary = true;
        SearchQuery = string.Empty;
        var settings = _settingsService.LoadSettings();

        await Dispatcher.UIThread.InvokeAsync(() => {
            SelectedSong = null;
            _allSongs.Clear();
            Artists.Clear();
            Albums.Clear();
            FilteredSongs.Clear();
            WaveformRenderData.Clear();
            OnPropertyChanged(nameof(WaveformRenderData));
            StatusBarText = "Preparing to load music...";
        });

        if (settings.MusicDirectories.Any())
        {
            try
            {
                await Task.Run(async () =>
                {
                    await _musicLibraryService.LoadMusicFromDirectoriesAsync(
                        settings.MusicDirectories,
                        song => Dispatcher.UIThread.InvokeAsync(() => _allSongs.Add(song)),
                        s => Dispatcher.UIThread.InvokeAsync(() => StatusBarText = s)
                    );
                });

                // Key selector for grouping by (Album, Artist) tuple.
                // Normalization (trimming, handling nulls) is good practice for keys.
                Func<Song, (string Album, string Artist)> albumArtistKeySelector = s =>
                    (s.Album?.Trim() ?? string.Empty, s.Artist?.Trim() ?? string.Empty);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Artists.Clear();
                    var uniqueArtistNames = _allSongs
                        .Select(s => s.Artist)
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .Distinct(StringComparer.OrdinalIgnoreCase) // Case-insensitive distinct artists
                        .OrderBy(a => a, StringComparer.OrdinalIgnoreCase) // Order them case-insensitively
                        .ToList();
                    Bitmap? defaultThumb = _musicLibraryService.GetDefaultThumbnail();
                    foreach (var artistName in uniqueArtistNames!)
                    {
                        Bitmap? representativeThumb = _allSongs
                            .FirstOrDefault(s => (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false) && s.Thumbnail != null)
                            ?.Thumbnail ?? defaultThumb;
                        Artists.Add(new ArtistViewModel { Name = artistName, Thumbnail = representativeThumb });
                    }
                    OnPropertyChanged(nameof(Artists));

                    Albums.Clear();
                    var uniqueAlbums = _allSongs
                        .Where(s => !string.IsNullOrWhiteSpace(s.Album) && !string.IsNullOrWhiteSpace(s.Artist))
                        // Use the key selector and the custom IEqualityComparer
                        .GroupBy(albumArtistKeySelector, AlbumArtistTupleComparer.Instance)
                        .Select(g => new
                        {
                            // Get original casing from the first song in the group for display purposes
                            AlbumTitle = g.First().Album,
                            ArtistName = g.First().Artist,
                            FirstSongWithThumb = g.FirstOrDefault(s => s.Thumbnail != null)
                        })
                        // Order the final list of unique albums (case-insensitive)
                        .OrderBy(a => a.ArtistName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(a => a.AlbumTitle, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (var albumData in uniqueAlbums)
                    {
                        Albums.Add(new AlbumViewModel
                        {
                            Title = albumData.AlbumTitle,
                            Artist = albumData.ArtistName,
                            Thumbnail = albumData.FirstSongWithThumb?.Thumbnail ?? defaultThumb
                        });
                    }
                    OnPropertyChanged(nameof(Albums));

                    ApplyFilter();
                    if (!_allSongs.Any() && settings.MusicDirectories.Any()) StatusBarText = "No compatible songs found in specified directories.";
                    else if (_allSongs.Any()) StatusBarText = $"Loaded {_allSongs.Count} songs.";
                    else StatusBarText = "Library empty. Add directories via File menu.";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainVM] Error during music library loading: {ex}");
                await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "Error loading music library.");
            }
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "No music directories configured. Add one via File > Settings.");
        }
        IsLoadingLibrary = false;
        UpdateStatusBarText();
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;
        var currentSettingsBeforeDialog = _settingsService.LoadSettings();
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };
        await settingsDialog.ShowDialog(owner);
        if (settingsVM.SettingsChanged)
        {
            var newSettingsAfterDialog = _settingsService.LoadSettings();
            bool dirsActuallyChanged = !currentSettingsBeforeDialog.MusicDirectories.SequenceEqual(newSettingsAfterDialog.MusicDirectories);
            bool themeActuallyChanged = currentSettingsBeforeDialog.PreferredThemeFileName != newSettingsAfterDialog.PreferredThemeFileName;
            if (dirsActuallyChanged)
            {
                await LoadMusicLibrary();
            }
            if (themeActuallyChanged)
            {
                StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect.";
            }
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;

        // Use Avalonia's StorageProvider for folder picking
        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Music Directory",
            AllowMultiple = false
        });

        if (result != null && result.Count > 0)
        {
            // For IStorageFolder, get the path. It might be a URI string or a local path.
            // The .Path property of IStorageFolder gives an AbsolutePath struct.
            // .LocalPath is what we usually want for local file system access.
            string? folderPath = result[0].Path.LocalPath; // Preferred way for local paths

            if (string.IsNullOrEmpty(folderPath) && result[0].Path.IsAbsoluteUri) // Fallback if LocalPath is empty but it's a URI
            {
                try { folderPath = new Uri(result[0].Path.ToString()).LocalPath; } // Try to convert URI to LocalPath
                catch { folderPath = null; Debug.WriteLine($"[MainVM] Could not convert folder URI to local path: {result[0].Path}"); }
            }

            if (!string.IsNullOrEmpty(folderPath))
            {
                var settings = _settingsService.LoadSettings();
                if (!settings.MusicDirectories.Contains(folderPath))
                {
                    settings.MusicDirectories.Add(folderPath);
                    _settingsService.SaveSettings(settings);
                    await LoadMusicLibrary();
                }
            }
            else
            {
                Debug.WriteLine($"[MainVM] Selected folder path could not be determined or is not a local path: {result[0].Name}");
                // Optionally inform the user
            }
        }
    }
}