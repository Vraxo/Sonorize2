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
        _playbackViewModel.PropertyChanged += PlaybackViewModel_PropertyChanged; // Handles CurrentSong, HasCurrentSong from PlaybackVM
        if (_playbackViewModel.Controls != null)
        {
            _playbackViewModel.Controls.PropertyChanged += PlaybackControls_PropertyChanged; // Handles specific control properties
        }
        if (_playbackViewModel.ModeControls != null)
        {
            _playbackViewModel.ModeControls.PropertyChanged += PlaybackModeControls_PropertyChanged;
        }
        if (_playbackViewModel.WaveformDisplay != null)
        {
            _playbackViewModel.WaveformDisplay.PropertyChanged += PlaybackWaveformDisplay_PropertyChanged;
        }
        _advancedPanelViewModel.PropertyChanged += AdvancedPanel_PropertyChanged;
    }

    private void UnsubscribeFromEvents()
    {
        _libraryViewModel.PropertyChanged -= Library_PropertyChanged;
        _playbackViewModel.PropertyChanged -= PlaybackViewModel_PropertyChanged;
        if (_playbackViewModel.Controls != null)
        {
            _playbackViewModel.Controls.PropertyChanged -= PlaybackControls_PropertyChanged;
        }
        if (_playbackViewModel.ModeControls != null)
        {
            _playbackViewModel.ModeControls.PropertyChanged -= PlaybackModeControls_PropertyChanged;
        }
        if (_playbackViewModel.WaveformDisplay != null)
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

    // Handles PropertyChanged from PlaybackViewModel (e.g., CurrentSong, HasCurrentSong)
    private void PlaybackViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlaybackViewModel.CurrentSong):
                // MainWindowViewModel.Playback.CurrentSong will be updated because PlaybackViewModel.CurrentSong changed.
                _notifyMainWindowVMPropertyChangedCallback("Playback.CurrentSong");
                _notifyMainWindowVMPropertyChangedCallback("Playback.HasCurrentSong");
                // Properties on Playback.Controls might also change as a result of CurrentSong changing,
                // so we also notify for them as they are likely bound in UI.
                // PlaybackControls_PropertyChanged will handle granular updates if it receives direct events.
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.CurrentTimeDisplay");
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.TotalTimeDisplay");
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.CurrentPlaybackStatus");
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.IsPlaying");
                _raiseAllCommandsCanExecuteChangedCallback();
                _updateStatusBarTextCallback();
                break;
            case nameof(PlaybackViewModel.HasCurrentSong):
                _notifyMainWindowVMPropertyChangedCallback("Playback.HasCurrentSong");
                _raiseAllCommandsCanExecuteChangedCallback();
                _updateStatusBarTextCallback();
                break;
        }
    }

    // Handles PropertyChanged from PlaybackViewModel.Controls (PlaybackControlViewModel)
    private void PlaybackControls_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlaybackControlViewModel.CurrentPlaybackStatus):
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.CurrentPlaybackStatus");
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.IsPlaying"); // IsPlaying depends on CurrentPlaybackStatus
                _updateStatusBarTextCallback();
                _raiseAllCommandsCanExecuteChangedCallback();
                break;
            case nameof(PlaybackControlViewModel.IsPlaying):
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.IsPlaying");
                _updateStatusBarTextCallback();
                _raiseAllCommandsCanExecuteChangedCallback();
                break;
            case nameof(PlaybackControlViewModel.CurrentPosition):
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.CurrentPosition");
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.CurrentPositionSeconds");
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.CurrentTimeDisplay");
                break;
            case nameof(PlaybackControlViewModel.CurrentSongDuration):
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.CurrentSongDuration");
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.CurrentSongDurationSeconds");
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.TotalTimeDisplay");
                _raiseAllCommandsCanExecuteChangedCallback();
                break;
            case nameof(PlaybackControlViewModel.CurrentTimeDisplay):
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.CurrentTimeDisplay");
                break;
            case nameof(PlaybackControlViewModel.TotalTimeDisplay):
                _notifyMainWindowVMPropertyChangedCallback("Playback.Controls.TotalTimeDisplay");
                break;
                // CurrentSong change is handled by PlaybackViewModel_PropertyChanged, which then can trigger notifications for control properties too.
        }
    }


    private void PlaybackModeControls_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlaybackModeViewModel.ShuffleEnabled):
            case nameof(PlaybackModeViewModel.RepeatMode):
                _playbackViewModel.RaisePlaybackCommandCanExecuteChanged();
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