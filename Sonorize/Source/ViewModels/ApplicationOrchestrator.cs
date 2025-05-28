using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.ViewModels.Status;

namespace Sonorize.ViewModels;

public class ApplicationOrchestrator : IDisposable
{
    private readonly MainWindowViewModel _mainWindowViewModel; // To call back for UI updates
    private readonly SettingsService _settingsService;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly WaveformService _waveformService;
    private readonly LoopDataService _loopDataService;
    private readonly ScrobblingService _scrobblingService;
    private readonly NextTrackSelectorService _nextTrackSelectorService;

    public PlaybackService PlaybackService { get; }
    public ThemeColors CurrentTheme { get; }

    public LibraryViewModel Library { get; }
    public PlaybackViewModel Playback { get; }
    public LoopEditorViewModel LoopEditor { get; }
    public AdvancedPanelViewModel AdvancedPanel { get; }

    // UI Specific Services
    private readonly StatusBarTextProvider _statusBarTextProvider;
    private readonly SettingsChangeProcessorService _settingsChangeProcessorService;
    private readonly PlaybackFlowManagerService _playbackFlowManagerService;
    public ApplicationInteractionService ApplicationInteraction { get; }

    public ApplicationOrchestrator(
        MainWindowViewModel mainWindowViewModel,
        SettingsService settingsService,
        MusicLibraryService musicLibraryService,
        PlaybackService playbackService,
        ThemeColors theme,
        WaveformService waveformService,
        LoopDataService loopDataService,
        ScrobblingService scrobblingService,
        NextTrackSelectorService nextTrackSelectorService)
    {
        _mainWindowViewModel = mainWindowViewModel ?? throw new ArgumentNullException(nameof(mainWindowViewModel));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _musicLibraryService = musicLibraryService ?? throw new ArgumentNullException(nameof(musicLibraryService));
        PlaybackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        CurrentTheme = theme ?? throw new ArgumentNullException(nameof(theme));
        _waveformService = waveformService ?? throw new ArgumentNullException(nameof(waveformService));
        _loopDataService = loopDataService ?? throw new ArgumentNullException(nameof(loopDataService));
        _scrobblingService = scrobblingService ?? throw new ArgumentNullException(nameof(scrobblingService));
        _nextTrackSelectorService = nextTrackSelectorService ?? throw new ArgumentNullException(nameof(nextTrackSelectorService));

        // Create ViewModels
        Library = new LibraryViewModel(_mainWindowViewModel, _settingsService, _musicLibraryService, _loopDataService);
        Playback = new PlaybackViewModel(PlaybackService, _waveformService);
        LoopEditor = new LoopEditorViewModel(PlaybackService, _loopDataService);
        AdvancedPanel = new AdvancedPanelViewModel(Playback, Library);

        // Create UI specific services
        _statusBarTextProvider = new StatusBarTextProvider(Playback, LoopEditor, Library);
        _settingsChangeProcessorService = new SettingsChangeProcessorService(Library, _scrobblingService);
        _playbackFlowManagerService = new PlaybackFlowManagerService(Library, Playback, PlaybackService, _nextTrackSelectorService);
        ApplicationInteraction = new ApplicationInteractionService(_settingsService, _settingsChangeProcessorService, CurrentTheme);

        // Wire up event handlers
        WireEvents();
    }

    private void WireEvents()
    {
        Library.PropertyChanged += Library_PropertyChanged;
        Playback.PropertyChanged += Playback_PropertyChanged;
        AdvancedPanel.PropertyChanged += AdvancedPanel_PropertyChanged;
        PlaybackService.PlaybackEndedNaturally += PlaybackService_PlaybackEndedNaturally;
    }

    private void UnwireEvents()
    {
        Library.PropertyChanged -= Library_PropertyChanged;
        Playback.PropertyChanged -= Playback_PropertyChanged;
        AdvancedPanel.PropertyChanged -= AdvancedPanel_PropertyChanged;
        PlaybackService.PlaybackEndedNaturally -= PlaybackService_PlaybackEndedNaturally;

        Library.Dispose();
        AdvancedPanel.Dispose();
        // PlaybackViewModel and LoopEditorViewModel don't have explicit Dispose currently
    }

    private void PlaybackService_PlaybackEndedNaturally(object? sender, EventArgs e)
    {
        Debug.WriteLine("[AppOrchestrator] PlaybackService_PlaybackEndedNaturally event received.");
        _playbackFlowManagerService.HandlePlaybackEndedNaturally();
        Debug.WriteLine("[AppOrchestrator] PlaybackService_PlaybackEndedNaturally handler completed.");
    }

