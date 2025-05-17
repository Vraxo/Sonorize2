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

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    // ... (existing properties and constructor a Ttributes) ...
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    private readonly ObservableCollection<Song> _allSongs = new();
    public ObservableCollection<Song> FilteredSongs { get; } = new();
    public ObservableCollection<ArtistViewModel> Artists { get; } = new();

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
            var previousSong = _selectedSongInternal;
            if (SetProperty(ref _selectedSongInternal, value))
            {
                HandleSelectedSongChange(previousSong, _selectedSongInternal);
            }
        }
    }

    private ArtistViewModel? _selectedArtist;
    public ArtistViewModel? SelectedArtist
    {
        get => _selectedArtist;
        set
        {
            // We set the property but the main action happens in OnArtistSelected
            // if we were to bind this to ListBox.SelectedItem.
            // For a command-like action, we'd pass the artist to a command.
            // Let's handle it via a method called when selection changes.
            if (SetProperty(ref _selectedArtist, value))
            {
                if (value != null)
                {
                    OnArtistSelected(value);
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
        if (oldSong != null)
        {
            oldSong.PropertyChanged -= OnCurrentSongActiveLoopChanged;
        }
        if (newSong != null)
        {
            newSong.PropertyChanged += OnCurrentSongActiveLoopChanged;
            PlaybackService.Play(newSong);
            await LoadWaveformForCurrentSong();
        }
        else
        {
            PlaybackService.Stop();
            WaveformRenderData.Clear();
            OnPropertyChanged(nameof(WaveformRenderData));
            IsAdvancedPanelVisible = false;
        }

        UpdateLoopEditorForCurrentSong();
        UpdateActiveLoopDisplayText();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(PlaybackService.HasCurrentSong));
    }

    private void OnArtistSelected(ArtistViewModel artist)
    {
        if (artist?.Name == null) return;

        Debug.WriteLine($"[MainVM] Artist selected: {artist.Name}");
        SearchQuery = artist.Name; // This will trigger ApplyFilter
        ActiveTabIndex = 0;       // Switch to Library tab (index 0)

        // Optionally, deselect artist in the list after action to allow re-clicking
        // This depends on desired UX. For now, let it stay selected.
        // If we want to make it a pure "action" that deselects:
        // Dispatcher.UIThread.Post(() => SelectedArtist = null, DispatcherPriority.Background);
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
            if (SetProperty(ref _playbackPitch, Math.Max(-4, Math.Min(value, 4))))
            {
                PlaybackService.PitchSemitones = (float)_playbackPitch;
                OnPropertyChanged(nameof(PlaybackPitchDisplay));
            }
        }
    }
    public string PlaybackPitchDisplay => $"{_playbackPitch:+0.0;-0.0;0} st";

    public ObservableCollection<WaveformPoint> WaveformRenderData { get; } = new();
    private bool _isWaveformLoading = false;
    public bool IsWaveformLoading { get => _isWaveformLoading; private set => SetProperty(ref _isWaveformLoading, value); }

    public ObservableCollection<LoopRegion> EditableLoopRegions { get; } = new();
    private LoopRegion? _selectedEditableLoopRegion;
    public LoopRegion? SelectedEditableLoopRegion { get => _selectedEditableLoopRegion; set { if (SetProperty(ref _selectedEditableLoopRegion, value)) OnSelectedEditableLoopRegionChanged(); } }
    private void OnSelectedEditableLoopRegionChanged()
    {
        (ActivateLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        if (_selectedEditableLoopRegion != null) NewLoopNameInput = _selectedEditableLoopRegion.Name;
    }
    private string _newLoopNameInput = "New Loop";
    public string NewLoopNameInput { get => _newLoopNameInput; set => SetProperty(ref _newLoopNameInput, value); }

    private TimeSpan? _newLoopStartCandidate;
    public TimeSpan? NewLoopStartCandidate { get => _newLoopStartCandidate; set { SetProperty(ref _newLoopStartCandidate, value); OnPropertyChanged(nameof(CanSaveNewLoopRegion)); OnPropertyChanged(nameof(NewLoopStartCandidateDisplay)); } }

    private TimeSpan? _newLoopEndCandidate;
    public TimeSpan? NewLoopEndCandidate { get => _newLoopEndCandidate; set { SetProperty(ref _newLoopEndCandidate, value); OnPropertyChanged(nameof(CanSaveNewLoopRegion)); OnPropertyChanged(nameof(NewLoopEndCandidateDisplay)); } }

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
    public ICommand SaveNewLoopRegionCommand { get; }
    public ICommand ActivateLoopRegionCommand { get; }
    public ICommand DeactivateActiveLoopCommand { get; }
    public ICommand DeleteLoopRegionCommand { get; }
    public ICommand WaveformSeekCommand { get; }

    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme;
        _waveformService = waveformService;

        LoadInitialDataCommand = new RelayCommand(async _ => await LoadMusicLibrary(), _ => !IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !IsLoadingLibrary);

        ToggleAdvancedPanelCommand = new RelayCommand(
            _ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible,
            _ => PlaybackService.CurrentSong != null && !IsLoadingLibrary);

        CaptureLoopStartCandidateCommand = new RelayCommand(_ => NewLoopStartCandidate = PlaybackService.CurrentPosition, _ => PlaybackService.CurrentSong != null);
        CaptureLoopEndCandidateCommand = new RelayCommand(_ => NewLoopEndCandidate = PlaybackService.CurrentPosition, _ => PlaybackService.CurrentSong != null);

        SaveNewLoopRegionCommand = new RelayCommand(SaveLoopCandidateAction, _ => CanSaveNewLoopRegion);
        ActivateLoopRegionCommand = new RelayCommand(_ => { if (PlaybackService.CurrentSong != null && SelectedEditableLoopRegion != null) PlaybackService.CurrentSong.ActiveLoop = SelectedEditableLoopRegion; }, _ => CanActivateLoopRegion());
        DeactivateActiveLoopCommand = new RelayCommand(_ => { if (PlaybackService.CurrentSong != null) PlaybackService.CurrentSong.ActiveLoop = null; }, _ => PlaybackService.CurrentSong?.ActiveLoop != null);
        DeleteLoopRegionCommand = new RelayCommand(DeleteSelectedLoopRegionAction, _ => SelectedEditableLoopRegion != null);
        WaveformSeekCommand = new RelayCommand(timeSpanObj => { if (timeSpanObj is TimeSpan ts) PlaybackService.Seek(ts); }, _ => PlaybackService.CurrentSong != null);

        PlaybackService.PropertyChanged += OnPlaybackServicePropertyChanged;
        PlaybackSpeed = 1.0;
        PlaybackPitch = 0.0;
        UpdateStatusBarText();
        UpdateActiveLoopDisplayText();
    }

    private void ApplyFilter()
    {
        FilteredSongs.Clear();
        var songsToFilter = _allSongs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant().Trim();
            // If the query matches an exact artist name (common after artist selection), prioritize that.
            // Otherwise, perform a broader search.
            // This logic can be refined. For now, the existing broader search is fine.
            songsToFilter = songsToFilter.Where(s =>
                (s.Title?.ToLowerInvariant().Contains(query) ?? false) ||
                (s.Artist?.ToLowerInvariant().Contains(query) ?? false));
        }

        foreach (var song in songsToFilter.OrderBy(s => s.Artist).ThenBy(s => s.Title))
        {
            FilteredSongs.Add(song);
        }

        if (SelectedSong != null && !FilteredSongs.Contains(SelectedSong))
        {
            SelectedSong = null; // Deselect if it's no longer in the filtered list
        }
        else if (FilteredSongs.Any() && SelectedSong == null && songsToFilter.Count() == _allSongs.Count)
        {
            // This case is tricky: if filter cleared and no song selected, do nothing.
            // If filter applied (e.g. artist selected) and results exist, consider selecting the first one.
            // For now, let manual selection in Library tab handle it.
        }
        UpdateStatusBarText();
    }


    private bool CanActivateLoopRegion() => PlaybackService.CurrentSong != null && SelectedEditableLoopRegion != null && PlaybackService.CurrentSong.ActiveLoop != SelectedEditableLoopRegion;

    private async Task LoadWaveformForCurrentSong()
    {
        if (PlaybackService.CurrentSong == null || string.IsNullOrEmpty(PlaybackService.CurrentSong.FilePath))
        {
            WaveformRenderData.Clear();
            OnPropertyChanged(nameof(WaveformRenderData));
            return;
        }

        IsWaveformLoading = true;
        try
        {
            int targetWaveformPoints = 1000;
            var points = await _waveformService.GetWaveformAsync(PlaybackService.CurrentSong.FilePath, targetWaveformPoints);
            WaveformRenderData.Clear();
            foreach (var p in points) WaveformRenderData.Add(p);
            OnPropertyChanged(nameof(WaveformRenderData));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ViewModel] Failed to load waveform: {ex.Message}");
            WaveformRenderData.Clear();
            OnPropertyChanged(nameof(WaveformRenderData));
        }
        finally
        {
            IsWaveformLoading = false;
        }
    }

    private void OnPlaybackServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(PlaybackService.CurrentSong):
                (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
                break;
            case nameof(PlaybackService.IsPlaying):
            case nameof(PlaybackService.CurrentPlaybackStatus):
                UpdateStatusBarText();
                break;
            case nameof(PlaybackService.CurrentPosition):
            case nameof(PlaybackService.CurrentSongDuration):
                (SaveNewLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
                break;
        }
    }

    private void OnCurrentSongActiveLoopChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Song.ActiveLoop))
        {
            UpdateActiveLoopDisplayText();
            UpdateStatusBarText();
            (ActivateLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeactivateActiveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private void UpdateLoopEditorForCurrentSong()
    {
        EditableLoopRegions.Clear();
        if (PlaybackService.CurrentSong != null)
        {
            foreach (var loop in PlaybackService.CurrentSong.LoopRegions) EditableLoopRegions.Add(loop);
            NewLoopNameInput = $"Loop {EditableLoopRegions.Count + 1}";
        }
        else NewLoopNameInput = "New Loop";

        NewLoopStartCandidate = null;
        NewLoopEndCandidate = null;
        SelectedEditableLoopRegion = null;

        (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveNewLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ActivateLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeactivateActiveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(CanSaveNewLoopRegion));
    }

    private void ClearLoopCandidate()
    {
        NewLoopNameInput = $"Loop {(PlaybackService.CurrentSong?.LoopRegions.Count ?? 0) + 1}";
        NewLoopStartCandidate = null;
        NewLoopEndCandidate = null;
        OnPropertyChanged(nameof(CanSaveNewLoopRegion));
    }

    public bool CanSaveNewLoopRegion =>
        PlaybackService.CurrentSong != null &&
        NewLoopStartCandidate.HasValue &&
        NewLoopEndCandidate.HasValue &&
        NewLoopEndCandidate.Value > NewLoopStartCandidate.Value &&
        NewLoopEndCandidate.Value <= PlaybackService.CurrentSongDuration &&
        NewLoopStartCandidate.Value >= TimeSpan.Zero &&
        !string.IsNullOrWhiteSpace(NewLoopNameInput);

    private void SaveLoopCandidateAction(object? param)
    {
        if (!CanSaveNewLoopRegion || PlaybackService.CurrentSong == null || !NewLoopStartCandidate.HasValue || !NewLoopEndCandidate.HasValue) return;
        var newLoop = new LoopRegion(NewLoopStartCandidate.Value, NewLoopEndCandidate.Value, NewLoopNameInput);
        PlaybackService.CurrentSong.LoopRegions.Add(newLoop);
        EditableLoopRegions.Add(newLoop);
        ClearLoopCandidate();
    }

    private void DeleteSelectedLoopRegionAction(object? param)
    {
        if (PlaybackService.CurrentSong != null && SelectedEditableLoopRegion != null)
        {
            if (PlaybackService.CurrentSong.ActiveLoop == SelectedEditableLoopRegion) PlaybackService.CurrentSong.ActiveLoop = null;
            PlaybackService.CurrentSong.LoopRegions.Remove(SelectedEditableLoopRegion);
            EditableLoopRegions.Remove(SelectedEditableLoopRegion);
            SelectedEditableLoopRegion = null;
            ClearLoopCandidate();
        }
    }

    private void UpdateActiveLoopDisplayText()
    {
        if (PlaybackService.CurrentSong?.ActiveLoop != null)
        {
            var loop = PlaybackService.CurrentSong.ActiveLoop;
            ActiveLoopDisplayText = $"Active Loop: {loop.Name} ({loop.Start:mm\\:ss\\.f} - {loop.End:mm\\:ss\\.f})";
        }
        else ActiveLoopDisplayText = "No active loop.";
    }

    private void UpdateStatusBarText()
    {
        if (IsLoadingLibrary) return;
        string status;

        if (PlaybackService.CurrentSong != null)
        {
            string stateStr = PlaybackService.CurrentPlaybackStatus switch
            {
                PlaybackStateStatus.Playing => "Playing",
                PlaybackStateStatus.Paused => "Paused",
                PlaybackStateStatus.Stopped => "Stopped",
                _ => "Idle"
            };
            status = $"{stateStr}: {PlaybackService.CurrentSong.Title}";
        }
        else
        {
            status = $"Sonorize - {FilteredSongs.Count} of {_allSongs.Count} songs displayed.";
            if (_allSongs.Count == 0 && !IsLoadingLibrary) status = "Sonorize - Library empty. Add directories via File menu.";
        }

        if (PlaybackService.CurrentSong?.ActiveLoop != null) status += $" (Loop: {PlaybackService.CurrentSong.ActiveLoop.Name})";
        StatusBarText = status;
    }

    private async Task LoadMusicLibrary()
    {
        if (IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;
        IsLoadingLibrary = true;
        SearchQuery = string.Empty; // Clear search on full reload
        var settings = _settingsService.LoadSettings();

        await Dispatcher.UIThread.InvokeAsync(() => {
            _allSongs.Clear();
            Artists.Clear();
            FilteredSongs.Clear(); // Also clear filtered songs
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

                var uniqueArtistNames = _allSongs
                    .Select(s => s.Artist)
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(a => a)
                    .ToList();

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    Artists.Clear();
                    Bitmap? defaultArtistThumb = _musicLibraryService.GetDefaultThumbnail();

                    foreach (var artistName in uniqueArtistNames)
                    {
                        Bitmap? representativeThumb = _allSongs
                            .FirstOrDefault(s => s.Artist.Equals(artistName, StringComparison.OrdinalIgnoreCase) && s.Thumbnail != null)
                            ?.Thumbnail;

                        Artists.Add(new ArtistViewModel
                        {
                            Name = artistName,
                            Thumbnail = representativeThumb ?? defaultArtistThumb
                        });
                    }
                    OnPropertyChanged(nameof(Artists));
                    ApplyFilter(); // This will populate FilteredSongs based on (now empty) SearchQuery
                    if (!_allSongs.Any() && settings.MusicDirectories.Any()) StatusBarText = "No compatible songs found in specified directories.";
                    else if (_allSongs.Any()) StatusBarText = $"Loaded {_allSongs.Count} songs.";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainVM] Error during music library loading: {ex}");
                await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "Error loading music library.");
            }
        }
        else await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "No music directories configured. Add one via File > Settings.");

        IsLoadingLibrary = false;
        UpdateStatusBarText(); // General update
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;
        var currentSettingsBeforeDialog = _settingsService.LoadSettings(); // Capture state before
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };
        await settingsDialog.ShowDialog(owner);

        if (settingsVM.SettingsChanged) // SettingsVM indicates a save happened
        {
            var newSettingsAfterDialog = _settingsService.LoadSettings(); // Get what was actually saved

            bool dirsActuallyChanged = !currentSettingsBeforeDialog.MusicDirectories.SequenceEqual(newSettingsAfterDialog.MusicDirectories);
            bool themeActuallyChanged = currentSettingsBeforeDialog.PreferredThemeFileName != newSettingsAfterDialog.PreferredThemeFileName;

            if (dirsActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Music directories changed in settings, reloading library.");
                await LoadMusicLibrary();
            }

            if (themeActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Theme changed in settings. Restart required for full effect.");
                StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect.";
            }
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;
        var resultPath = await new OpenFolderDialog { Title = "Select Music Directory" }.ShowAsync(owner);
        if (!string.IsNullOrEmpty(resultPath))
        {
            var settings = _settingsService.LoadSettings();
            if (!settings.MusicDirectories.Contains(resultPath))
            {
                settings.MusicDirectories.Add(resultPath);
                _settingsService.SaveSettings(settings);
                await LoadMusicLibrary();
            }
        }
    }
}