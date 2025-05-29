using System;
using System.ComponentModel;
using Sonorize.Services; // For PlaybackService, PlaybackFlowManagerService
using Sonorize.ViewModels.Status; // Required if it directly used StatusBarTextProvider, but it will call MWVM's method

namespace Sonorize.ViewModels;

public class MainWindowViewModelCoordinator : IDisposable
{
    private readonly MainWindowViewModel _ownerViewModel;
    private readonly LibraryViewModel _libraryViewModel;
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly WaveformDisplayViewModel _waveformDisplayViewModel;
    private readonly AdvancedPanelViewModel _advancedPanelViewModel;
    private readonly PlaybackService _playbackService;
    private readonly PlaybackFlowManagerService _playbackFlowManagerService;

    public MainWindowViewModelCoordinator(
        MainWindowViewModel ownerViewModel,
        LibraryViewModel libraryViewModel,
        PlaybackViewModel playbackViewModel,
        AdvancedPanelViewModel advancedPanelViewModel,
        PlaybackService playbackService,
        PlaybackFlowManagerService playbackFlowManagerService)
    {
        _ownerViewModel = ownerViewModel ?? throw new ArgumentNullException(nameof(ownerViewModel));
        _libraryViewModel = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));
        _playbackViewModel = playbackViewModel ?? throw new ArgumentNullException(nameof(playbackViewModel));
        _waveformDisplayViewModel = playbackViewModel.WaveformDisplay ?? throw new ArgumentNullException(nameof(playbackViewModel.WaveformDisplay));
        _advancedPanelViewModel = advancedPanelViewModel ?? throw new ArgumentNullException(nameof(advancedPanelViewModel));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _playbackFlowManagerService = playbackFlowManagerService ?? throw new ArgumentNullException(nameof(playbackFlowManagerService));

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _libraryViewModel.PropertyChanged += Library_PropertyChanged;
        _playbackViewModel.PropertyChanged += Playback_PropertyChanged;
        _waveformDisplayViewModel.PropertyChanged += PlaybackWaveformDisplay_PropertyChanged;
        _advancedPanelViewModel.PropertyChanged += AdvancedPanel_PropertyChanged;
        _playbackService.PlaybackEndedNaturally += PlaybackService_PlaybackEndedNaturally;
    }

    private void UnsubscribeFromEvents()
    {
        _libraryViewModel.PropertyChanged -= Library_PropertyChanged;
        _playbackViewModel.PropertyChanged -= Playback_PropertyChanged;
        _waveformDisplayViewModel.PropertyChanged -= PlaybackWaveformDisplay_PropertyChanged;
        _advancedPanelViewModel.PropertyChanged -= AdvancedPanel_PropertyChanged;
        _playbackService.PlaybackEndedNaturally -= PlaybackService_PlaybackEndedNaturally;
    }

    private void PlaybackService_PlaybackEndedNaturally(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[MWVMCoordinator] PlaybackService_PlaybackEndedNaturally event received.");
        _playbackFlowManagerService.HandlePlaybackEndedNaturally();
        System.Diagnostics.Debug.WriteLine("[MWVMCoordinator] PlaybackService_PlaybackEndedNaturally handler completed after delegation.");
    }

    private void Library_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LibraryViewModel.SelectedSong):
                _ownerViewModel.RaiseAllCommandsCanExecuteChanged();
                break;
            case nameof(LibraryViewModel.IsLoadingLibrary):
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.IsLoadingLibrary));
                _ownerViewModel.RaiseAllCommandsCanExecuteChanged();
                _ownerViewModel.UpdateStatusBarText();
                break;
            case nameof(LibraryViewModel.LibraryStatusText):
                _ownerViewModel.UpdateStatusBarText();
                break;
        }
    }

    private void Playback_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlaybackViewModel.CurrentSong):
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.CurrentSong)); // For direct bindings to MWVM.Playback.CurrentSong
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.HasCurrentSong));
                _ownerViewModel.RaiseAllCommandsCanExecuteChanged();
                _ownerViewModel.UpdateStatusBarText();
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.CurrentTimeDisplay));
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.TotalTimeDisplay));
                break;
            case nameof(PlaybackViewModel.CurrentPlaybackStatus):
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.CurrentPlaybackStatus));
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.IsPlaying));
                _ownerViewModel.UpdateStatusBarText();
                _ownerViewModel.RaiseAllCommandsCanExecuteChanged();
                break;
            case nameof(PlaybackViewModel.CurrentPosition):
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.CurrentPosition));
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.CurrentPositionSeconds));
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.CurrentTimeDisplay));
                break;
            case nameof(PlaybackViewModel.CurrentSongDuration):
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.CurrentSongDuration));
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.CurrentSongDurationSeconds));
                _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.Playback.TotalTimeDisplay));
                _ownerViewModel.RaiseAllCommandsCanExecuteChanged();
                break;
            case nameof(PlaybackViewModel.ShuffleEnabled):
            case nameof(PlaybackViewModel.RepeatMode):
                _playbackViewModel.RaisePlaybackCommandCanExecuteChanged();
                _ownerViewModel.UpdateStatusBarText();
                break;
        }
    }

    private void PlaybackWaveformDisplay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WaveformDisplayViewModel.IsWaveformLoading))
        {
            _ownerViewModel.RaiseAllCommandsCanExecuteChanged();
        }
    }

    private void AdvancedPanel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdvancedPanelViewModel.IsVisible))
        {
            _ownerViewModel.OnPropertyChanged(nameof(MainWindowViewModel.IsAdvancedPanelVisible));
            _ownerViewModel.RaiseAllCommandsCanExecuteChanged();
        }
    }

    public void Dispose()
    {
        UnsubscribeFromEvents();
        GC.SuppressFinalize(this);
    }

    ~MainWindowViewModelCoordinator()
    {
        Dispose();
    }
}