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
using Sonorize.Utils;
using System.IO;

namespace Sonorize.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService; // Still need to pass to PlaybackVM
    private readonly LoopDataService _loopDataService; // Still need to pass to LibraryVM and LoopEditorVM

    // Expose the Services directly for child VMs or public properties
    public PlaybackService PlaybackService { get; } // Keep reference to the service
    public ThemeColors CurrentTheme { get; } // Theme is a top-level concern

    // Expose the child ViewModels
    public LibraryViewModel Library { get; set; }
    public LoopEditorViewModel LoopEditor { get; }
    public PlaybackViewModel Playback { get; } // New Playback ViewModel

    // HasCurrentSong property moved to PlaybackViewModel - Access via Playback.HasCurrentSong
    // IsWaveformLoading moved to PlaybackViewModel - Access via Playback.IsWaveformLoading
    // WaveformRenderData moved to PlaybackViewModel - Access via Playback.WaveformRenderData

    private string _statusBarText = "Welcome to Sonorize!";
    public string StatusBarText { get => _statusBarText; set => SetProperty(ref _statusBarText, value); }

    // IsLoadingLibrary is a proxy to Library's state
    public bool IsLoadingLibrary { get => Library.IsLoadingLibrary; }

    private bool _isAdvancedPanelVisible;
    public bool IsAdvancedPanelVisible { get => _isAdvancedPanelVisible; set { if (SetProperty(ref _isAdvancedPanelVisible, value)) OnAdvancedPanelVisibleChanged(); } }

    // Top-level commands
    public ICommand LoadInitialDataCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand AddDirectoryAndRefreshCommand { get; }
    public ICommand ToggleAdvancedPanelCommand { get; }

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
        PlaybackService = playbackService; // Keep service ref for child VMs
        CurrentTheme = theme;
        _waveformService = waveformService; // Keep service ref for child VMs
        _loopDataService = loopDataService; // Keep service ref for child VMs

        // Initialize child ViewModels, passing required dependencies
        Library = new LibraryViewModel(_settingsService, _musicLibraryService, _loopDataService);
        Playback = new PlaybackViewModel(PlaybackService, _waveformService); // Pass PlaybackService and WaveformService
        LoopEditor = new LoopEditorViewModel(PlaybackService, _loopDataService); // Pass PlaybackService and LoopDataService


        // Subscribe to child ViewModel property changes relevant to the parent VM
        Library.PropertyChanged += Library_PropertyChanged;
        Playback.PropertyChanged += Playback_PropertyChanged; // Listen to Playback VM changes

        LoadInitialDataCommand = new RelayCommand(async _ => await Library.LoadLibraryAsync(), _ => !Library.IsLoadingLibrary);
        OpenSettingsCommand = new RelayCommand(async owner => await OpenSettingsDialog(owner), _ => !Library.IsLoadingLibrary);
        ExitCommand = new RelayCommand(_ => Environment.Exit(0));
        AddDirectoryAndRefreshCommand = new RelayCommand(async owner => await AddMusicDirectoryAndRefresh(owner), _ => !Library.IsLoadingLibrary);

        // ToggleAdvancedPanelCommand depends on Playback.HasCurrentSong (derived from PlaybackService)
        ToggleAdvancedPanelCommand = new RelayCommand(
            _ => IsAdvancedPanelVisible = !IsAdvancedPanelVisible,
            _ => Playback.HasCurrentSong && !Library.IsLoadingLibrary);


        // Initial state update
        UpdateAllUIDependentStates();
    }

    private void Library_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(Library.SelectedSong):
                    Debug.WriteLine($"[MainVM_LibChanged] Library.SelectedSong changed to: {Library.SelectedSong?.Title ?? "null"}.");
                    // When a song is selected in the Library, tell the PlaybackService to play it.
                    // PlaybackService_PropertyChanged handler (in this VM and PlaybackVM) will react to this.
                    if (Library.SelectedSong != null)
                    {
                        PlaybackService.Play(Library.SelectedSong);
                    }
                    // Note: If selection is cleared (SelectedSong becomes null), we don't stop playback here.
                    // Playback stops only when the song naturally ends or the user clicks stop.

                    // Update commands that might depend on a song being selected/playing
                    RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(Library.IsLoadingLibrary):
                    // Propagate the loading state change
                    OnPropertyChanged(nameof(IsLoadingLibrary));
                    // Update commands dependent on loading state
                    RaiseAllCommandsCanExecuteChanged();
                    UpdateStatusBarText(); // Status text includes loading info
                    break;
                case nameof(Library.LibraryStatusText):
                    UpdateStatusBarText(); // Library status affects overall status bar
                    break;
                    // Properties like Artists, Albums, FilteredSongs changing are handled by LibraryVM itself.
                    // When FilteredSongs changes, LibraryVM calls RaiseNavigationCommandsCanExecuteChanged internally.
            }
        });
    }

    private void Playback_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Listen for PlaybackViewModel changes that affect MainViewModel's state/UI
            switch (e.PropertyName)
            {
                case nameof(PlaybackViewModel.CurrentSong):
                    OnPropertyChanged(nameof(Playback.CurrentSong)); // Propagate the song itself
                    OnPropertyChanged(nameof(Playback.HasCurrentSong)); // Propagate derived property
                    // Commands that might depend on a song being playing (like ToggleAdvancedPanelCommand)
                    RaiseAllCommandsCanExecuteChanged();

                    // If playback stops entirely (HasCurrentSong becomes false), clear the library selection
                    // This covers song ending or explicit Stop() call.
                    if (!Playback.HasCurrentSong && Library.SelectedSong != null)
                    {
                        Debug.WriteLine("[MainVM_PlaybackChanged] PlaybackService has no current song. Clearing Library selection.");
                        Library.SelectedSong = null; // This will trigger Library_PropertyChanged
                    }

                    // When a new song starts playing (Playback.CurrentSong changes to non-null)
                    // trigger waveform loading if panel is visible.
                    if (Playback.CurrentSong != null && IsAdvancedPanelVisible)
                    {
                        Debug.WriteLine("[MainVM_PlaybackChanged] Playback has current song, advanced panel is visible. Requesting waveform load.");
                        _ = Playback.LoadWaveformForCurrentSongAsync(); // Delegate waveform load
                    }
                    // Note: Clearing waveform when song becomes null is handled inside PlaybackViewModel

                    // Update time displays when song changes (delegated to PlaybackVM)
                    // UpdateStatusBarText(); // Playback status affects overall status bar - handled by CurrentPlaybackStatus change

                    // Commands in PlaybackVM (Play/Pause, Seek, Speed/Pitch) are handled by PlaybackVM itself.
                    // Library navigation commands are handled by LibraryVM's SelectedSong/FilteredSongs change.
                    // ToggleAdvancedPanelCommand depends on Playback.HasCurrentSong, so need to raise.
                    RaiseAllCommandsCanExecuteChanged();

                    break;
                case nameof(PlaybackViewModel.CurrentPlaybackStatus):
                    OnPropertyChanged(nameof(Playback.CurrentPlaybackStatus)); // Propagate status
                    OnPropertyChanged(nameof(Playback.IsPlaying)); // Propagate derived IsPlaying
                    UpdateStatusBarText(); // Playback status affects overall status bar
                    // Commands in PlaybackVM already listen to this. MainVM commands potentially affected? (currently none directly)
                    RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(PlaybackViewModel.CurrentPosition):
                    OnPropertyChanged(nameof(Playback.CurrentPosition)); // Propagate position
                    OnPropertyChanged(nameof(Playback.CurrentPositionSeconds)); // Propagate derived property
                    OnPropertyChanged(nameof(Playback.CurrentTimeDisplay)); // Propagate derived property
                    // Loop editor and UI slider are bound directly to PlaybackVM properties.
                    // Commands in PlaybackVM already listen to this.
                    // Raising all commands here is usually unnecessary unless a MainVM command directly depends on position.
                    // RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(PlaybackViewModel.CurrentSongDuration):
                    OnPropertyChanged(nameof(Playback.CurrentSongDuration)); // Propagate duration
                    OnPropertyChanged(nameof(Playback.CurrentSongDurationSeconds)); // Propagate derived property
                    OnPropertyChanged(nameof(Playback.TotalTimeDisplay)); // Propagate derived property
                    // UI slider is bound directly.
                    // RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(PlaybackViewModel.IsWaveformLoading):
                    OnPropertyChanged(nameof(Playback.IsWaveformLoading)); // Propagate state
                    // Commands in PlaybackVM already listen to this.
                    // Raising all commands here is usually unnecessary unless a MainVM command directly depends on this.
                    // RaiseAllCommandsCanExecuteChanged();
                    break;
                case nameof(PlaybackViewModel.WaveformRenderData):
                    OnPropertyChanged(nameof(Playback.WaveformRenderData)); // Propagate data
                    // Waveform display is bound directly.
                    break;

                    // PlaybackSpeed, PlaybackPitch, derived display properties are handled within PlaybackVM.
                    // LoopEditor VM listens directly to PlaybackService for position/duration.
            }
        });
    }

    private void OnAdvancedPanelVisibleChanged()
    {
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // If Advanced Panel becomes visible, and a song is playing, load its waveform.
        // Access PlaybackVM properties directly
        if (IsAdvancedPanelVisible && Playback.HasCurrentSong && !Playback.WaveformRenderData.Any() && !Playback.IsWaveformLoading)
        {
            Debug.WriteLine("[MainVM] Advanced Panel visible, song is playing, waveform not loaded/loading. Requesting waveform load.");
            _ = Playback.LoadWaveformForCurrentSongAsync(); // Delegate waveform load
        }
        // Note: If it becomes hidden, we don't clear the waveform data automatically here.
        // Clearing upon song change/stop is handled by PlaybackViewModel's PS handler.
    }

    private void UpdateAllUIDependentStates()
    {
        // Update main VM properties that depend on child VM states
        OnPropertyChanged(nameof(IsLoadingLibrary)); // Depends on Library.IsLoadingLibrary
        OnPropertyChanged(nameof(Playback.CurrentSong)); // Ensure Playback's CurrentSong is reflected
        OnPropertyChanged(nameof(Playback.HasCurrentSong)); // Ensure Playback's HasCurrentSong is reflected
        OnPropertyChanged(nameof(IsAdvancedPanelVisible)); // Ensure panel visibility is reflected
        UpdateStatusBarText(); // Depends on Playback and Library status

        // Trigger commands CanExecute updates for all VMs
        RaiseAllCommandsCanExecuteChanged();
    }

    /// <summary>
    /// Raises CanExecuteChanged for commands owned by this ViewModel and tells child VMs to do the same.
    /// </summary>
    public void RaiseAllCommandsCanExecuteChanged()
    {
        // Raise CanExecuteChanged for commands owned by MainWindowViewModel
        (LoadInitialDataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (OpenSettingsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExitCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (AddDirectoryAndRefreshCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleAdvancedPanelCommand as RelayCommand)?.RaiseCanExecuteChanged();

        // Tell child VMs to raise their commands' CanExecuteChanged
        // This now includes the navigation commands within LibraryViewModel
        Library.RaiseLibraryCommandsCanExecuteChanged();
        Library.RaiseNavigationCommandsCanExecuteChanged(); // Explicitly call navigation commands
        Playback.RaisePlaybackCommandCanExecuteChanged();
        LoopEditor.RaiseLoopCommandCanExecuteChanged();
    }


    private void UpdateStatusBarText()
    {
        // Combine status from PlaybackViewModel and LibraryViewModel
        string status;
        if (Playback.HasCurrentSong)
        {
            string stateStr = Playback.CurrentPlaybackStatus switch { PlaybackStateStatus.Playing => "Playing", PlaybackStateStatus.Paused => "Paused", PlaybackStateStatus.Stopped => "Stopped", _ => "Idle" };
            // Access CurrentSong via Playback property
            status = $"{stateStr}: {Playback.CurrentSong?.Title ?? "Unknown Song"}";
            // Use LoopEditor's property for loop active state displayed in status bar
            // Access CurrentSong via Playback property
            if (LoopEditor.IsCurrentLoopActiveUiBinding && Playback.CurrentSong?.SavedLoop != null)
            {
                status += $" (Loop Active)";
            }
        }
        else
        {
            // No playback info, use library status
            status = Library.LibraryStatusText;
        }
        StatusBarText = status;
    }

    private async Task LoadMusicLibrary()
    {
        // Delegate the core loading logic to the LibraryViewModel
        await Library.LoadLibraryAsync();
        // The Library_PropertyChanged handler for IsLoadingLibrary will trigger status updates.
    }

    private async Task OpenSettingsDialog(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false; // Hide advanced panel when opening settings

        var currentSettingsBeforeDialog = _settingsService.LoadSettings();
        var settingsVM = new SettingsViewModel(_settingsService);
        var settingsDialog = new Sonorize.Views.SettingsWindow(CurrentTheme) { DataContext = settingsVM };

        await settingsDialog.ShowDialog(owner); // Show dialog modally

        // After the dialog is closed
        if (settingsVM.SettingsChanged)
        {
            Debug.WriteLine("[MainVM] Settings changed detected after dialog closed.");
            var newSettingsAfterDialog = _settingsService.LoadSettings();
            bool dirsActuallyChanged = !currentSettingsBeforeDialog.MusicDirectories.SequenceEqual(newSettingsAfterDialog.MusicDirectories);
            bool themeActuallyChanged = currentSettingsBeforeDialog.PreferredThemeFileName != newSettingsAfterDialog.PreferredThemeFileName;

            if (dirsActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Music directories changed. Reloading library.");
                await Library.LoadLibraryAsync(); // Delegate library reload
                // Status bar update triggered by Library.IsLoadingLibrary change.
            }

            if (themeActuallyChanged)
            {
                Debug.WriteLine("[MainVM] Theme changed. Restart recommended.");
                // Update status bar to inform user about restart
                StatusBarText = "Theme changed. Please restart Sonorize for the changes to take full effect.";
            }
        }
        else
        {
            Debug.WriteLine("[MainVM] Settings dialog closed, no changes reported by SettingsViewModel.");
            // Ensure status bar reflects current state after dialog if no reload/theme change occurred
            UpdateStatusBarText();
        }
    }

    private async Task AddMusicDirectoryAndRefresh(object? ownerWindow)
    {
        if (ownerWindow is not Window owner || Library.IsLoadingLibrary) return;
        IsAdvancedPanelVisible = false; // Hide advanced panel

        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Select Music Directory", AllowMultiple = false });

        if (result != null && result.Count > 0)
        {
            string? folderPath = null;
            try
            {
                // Using Path.GetFullPath is crucial for consistent path representation
                folderPath = Path.GetFullPath(result[0].Path.LocalPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MainVM] Error getting full path for selected directory: {ex.Message}");
                StatusBarText = "Error getting path for selected directory.";
                return;
            }

            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                var settings = _settingsService.LoadSettings();
                // Use a case-insensitive comparison for existing directories
                if (!settings.MusicDirectories.Any(d => string.Equals(d, folderPath, StringComparison.OrdinalIgnoreCase)))
                {
                    settings.MusicDirectories.Add(folderPath);
                    _settingsService.SaveSettings(settings);
                    Debug.WriteLine($"[MainVM] Added new directory: {folderPath}. Reloading library.");
                    await Library.LoadLibraryAsync(); // Delegate library reload
                    // Status bar update triggered by Library.IsLoadingLibrary change.
                }
                else
                {
                    Debug.WriteLine($"[MainVM] Directory already exists: {folderPath}");
                    StatusBarText = "Directory already in library.";
                }
            }
            else
            {
                Debug.WriteLine($"[MainVM] Selected directory path is invalid or does not exist: {folderPath}");
                StatusBarText = "Invalid directory selected.";
            }
        }
        else
        {
            Debug.WriteLine("[MainVM] Folder picker cancelled or returned no results.");
            // Optionally update status bar if folder picker was cancelled
            UpdateStatusBarText(); // Revert status bar to normal if not loading
        }
    }
}