    private void Library_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(LibraryViewModel.SelectedSong):
                    Debug.WriteLine($"[AppOrchestrator_LibChanged] Library.SelectedSong changed to: {Library.SelectedSong?.Title ?? "null"}. Instance: {Library.SelectedSong?.GetHashCode() ?? 0}");
                    if (Library.SelectedSong != null && PlaybackService.CurrentSong != Library.SelectedSong)
                    {
                        Debug.WriteLine($"[AppOrchestrator_LibChanged] Library.SelectedSong changed to a *different* song ({Library.SelectedSong.Title}) than PlaybackService.CurrentSong ({PlaybackService.CurrentSong?.Title ?? "null"}). Calling PlaybackService.Play().");
                        PlaybackService.Play(Library.SelectedSong);
                    }
                    else if (Library.SelectedSong != null && PlaybackService.CurrentSong == Library.SelectedSong)
                    {
                        Debug.WriteLine($"[AppOrchestrator_LibChanged] Library.SelectedSong changed but is the SAME song instance as PlaybackService.CurrentSong ({Library.SelectedSong.Title}). Assuming RepeatOne handled it or user re-clicked already playing song. No Play call needed here.");
                    }
                    else if (Library.SelectedSong == null)
                    {
                        Debug.WriteLine("[AppOrchestrator_LibChanged] Library.SelectedSong is null. No Play call needed here. PlaybackService.Stop might have been called.");
                    }
                    _mainWindowViewModel.RaiseAllCommandsCanExecuteChanged();
                    break;

                case nameof(LibraryViewModel.IsLoadingLibrary):
                    _mainWindowViewModel.OnPropertyChanged(nameof(MainWindowViewModel.IsLoadingLibrary)); // Notify MWVM proxy property
                    _mainWindowViewModel.RaiseAllCommandsCanExecuteChanged();
                    UpdateStatusBarText();
                    break;

                case nameof(LibraryViewModel.LibraryStatusText):
                    UpdateStatusBarText();
                    break;
            }
        });
    }

    private void Playback_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            _mainWindowViewModel.OnPlaybackPropertyChanged(e.PropertyName); // Forward to MainWindowViewModel

            // Specific logic that was in MainWindowViewModel's Playback_PropertyChanged
            if (e.PropertyName == nameof(PlaybackViewModel.CurrentSong))
            {
                if (!Playback.HasCurrentSong && Library.SelectedSong != null)
                {
                    Debug.WriteLine("[AppOrchestrator_PlaybackChanged] PlaybackService has no current song. Clearing Library selection.");
                    Library.SelectedSong = null;
                }
            }

            if (e.PropertyName is nameof(PlaybackViewModel.CurrentSong) or
                nameof(PlaybackViewModel.CurrentPlaybackStatus) or
                nameof(PlaybackViewModel.ShuffleEnabled) or
                nameof(PlaybackViewModel.RepeatMode))
            {
                UpdateStatusBarText();
            }
        });
    }

    private void AdvancedPanel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdvancedPanelViewModel.IsVisible))
        {
            _mainWindowViewModel.OnPropertyChanged(nameof(MainWindowViewModel.IsAdvancedPanelVisible));
            _mainWindowViewModel.RaiseAllCommandsCanExecuteChanged();
        }
    }

    public void UpdateStatusBarText()
    {
        _mainWindowViewModel.StatusBarText = _statusBarTextProvider.GetCurrentStatusText();
    }

    public void InitializeUIDependentStates()
    {
        _mainWindowViewModel.OnPropertyChanged(nameof(MainWindowViewModel.IsLoadingLibrary));
        _mainWindowViewModel.OnPlaybackPropertyChanged(nameof(PlaybackViewModel.CurrentSong)); // Triggers chain of updates
        _mainWindowViewModel.OnPropertyChanged(nameof(MainWindowViewModel.IsAdvancedPanelVisible));
        _mainWindowViewModel.OnPropertyChanged(nameof(MainWindowViewModel.ActiveTabIndex));
        UpdateStatusBarText();
        _mainWindowViewModel.RaiseAllCommandsCanExecuteChanged();
    }

    public async Task LoadInitialLibraryDataAsync()
    {
        await Library.LoadLibraryAsync();
    }


    public void Dispose()
    {
        UnwireEvents();
        PlaybackService.Dispose();
        _musicLibraryService.SongThumbnailUpdated -= Library.MusicLibraryService_SongThumbnailUpdated; // Assuming Library subscribes
        // Any other disposals for services created here, if they implement IDisposable
    }
}