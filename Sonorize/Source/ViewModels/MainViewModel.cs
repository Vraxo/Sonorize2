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
using Sonorize.Utils; // For AlbumArtistTupleComparer

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

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
            if (SetProperty(ref _selectedArtist, value)) // SetProperty raises PropertyChanged
            {
                if (value != null) // If an actual artist is selected (not null)
                {
                    OnArtistSelected(value); // Call the handler method
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
            if (SetProperty(ref _selectedAlbum, value)) // SetProperty raises PropertyChanged
            {
                if (value != null) // If an actual album is selected (not null)
                {
                    OnAlbumSelected(value); // Call the handler method
                }
            }
        }
    }

    private int _activeTabIndex = 0;
    public int ActiveTabIndex { get => _activeTabIndex; set => SetProperty(ref _activeTabIndex, value); }

    private bool _isCurrentLoopActiveUiBinding;
    public bool IsCurrentLoopActiveUiBinding
    {
        get => _isCurrentLoopActiveUiBinding;
        set
        {
            if (SetProperty(ref _isCurrentLoopActiveUiBinding, value))
            {
                if (PlaybackService.CurrentSong != null && PlaybackService.CurrentSong.SavedLoop != null)
                {
                    PlaybackService.CurrentSong.IsLoopActive = value;
                }
                else if (PlaybackService.CurrentSong != null && PlaybackService.CurrentSong.SavedLoop == null && value == true)
                {
                    _isCurrentLoopActiveUiBinding = false;
                    OnPropertyChanged(nameof(IsCurrentLoopActiveUiBinding));
                    Debug.WriteLine($"[MainVM] Attempted to activate loop via UI, but no loop is defined for {PlaybackService.CurrentSong.Title}.");
                }
            }
        }
    }

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

    private TimeSpan? _newLoopStartCandidate;
    public TimeSpan? NewLoopStartCandidate { get => _newLoopStartCandidate; set { SetProperty(ref _newLoopStartCandidate, value); OnPropertyChanged(nameof(CanSaveLoopRegion)); OnPropertyChanged(nameof(NewLoopStartCandidateDisplay)); } }

    private TimeSpan? _newLoopEndCandidate;
    public TimeSpan? NewLoopEndCandidate { get => _newLoopEndCandidate; set { SetProperty(ref _newLoopEndCandidate, value); OnPropertyChanged(nameof(CanSaveLoopRegion)); OnPropertyChanged(nameof(NewLoopEndCandidateDisplay)); } }

    public string NewLoopStartCandidateDisplay => _newLoopStartCandidate.HasValue ? $"{_newLoopStartCandidate.Value:mm\\:ss\\.ff}" : "Not set";
    public string NewLoopEndCandidateDisplay => _newLoopEndCandidate.HasValue ? $"{_newLoopEndCandidate.Value:mm\\:ss\\.ff}" : "Not set";

    private string _activeLoopDisplayText = "No loop defined.";
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
    public ICommand ToggleLoopActiveCommand { get; }
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
        ToggleAdvancedPanelCommand = new RelayCommand(_ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible, _ => (SelectedSong != null || PlaybackService.CurrentSong != null) && !IsLoadingLibrary);
        CaptureLoopStartCandidateCommand = new RelayCommand(_ => NewLoopStartCandidate = PlaybackService.CurrentPosition, _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentPlaybackStatus != PlaybackStateStatus.Stopped);
        CaptureLoopEndCandidateCommand = new RelayCommand(_ => NewLoopEndCandidate = PlaybackService.CurrentPosition, _ => PlaybackService.CurrentSong != null && PlaybackService.CurrentPlaybackStatus != PlaybackStateStatus.Stopped);
        SaveLoopCommand = new RelayCommand(SaveLoopAction, _ => CanSaveLoopRegion);
        ClearLoopCommand = new RelayCommand(ClearSavedLoopAction, _ => PlaybackService.CurrentSong?.SavedLoop != null);
        ToggleLoopActiveCommand = new RelayCommand(ToggleCurrentSongLoopActive, _ => PlaybackService.CurrentSong?.SavedLoop != null);
        WaveformSeekCommand = new RelayCommand(timeSpanObj => { if (timeSpanObj is TimeSpan ts && PlaybackService.CurrentSong != null) PlaybackService.Seek(ts); }, _ => PlaybackService.CurrentSong != null);

        PlaybackService.PropertyChanged += OnPlaybackServicePropertyChanged;
        PlaybackSpeed = 1.0;
        PlaybackPitch = 0.0;
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
        if (IsAdvancedPanelVisible && PlaybackService.CurrentSong != null && (!WaveformRenderData.Any() || !IsWaveformLoading))
        {
            _ = LoadWaveformForCurrentSong();
        }
    }

    private void UpdateAllUIDependentStates()
    {
        OnPropertyChanged(nameof(HasCurrentSong));
        UpdateLoopEditorForCurrentSong();
        UpdateActiveLoopDisplayText();
        UpdateStatusBarText();
        RaiseAllCommandsCanExecuteChanged();
    }

    private void RaiseAllCommandsCanExecuteChanged()
    {
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleLoopActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (WaveformSeekCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void HandleSelectedSongChange(Song? oldSong, Song? newSong)
    {
        Debug.WriteLine($"[MainVM] HandleSelectedSongChange. Old: {oldSong?.Title ?? "null"}, New: {newSong?.Title ?? "null"}");
        if (oldSong != null)
        {
            oldSong.PropertyChanged -= OnCurrentSongSavedLoopChanged;
            oldSong.PropertyChanged -= OnCurrentSongIsLoopActiveChanged;
        }

        if (newSong != null)
        {
            if (newSong != PlaybackService.CurrentSong || PlaybackService.CurrentPlaybackStatus == PlaybackStateStatus.Stopped)
            {
                PlaybackService.Play(newSong);
            }
            else
            {
                IsCurrentLoopActiveUiBinding = newSong.IsLoopActive;
            }

            if (_selectedSongInternal != null)
            {
                _selectedSongInternal.PropertyChanged += OnCurrentSongSavedLoopChanged;
                _selectedSongInternal.PropertyChanged += OnCurrentSongIsLoopActiveChanged;
            }
        }
        else
        {
            if (PlaybackService.CurrentSong != null)
            {
                PlaybackService.Stop();
            }
        }
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void OnPlaybackServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (args.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    var currentServiceSong = PlaybackService.CurrentSong;
                    Debug.WriteLine($"[MainVM_PSPChanged] Service.CurrentSong is now: {currentServiceSong?.Title ?? "null"}");

                    if (_selectedSongInternal != null && _selectedSongInternal != currentServiceSong)
                    {
                        _selectedSongInternal.PropertyChanged -= OnCurrentSongSavedLoopChanged;
                        _selectedSongInternal.PropertyChanged -= OnCurrentSongIsLoopActiveChanged;
                    }

                    if (currentServiceSong != null)
                    {
                        IsCurrentLoopActiveUiBinding = currentServiceSong.IsLoopActive;
                        if (_selectedSongInternal != currentServiceSong)
                        {
                            currentServiceSong.PropertyChanged += OnCurrentSongSavedLoopChanged;
                            currentServiceSong.PropertyChanged += OnCurrentSongIsLoopActiveChanged;
                        }
                    }
                    else
                    {
                        IsCurrentLoopActiveUiBinding = false;
                        WaveformRenderData.Clear();
                        OnPropertyChanged(nameof(WaveformRenderData));
                    }
                    UpdateAllUIDependentStates();
                    if (currentServiceSong != null) _ = LoadWaveformForCurrentSong();
                    break;

                case nameof(PlaybackService.IsPlaying):
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    UpdateStatusBarText();
                    RaiseAllCommandsCanExecuteChanged();
                    break;

                case nameof(PlaybackService.CurrentPosition):
                case nameof(PlaybackService.CurrentSongDuration):
                    OnPropertyChanged(nameof(CanSaveLoopRegion));
                    RaiseAllCommandsCanExecuteChanged();
                    break;
            }
        });
    }

    private void OnCurrentSongSavedLoopChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Song.SavedLoop) && sender is Song song && song == PlaybackService.CurrentSong)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Debug.WriteLine($"[MainVM_SongPChanged] SavedLoop changed for {song.Title}. Active: {song.IsLoopActive}");
                UpdateActiveLoopDisplayText();
                UpdateStatusBarText();
                (ToggleLoopActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
                if (song.SavedLoop != null && !song.IsLoopActive)
                {
                    song.IsLoopActive = true;
                }
                else if (song.SavedLoop == null && song.IsLoopActive)
                {
                    song.IsLoopActive = false;
                }
                IsCurrentLoopActiveUiBinding = song.IsLoopActive;
            });
        }
    }

    private void OnCurrentSongIsLoopActiveChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Song.IsLoopActive) && sender is Song song && song == PlaybackService.CurrentSong)
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Debug.WriteLine($"[MainVM_SongPChanged] IsLoopActive changed to {song.IsLoopActive} for {song.Title}. Persisting.");
                if (song.SavedLoop != null)
                {
                    _loopDataService.UpdateLoopActiveState(song.FilePath, song.IsLoopActive);
                }
                if (_isCurrentLoopActiveUiBinding != song.IsLoopActive)
                {
                    IsCurrentLoopActiveUiBinding = song.IsLoopActive;
                }
                UpdateActiveLoopDisplayText();
                UpdateStatusBarText();
            });
        }
    }

    private void ToggleCurrentSongLoopActive(object? parameter)
    {
        if (PlaybackService.CurrentSong != null && PlaybackService.CurrentSong.SavedLoop != null)
        {
            IsCurrentLoopActiveUiBinding = !IsCurrentLoopActiveUiBinding;
        }
    }

    // Handler methods that were missing from a previous condensed version
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
            SelectedSong = null;
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
            var points = await _waveformService.GetWaveformAsync(songToLoadWaveformFor.FilePath, 1000);
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

    private void UpdateLoopEditorForCurrentSong()
    {
        var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong?.SavedLoop != null)
        {
            NewLoopStartCandidate = currentServiceSong.SavedLoop.Start;
            NewLoopEndCandidate = currentServiceSong.SavedLoop.End;
            IsCurrentLoopActiveUiBinding = currentServiceSong.IsLoopActive;
        }
        else
        {
            NewLoopStartCandidate = null; NewLoopEndCandidate = null; IsCurrentLoopActiveUiBinding = false;
        }
        OnPropertyChanged(nameof(CanSaveLoopRegion));
        RaiseAllCommandsCanExecuteChanged();
    }

    private void ClearLoopCandidateInputs() { NewLoopStartCandidate = null; NewLoopEndCandidate = null; RaiseAllCommandsCanExecuteChanged(); }
    public bool CanSaveLoopRegion => PlaybackService.CurrentSong != null && NewLoopStartCandidate.HasValue && NewLoopEndCandidate.HasValue && NewLoopEndCandidate.Value > NewLoopStartCandidate.Value && NewLoopEndCandidate.Value <= PlaybackService.CurrentSongDuration && NewLoopStartCandidate.Value >= TimeSpan.Zero;

    private void SaveLoopAction(object? param)
    {
        var currentServiceSong = PlaybackService.CurrentSong;
        if (!CanSaveLoopRegion || currentServiceSong == null || !NewLoopStartCandidate.HasValue || !NewLoopEndCandidate.HasValue) return;
        var newLoop = new LoopRegion(NewLoopStartCandidate.Value, NewLoopEndCandidate.Value, "User Loop");
        bool shouldBeActive = (currentServiceSong.SavedLoop != null && currentServiceSong.IsLoopActive) || currentServiceSong.SavedLoop == null;
        currentServiceSong.SavedLoop = newLoop;
        if (currentServiceSong.IsLoopActive != shouldBeActive) { currentServiceSong.IsLoopActive = shouldBeActive; }
        else { _loopDataService.SetLoop(currentServiceSong.FilePath, newLoop.Start, newLoop.End, currentServiceSong.IsLoopActive); }
        Debug.WriteLine($"[MainVM] Loop saved for {currentServiceSong.Title}. Active: {currentServiceSong.IsLoopActive}");
    }

    private void ClearSavedLoopAction(object? param)
    {
        var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong != null)
        {
            var filePath = currentServiceSong.FilePath;
            currentServiceSong.SavedLoop = null; currentServiceSong.IsLoopActive = false;
            if (!string.IsNullOrEmpty(filePath)) { _loopDataService.ClearLoop(filePath); }
        }
        ClearLoopCandidateInputs();
    }

    private void UpdateActiveLoopDisplayText()
    {
        var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong?.SavedLoop != null)
        {
            var loop = currentServiceSong.SavedLoop; string activeStatus = currentServiceSong.IsLoopActive ? " (Active)" : " (Inactive)";
            ActiveLoopDisplayText = $"Loop: {loop.Start:mm\\:ss\\.f} - {loop.End:mm\\:ss\\.f}{activeStatus}";
        }
        else ActiveLoopDisplayText = "No loop defined.";
    }

    private void UpdateStatusBarText()
    {
        if (IsLoadingLibrary) return; string status; var currentServiceSong = PlaybackService.CurrentSong;
        if (currentServiceSong != null)
        {
            string stateStr = PlaybackService.CurrentPlaybackStatus switch { PlaybackStateStatus.Playing => "Playing", PlaybackStateStatus.Paused => "Paused", PlaybackStateStatus.Stopped => "Stopped", _ => "Idle" };
            status = $"{stateStr}: {currentServiceSong.Title}";
            if (currentServiceSong.SavedLoop != null && currentServiceSong.IsLoopActive) status += $" (Loop Active)";
        }
        else
        {
            status = $"Sonorize - {FilteredSongs.Count} of {_allSongs.Count} songs displayed.";
            if (_allSongs.Count == 0 && !IsLoadingLibrary && !_settingsService.LoadSettings().MusicDirectories.Any()) status = "Sonorize - Library empty. Add directories via File menu.";
            else if (_allSongs.Count == 0 && !IsLoadingLibrary && _settingsService.LoadSettings().MusicDirectories.Any()) status = "Sonorize - No songs found in configured directories.";
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
                await Task.Run(async () => { await _musicLibraryService.LoadMusicFromDirectoriesAsync(settings.MusicDirectories, song => Dispatcher.UIThread.InvokeAsync(() => _allSongs.Add(song)), s => Dispatcher.UIThread.InvokeAsync(() => StatusBarText = s)); });
                await Dispatcher.UIThread.InvokeAsync(() => {
                    Artists.Clear(); var uniqueArtistNames = _allSongs.Select(s => s.Artist).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
                    Bitmap? defaultThumb = _musicLibraryService.GetDefaultThumbnail(); foreach (var artistName in uniqueArtistNames!) { Bitmap? repThumb = _allSongs.FirstOrDefault(s => (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false) && s.Thumbnail != null)?.Thumbnail ?? defaultThumb; Artists.Add(new ArtistViewModel { Name = artistName, Thumbnail = repThumb }); }
                    OnPropertyChanged(nameof(Artists));
                    Albums.Clear(); Func<Song, (string Album, string Artist)> keySelector = s => (s.Album?.Trim() ?? string.Empty, s.Artist?.Trim() ?? string.Empty);
                    var uniqueAlbums = _allSongs.Where(s => !string.IsNullOrWhiteSpace(s.Album) && !string.IsNullOrWhiteSpace(s.Artist)).GroupBy(keySelector, AlbumArtistTupleComparer.Instance)
                        .Select(g => new { AlbumTitle = g.First().Album, ArtistName = g.First().Artist, ThumbSong = g.FirstOrDefault(s => s.Thumbnail != null) })
                        .OrderBy(a => a.ArtistName, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.AlbumTitle, StringComparer.OrdinalIgnoreCase).ToList();
                    foreach (var albumData in uniqueAlbums) Albums.Add(new AlbumViewModel { Title = albumData.AlbumTitle, Artist = albumData.ArtistName, Thumbnail = albumData.ThumbSong?.Thumbnail ?? defaultThumb }); OnPropertyChanged(nameof(Albums));
                    ApplyFilter();
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
            var newSettingsAfterDialog = _settingsService.LoadSettings(); bool dirsActuallyChanged = !currentSettingsBeforeDialog.MusicDirectories.SequenceEqual(newSettingsAfterDialog.MusicDirectories);
            bool themeActuallyChanged = currentSettingsBeforeDialog.PreferredThemeFileName != newSettingsAfterDialog.PreferredThemeFileName;
            if (dirsActuallyChanged) await LoadMusicLibrary();
            if (themeActuallyChanged) StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect.";
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