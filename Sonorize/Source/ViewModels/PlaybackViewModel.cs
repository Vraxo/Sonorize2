using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services; // This using directive makes PlaybackStateStatus from the Service available

namespace Sonorize.ViewModels;

public enum RepeatMode { None, PlayOnce, RepeatOne, RepeatAll }

public class PlaybackViewModel : ViewModelBase, IDisposable
{
    public PlaybackService PlaybackService { get; }

    public PlaybackControlViewModel Controls { get; } // New child VM
    public WaveformDisplayViewModel WaveformDisplay { get; }
    public PlaybackModeViewModel ModeControls { get; }
    public PlaybackEffectsViewModel EffectsControls { get; }

    // Proxied properties for general state, often used by parent or for broader UI logic
    public Song? CurrentSong => PlaybackService.CurrentSong;
    public bool HasCurrentSong => PlaybackService.CurrentSong != null;


    // Properties related to direct playback control are now in PlaybackControlViewModel
    // e.g., CurrentPosition, CurrentPositionSeconds, CurrentSongDuration, CurrentSongDurationSeconds
    // e.g., CurrentPlaybackStatus, IsPlaying
    // e.g., CurrentTimeDisplay, TotalTimeDisplay

    // Commands are now in PlaybackControlViewModel
    // public ICommand PlayPauseResumeCommand { get; }
    // public ICommand SeekCommand { get; }

    public PlaybackViewModel(PlaybackService playbackService, WaveformService waveformService)
    {
        PlaybackService = playbackService;
        WaveformDisplay = new WaveformDisplayViewModel(playbackService, waveformService);
        Controls = new PlaybackControlViewModel(playbackService, WaveformDisplay); // Instantiate new VM
        ModeControls = new PlaybackModeViewModel(this);
        EffectsControls = new PlaybackEffectsViewModel(playbackService);

        PlaybackService.PropertyChanged += PlaybackService_PropertyChanged;
        PropertyChanged += PlaybackViewModel_PropertyChanged;
        WaveformDisplay.PropertyChanged += WaveformDisplay_PropertyChanged;
    }

    private void WaveformDisplay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WaveformDisplayViewModel.IsWaveformLoading))
        {
            return;
        }
        // PlaybackControlViewModel also listens to this, so this call ensures all commands are updated
        RaisePlaybackCommandCanExecuteChanged();
    }

    private void PlaybackService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    OnPropertyChanged(nameof(CurrentSong)); // For data contexts, WaveformDisplay, etc.
                    OnPropertyChanged(nameof(HasCurrentSong)); // For ModeControls, general UI state
                                                               // PlaybackControlViewModel handles its own updates from PlaybackService
                    break;
                    // Other properties like CurrentPosition, CurrentPlaybackStatus are handled
                    // directly by PlaybackControlViewModel for its specific UI elements.
            }
            // It's important to raise command changes if HasCurrentSong changes,
            // as many commands depend on it.
            RaisePlaybackCommandCanExecuteChanged();
        });
    }

    private void PlaybackViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HasCurrentSong):
                ModeControls.RaiseCommandCanExecuteChanged();
                // PlaybackControlViewModel also depends on HasCurrentSong (via PlaybackService)
                // and updates its commands internally.
                // This global RaisePlaybackCommandCanExecuteChanged ensures all relevant commands
                // across PlaybackViewModel and its children are re-evaluated.
                RaisePlaybackCommandCanExecuteChanged();
                break;
        }
    }

    public void RaisePlaybackCommandCanExecuteChanged()
    {
        Controls.RaiseCommandsCanExecuteChanged();
        ModeControls.RaiseCommandCanExecuteChanged();
        // EffectsControls currently has no commands that need dynamic CanExecute updates.
    }

    public void Dispose()
    {
        if (PlaybackService != null)
        {
            PlaybackService.PropertyChanged -= PlaybackService_PropertyChanged;
        }
        PropertyChanged -= PlaybackViewModel_PropertyChanged;

        if (WaveformDisplay != null)
        {
            WaveformDisplay.PropertyChanged -= WaveformDisplay_PropertyChanged;
            // If WaveformDisplay implements IDisposable, call it: WaveformDisplay.Dispose();
        }
        Controls?.Dispose();
        ModeControls?.Dispose();
        // EffectsControls does not currently implement IDisposable or hold resources.
    }
}