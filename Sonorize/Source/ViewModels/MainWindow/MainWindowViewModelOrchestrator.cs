using System;
using System.ComponentModel;
using Sonorize.ViewModels;

namespace Sonorize.ViewModels;

public class MainWindowViewModelOrchestrator : IDisposable
{
    private readonly LibraryViewModel _libraryViewModel;
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly AdvancedPanelViewModel _advancedPanelViewModel;

    private readonly Action _raiseAllCommandsCanExecuteChangedCallback;
    private readonly Action _updateStatusBarTextCallback;
    private readonly Action<string> _notifyMainWindowVMPropertyChangedCallback;

    public MainWindowViewModelOrchestrator(
        LibraryViewModel libraryViewModel,
        PlaybackViewModel playbackViewModel,
        AdvancedPanelViewModel advancedPanelViewModel,
        Action raiseAllCommandsCanExecuteChangedCallback,
        Action updateStatusBarTextCallback,
        Action<string> notifyMainWindowVMPropertyChangedCallback)
    {
        _libraryViewModel = libraryViewModel ?? throw new ArgumentNullException(nameof(libraryViewModel));
        _playbackViewModel = playbackViewModel ?? throw new ArgumentNullException(nameof(playbackViewModel));
        _advancedPanelViewModel = advancedPanelViewModel ?? throw new ArgumentNullException(nameof(advancedPanelViewModel));
        _raiseAllCommandsCanExecuteChangedCallback = raiseAllCommandsCanExecuteChangedCallback ?? throw new ArgumentNullException(nameof(raiseAllCommandsCanExecuteChangedCallback));
        _updateStatusBarTextCallback = updateStatusBarTextCallback ?? throw new ArgumentNullException(nameof(updateStatusBarTextCallback));
        _notifyMainWindowVMPropertyChangedCallback = notifyMainWindowVMPropertyChangedCallback ?? throw new ArgumentNullException(nameof(notifyMainWindowVMPropertyChangedCallback));

        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _libraryViewModel.PropertyChanged += Library_PropertyChanged;
        _playbackViewModel.PropertyChanged += Playback_PropertyChanged;
        if (_playbackViewModel.ModeControls is not null)
        {
            _playbackViewModel.ModeControls.PropertyChanged += PlaybackModeControls_PropertyChanged;
        }
        if (_playbackViewModel.WaveformDisplay is not null)
        {
            _playbackViewModel.WaveformDisplay.PropertyChanged += PlaybackWaveformDisplay_PropertyChanged;
        }
        _advancedPanelViewModel.PropertyChanged += AdvancedPanel_PropertyChanged;
    }

    private void UnsubscribeFromEvents()
    {
        _libraryViewModel.PropertyChanged -= Library_PropertyChanged;
        _playbackViewModel.PropertyChanged -= Playback_PropertyChanged;
        if (_playbackViewModel.ModeControls is not null)
        {
            _playbackViewModel.ModeControls.PropertyChanged -= PlaybackModeControls_PropertyChanged;
        }
        if (_playbackViewModel.WaveformDisplay is not null)
        {
            _playbackViewModel.WaveformDisplay.PropertyChanged -= PlaybackWaveformDisplay_PropertyChanged;
        }
        _advancedPanelViewModel.PropertyChanged -= AdvancedPanel_PropertyChanged;
    }

    private void PlaybackWaveformDisplay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WaveformDisplayViewModel.IsWaveformLoading))
        {
            _raiseAllCommandsCanExecuteChangedCallback();
        }
    }

    private void AdvancedPanel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdvancedPanelViewModel.IsVisible))
        {
            _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.IsAdvancedPanelVisible));
            _raiseAllCommandsCanExecuteChangedCallback();
        }
    }

    private void Library_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LibraryViewModel.SelectedSong):
                _raiseAllCommandsCanExecuteChangedCallback();
                break;
            case nameof(LibraryViewModel.IsLoadingLibrary):
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.IsLoadingLibrary));
                _raiseAllCommandsCanExecuteChangedCallback();
                _updateStatusBarTextCallback();
                break;
            case nameof(LibraryViewModel.LibraryStatusText):
                _updateStatusBarTextCallback();
                break;
        }
    }

    private void Playback_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlaybackViewModel.CurrentSong):
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.CurrentSong)); // Or just rely on MainWindowViewModel.Playback being the source
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.HasCurrentSong));
                _raiseAllCommandsCanExecuteChangedCallback();
                _updateStatusBarTextCallback();
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.CurrentTimeDisplay));
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.TotalTimeDisplay));
                break;
            case nameof(PlaybackViewModel.CurrentPlaybackStatus):
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.CurrentPlaybackStatus));
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.IsPlaying));
                _updateStatusBarTextCallback();
                _raiseAllCommandsCanExecuteChangedCallback();
                break;
            case nameof(PlaybackViewModel.CurrentPosition):
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.CurrentPosition));
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.CurrentPositionSeconds));
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.CurrentTimeDisplay));
                break;
            case nameof(PlaybackViewModel.CurrentSongDuration):
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.CurrentSongDuration));
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.CurrentSongDurationSeconds));
                _notifyMainWindowVMPropertyChangedCallback(nameof(MainWindowViewModel.Playback.TotalTimeDisplay));
                _raiseAllCommandsCanExecuteChangedCallback();
                break;
        }
    }

    private void PlaybackModeControls_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlaybackModeViewModel.ShuffleEnabled):
            case nameof(PlaybackModeViewModel.RepeatMode):
                _playbackViewModel.RaisePlaybackCommandCanExecuteChanged(); // PlaybackVM raises its own commands
                _updateStatusBarTextCallback();
                break;
        }
    }

    public void Dispose()
    {
        UnsubscribeFromEvents();
        GC.SuppressFinalize(this);
    }
}