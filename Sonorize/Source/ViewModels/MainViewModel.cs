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

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    public ObservableCollection<Song> Songs { get; } = new();

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
            if (SetProperty(ref _playbackSpeed, Math.Max(0.25, Math.Min(value, 4.0))))
            {
                PlaybackService.PlaybackRate = (float)_playbackSpeed;
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
            if (SetProperty(ref _playbackPitch, Math.Max(-5, Math.Min(value, 5)))) // Clamped to -5 to 5
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

        CaptureLoopStartCandidateCommand = new RelayCommand(_ => NewLoopStartCandidate = PlaybackService.CurrentPosition.Divide(PlaybackService.PlaybackRate), _ => PlaybackService.CurrentSong != null);
        CaptureLoopEndCandidateCommand = new RelayCommand(_ => NewLoopEndCandidate = PlaybackService.CurrentPosition.Divide(PlaybackService.PlaybackRate), _ => PlaybackService.CurrentSong != null);
        SaveNewLoopRegionCommand = new RelayCommand(SaveLoopCandidateAction, _ => CanSaveNewLoopRegion);
        ActivateLoopRegionCommand = new RelayCommand(_ => { if (PlaybackService.CurrentSong != null && SelectedEditableLoopRegion != null) PlaybackService.CurrentSong.ActiveLoop = SelectedEditableLoopRegion; }, _ => CanActivateLoopRegion());
        DeactivateActiveLoopCommand = new RelayCommand(_ => { if (PlaybackService.CurrentSong != null) PlaybackService.CurrentSong.ActiveLoop = null; }, _ => PlaybackService.CurrentSong?.ActiveLoop != null);
        DeleteLoopRegionCommand = new RelayCommand(DeleteSelectedLoopRegionAction, _ => SelectedEditableLoopRegion != null);
        WaveformSeekCommand = new RelayCommand(timeSpan => PlaybackService.Seek((TimeSpan)timeSpan!), _ => PlaybackService.CurrentSong != null);

        PlaybackService.PropertyChanged += OnPlaybackServicePropertyChanged;
        PlaybackSpeed = 1.0;
        PlaybackPitch = 0.0;
        UpdateStatusBarText();
        UpdateActiveLoopDisplayText();
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
            case nameof(PlaybackService.IsPlaying): // IsPlaying changes
            case nameof(PlaybackService.CurrentPlaybackStatus): // CurrentPlaybackStatus also changes
                UpdateStatusBarText();
                break;
            case nameof(PlaybackService.CurrentPosition):
            case nameof(PlaybackService.CurrentSongDuration):
                (SaveNewLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

    public bool CanSaveNewLoopRegion => PlaybackService.CurrentSong != null &&
                                        NewLoopStartCandidate.HasValue &&
                                        NewLoopEndCandidate.HasValue &&
                                        NewLoopEndCandidate.Value > NewLoopStartCandidate.Value &&
                                        NewLoopEndCandidate.Value <= PlaybackService.CurrentSongDuration.Divide(PlaybackService.PlaybackRate) &&
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
            // Display loop times as they are (unadjusted by speed for clarity in definition)
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
            string stateStr = PlaybackService.CurrentPlaybackStatus switch // Use the new public property
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
            status = $"Sonorize - {Songs.Count} songs loaded.";
        }

        if (PlaybackService.CurrentSong?.ActiveLoop != null) status += $" (Loop: {PlaybackService.CurrentSong.ActiveLoop.Name})";
        StatusBarText = status;
    }

    private async Task LoadMusicLibrary()
    {
        if (IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;
        IsLoadingLibrary = true;
        var settings = _settingsService.LoadSettings();
        await Dispatcher.UIThread.InvokeAsync(() => { Songs.Clear(); StatusBarText = "Preparing..."; });
        if (settings.MusicDirectories.Any())
        {
            await Task.Run(async () => { try { await _musicLibraryService.LoadMusicFromDirectoriesAsync(settings.MusicDirectories, song => Songs.Add(song), s => StatusBarText = s); } catch (Exception ex) { Debug.WriteLine($"[MainVM] LoadMusicErr: {ex}"); await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "Error loading library."); } });
            await Dispatcher.UIThread.InvokeAsync(() => { StatusBarText = $"{Songs.Count} songs loaded."; if (!Songs.Any() && settings.MusicDirectories.Any()) StatusBarText = "No songs found."; });
        }
        else await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "No music directories.");
        IsLoadingLibrary = false; UpdateStatusBarText();
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };
        await settingsDialog.ShowDialog(owner);
        if (settingsVM.SettingsChanged) await LoadMusicLibrary();
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false;
        var resultPath = await new OpenFolderDialog { Title = "Select Music Directory" }.ShowAsync(owner);
        if (!string.IsNullOrEmpty(resultPath))
        {
            var settings = _settingsService.LoadSettings();
            if (!settings.MusicDirectories.Contains(resultPath)) { settings.MusicDirectories.Add(resultPath); _settingsService.SaveSettings(settings); await LoadMusicLibrary(); }
        }
    }
}