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

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    public ObservableCollection<Song> Songs { get; } = new();

    private Song? _selectedSongInternal; // To avoid re-triggering Play on programmatic selection
    public Song? SelectedSong
    {
        get => _selectedSongInternal;
        set
        {
            // Store previous song to detach property changed handler
            var previousSong = _selectedSongInternal;
            if (SetProperty(ref _selectedSongInternal, value))
            {
                if (previousSong != null)
                {
                    previousSong.PropertyChanged -= OnCurrentSongActiveLoopChanged;
                }
                if (_selectedSongInternal != null)
                {
                    _selectedSongInternal.PropertyChanged += OnCurrentSongActiveLoopChanged;
                    PlaybackService.Play(_selectedSongInternal); // Play the newly selected song
                }
                UpdateLoopEditorForCurrentSong();
                UpdateActiveLoopDisplayText();
                (OpenLoopEditorCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusBarText = "Welcome to Sonorize!";
    public string StatusBarText { get => _statusBarText; set => SetProperty(ref _statusBarText, value); }

    private bool _isLoadingLibrary = false;
    public bool IsLoadingLibrary
    {
        get => _isLoadingLibrary;
        set { if (SetProperty(ref _isLoadingLibrary, value)) UpdateCanExecuteStates(); }
    }

    // --- Loop Editor Properties ---
    private bool _isLoopEditorVisible;
    public bool IsLoopEditorVisible { get => _isLoopEditorVisible; set => SetProperty(ref _isLoopEditorVisible, value); }

    public ObservableCollection<LoopRegion> EditableLoopRegions { get; } = new();

    private LoopRegion? _selectedEditableLoopRegion;
    public LoopRegion? SelectedEditableLoopRegion
    {
        get => _selectedEditableLoopRegion;
        set
        {
            if (SetProperty(ref _selectedEditableLoopRegion, value))
            {
                (ActivateLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                if (value != null) NewLoopNameInput = value.Name; // Pre-fill for potential rename
            }
        }
    }

    private string _newLoopNameInput = "New Loop";
    public string NewLoopNameInput { get => _newLoopNameInput; set => SetProperty(ref _newLoopNameInput, value); }

    private TimeSpan? _newLoopStartCandidate;
    public TimeSpan? NewLoopStartCandidate { get => _newLoopStartCandidate; set => SetProperty(ref _newLoopStartCandidate, value, nameof(CanSaveNewLoopRegion)); }

    private TimeSpan? _newLoopEndCandidate;
    public TimeSpan? NewLoopEndCandidate { get => _newLoopEndCandidate; set => SetProperty(ref _newLoopEndCandidate, value, nameof(CanSaveNewLoopRegion)); }

    public string NewLoopStartCandidateDisplay => _newLoopStartCandidate.HasValue ? $"{_newLoopStartCandidate.Value:mm\\:ss}" : "Not set";
    public string NewLoopEndCandidateDisplay => _newLoopEndCandidate.HasValue ? $"{_newLoopEndCandidate.Value:mm\\:ss}" : "Not set";


    private string _activeLoopDisplayText = "No active loop.";
    public string ActiveLoopDisplayText { get => _activeLoopDisplayText; set => SetProperty(ref _activeLoopDisplayText, value); }


    // --- Commands ---
    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    // PlaySongCommand is effectively handled by SelectedSong setter
    public ICommand AddDirectoryAndRefreshCommand { get; }

    public ICommand OpenLoopEditorCommand { get; }
    public ICommand CloseLoopEditorCommand { get; }
    public ICommand CaptureLoopStartCandidateCommand { get; }
    public ICommand CaptureLoopEndCandidateCommand { get; }
    public ICommand SaveNewLoopRegionCommand { get; }
    public ICommand ActivateLoopRegionCommand { get; }
    public ICommand DeactivateActiveLoopCommand { get; }
    public ICommand DeleteLoopRegionCommand { get; }


    public MainWindowViewModel(
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme)
    {
        _settingsService = settingsService;
        _musicLibraryService = musicLibraryService;
        PlaybackService = playbackService;
        CurrentTheme = theme;

        LoadInitialDataCommand = new RelayCommand(async _ => await LoadMusicLibrary(), _ => !IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !IsLoadingLibrary && !IsLoopEditorVisible);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !IsLoadingLibrary && !IsLoopEditorVisible);

        // Loop Editor Commands
        OpenLoopEditorCommand = new RelayCommand(_ => IsLoopEditorVisible = true, _ => PlaybackService.CurrentSong != null && !IsLoopEditorVisible);
        CloseLoopEditorCommand = new RelayCommand(_ => { IsLoopEditorVisible = false; ClearLoopCandidate(); }, _ => IsLoopEditorVisible);
        CaptureLoopStartCandidateCommand = new RelayCommand(
            _ => NewLoopStartCandidate = PlaybackService.CurrentPosition,
            _ => PlaybackService.CurrentSong != null);
        CaptureLoopEndCandidateCommand = new RelayCommand(
            _ => NewLoopEndCandidate = PlaybackService.CurrentPosition,
            _ => PlaybackService.CurrentSong != null);
        SaveNewLoopRegionCommand = new RelayCommand(SaveLoopCandidateAction, CanSaveNewLoopRegion);
        ActivateLoopRegionCommand = new RelayCommand(
            _ => { if (PlaybackService.CurrentSong != null && SelectedEditableLoopRegion != null) PlaybackService.CurrentSong.ActiveLoop = SelectedEditableLoopRegion; },
            _ => PlaybackService.CurrentSong != null && SelectedEditableLoopRegion != null && PlaybackService.CurrentSong.ActiveLoop != SelectedEditableLoopRegion);
        DeactivateActiveLoopCommand = new RelayCommand(
            _ => { if (PlaybackService.CurrentSong != null) PlaybackService.CurrentSong.ActiveLoop = null; },
            _ => PlaybackService.CurrentSong?.ActiveLoop != null);
        DeleteLoopRegionCommand = new RelayCommand(DeleteSelectedLoopRegionAction, _ => SelectedEditableLoopRegion != null);


        PlaybackService.PropertyChanged += OnPlaybackServicePropertyChanged;
        UpdateStatusBarText();
        UpdateActiveLoopDisplayText();
    }

    private void OnPlaybackServicePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(PlaybackService.CurrentSong):
                // If the song itself changes, SelectedSong setter handles ActiveLoop related updates
                // (OpenLoopEditorCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Handled by SelectedSong
                break;
            case nameof(PlaybackService.IsPlaying):
                UpdateStatusBarText();
                break;
            case nameof(PlaybackService.CurrentPosition):
                OnPropertyChanged(nameof(NewLoopStartCandidateDisplay)); // Update display if editor is open
                OnPropertyChanged(nameof(NewLoopEndCandidateDisplay));
                (SaveNewLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
                break;
        }
    }

    private void OnCurrentSongActiveLoopChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Song.ActiveLoop))
        {
            Debug.WriteLine($"[MainVM] CurrentSong.ActiveLoop changed. New Active: {PlaybackService.CurrentSong?.ActiveLoop?.Name}");
            UpdateActiveLoopDisplayText();
            UpdateStatusBarText(); // Status bar might show loop info
            (ActivateLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeactivateActiveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }


    private void UpdateLoopEditorForCurrentSong()
    {
        EditableLoopRegions.Clear();
        ClearLoopCandidate();
        SelectedEditableLoopRegion = null;

        if (PlaybackService.CurrentSong != null)
        {
            foreach (var loop in PlaybackService.CurrentSong.LoopRegions)
            {
                EditableLoopRegions.Add(loop);
            }
        }
        // RaiseCanExecuteChanged for commands dependent on CurrentSong
        (OpenLoopEditorCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ActivateLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeactivateActiveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DeleteLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveNewLoopRegionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ClearLoopCandidate()
    {
        NewLoopNameInput = $"Loop {(PlaybackService.CurrentSong?.LoopRegions.Count ?? 0) + 1}";
        NewLoopStartCandidate = null;
        NewLoopEndCandidate = null;
        OnPropertyChanged(nameof(NewLoopStartCandidateDisplay));
        OnPropertyChanged(nameof(NewLoopEndCandidateDisplay));
    }

    private bool CanSaveNewLoopRegion(object? param)
    {
        return PlaybackService.CurrentSong != null &&
               NewLoopStartCandidate.HasValue &&
               NewLoopEndCandidate.HasValue &&
               NewLoopEndCandidate.Value > NewLoopStartCandidate.Value &&
               !string.IsNullOrWhiteSpace(NewLoopNameInput);
    }

    private void SaveLoopCandidateAction(object? param)
    {
        if (!CanSaveNewLoopRegion(null) || PlaybackService.CurrentSong == null || !NewLoopStartCandidate.HasValue || !NewLoopEndCandidate.HasValue) return;

        var newLoop = new LoopRegion(NewLoopStartCandidate.Value, NewLoopEndCandidate.Value, NewLoopNameInput);
        PlaybackService.CurrentSong.LoopRegions.Add(newLoop); // This will update EditableLoopRegions if it's directly bound or needs manual refresh

        // If EditableLoopRegions is a separate collection that mirrors CurrentSong.LoopRegions, add here too.
        // Assuming CurrentSong.LoopRegions IS the source for EditableLoopRegions (e.g. via direct binding or manual sync)
        // For simplicity now, we'll re-populate. A more complex sync would be better.
        UpdateLoopEditorForCurrentSong(); // Refreshes the list, selects nothing.
                                          // Consider just adding to EditableLoopRegions if it's bound directly.
                                          // EditableLoopRegions.Add(newLoop); // If it's a separate observable collection

        ClearLoopCandidate();
    }

    private void DeleteSelectedLoopRegionAction(object? param)
    {
        if (PlaybackService.CurrentSong != null && SelectedEditableLoopRegion != null)
        {
            if (PlaybackService.CurrentSong.ActiveLoop == SelectedEditableLoopRegion)
            {
                PlaybackService.CurrentSong.ActiveLoop = null;
            }
            PlaybackService.CurrentSong.LoopRegions.Remove(SelectedEditableLoopRegion);
            // UpdateLoopEditorForCurrentSong(); // To refresh the list. Or:
            EditableLoopRegions.Remove(SelectedEditableLoopRegion);
            SelectedEditableLoopRegion = null;
            ClearLoopCandidate(); // Reset add section
        }
    }


    private void UpdateActiveLoopDisplayText()
    {
        if (PlaybackService.CurrentSong?.ActiveLoop != null)
        {
            var loop = PlaybackService.CurrentSong.ActiveLoop;
            ActiveLoopDisplayText = $"Active Loop: {loop.Name} ({loop.Start:mm\\:ss} - {loop.End:mm\\:ss})";
        }
        else
        {
            ActiveLoopDisplayText = "No active loop.";
        }
    }

    private void UpdateCanExecuteStates()
    {
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenLoopEditorCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // ... any other commands that depend on IsLoadingLibrary or IsLoopEditorVisible
    }

    private void UpdateStatusBarText()
    {
        if (IsLoadingLibrary) return;

        string status;
        if (PlaybackService.IsPlaying && PlaybackService.CurrentSong != null)
        {
            status = $"Playing: {PlaybackService.CurrentSong.Title} - {PlaybackService.CurrentSong.Artist}";
        }
        else if (PlaybackService.CurrentSong != null)
        {
            var playbackState = PlaybackService.waveOutDevice?.PlaybackState;
            string stateStr = playbackState == NAudio.Wave.PlaybackState.Paused ? "Paused" : "Stopped";
            status = $"{stateStr}: {PlaybackService.CurrentSong.Title} - {PlaybackService.CurrentSong.Artist}";
        }
        else
        {
            status = $"Sonorize - {Songs.Count} songs loaded. Ready.";
        }

        if (PlaybackService.CurrentSong?.ActiveLoop != null)
        {
            status += $" (Loop: {PlaybackService.CurrentSong.ActiveLoop.Name})";
        }
        StatusBarText = status;
    }

    private async Task LoadMusicLibrary()
    {
        if (IsLoadingLibrary) return;
        IsLoadingLibrary = true;
        var settings = _settingsService.LoadSettings();
        await Dispatcher.UIThread.InvokeAsync(() => { Songs.Clear(); StatusBarText = "Preparing to load music library..."; });

        if (settings.MusicDirectories.Any())
        {
            await Task.Run(async () =>
            {
                try
                {
                    await _musicLibraryService.LoadMusicFromDirectoriesAsync(
                        settings.MusicDirectories,
                        song => Songs.Add(song),
                        status => StatusBarText = status
                    );
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MainVM] Error in LoadMusicFromDirectoriesAsync: {ex}");
                    await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "Error loading library.");
                }
            });
            await Dispatcher.UIThread.InvokeAsync(() => {
                StatusBarText = $"{Songs.Count} songs loaded. Ready.";
                if (!Songs.Any() && settings.MusicDirectories.Any())
                    StatusBarText = "No songs found. Add directories via File > Settings.";
            });
        }
        else
        {
            await Dispatcher.UIThread.InvokeAsync(() => StatusBarText = "No music directories. Add via File > Settings.");
        }
        IsLoadingLibrary = false;
        UpdateStatusBarText();
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner) return;
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };
        await settingsDialog.ShowDialog(owner);
        if (settingsVM.SettingsChanged) await LoadMusicLibrary();
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner) return;
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