using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input; // Required for ICommand

namespace Sonorize.ViewModels;

public class PlaybackModeViewModel : ViewModelBase
{
    private readonly PlaybackViewModel _parentPlaybackViewModel;

    public bool ShuffleEnabled
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            Debug.WriteLine($"[PlaybackModeVM] ShuffleEnabled set to: {value}");
            // Parent PlaybackViewModel might need to know if preferences are saved here
            // or if this directly influences something in PlaybackService.
            // For now, it's a UI state.
            RaiseCommandCanExecuteChanged();
        }
    } = false;

    public RepeatMode RepeatMode
    {
        get;
        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            Debug.WriteLine($"[PlaybackModeVM] RepeatMode set to: {value}");
            OnPropertyChanged(nameof(IsRepeatOne));
            OnPropertyChanged(nameof(IsRepeatAll));
            OnPropertyChanged(nameof(IsRepeatActive)); // Notify composite state change
            RaiseCommandCanExecuteChanged();
        }
    } = RepeatMode.PlayOnce;

    // Helper properties for UI bindings
    public bool IsRepeatOne { get => RepeatMode == RepeatMode.RepeatOne; set { if (value) RepeatMode = RepeatMode.RepeatOne; else if (RepeatMode == RepeatMode.RepeatOne) RepeatMode = RepeatMode.PlayOnce; } }
    public bool IsRepeatAll { get => RepeatMode == RepeatMode.RepeatAll; set { if (value) RepeatMode = RepeatMode.RepeatAll; else if (RepeatMode == RepeatMode.RepeatAll) RepeatMode = RepeatMode.PlayOnce; } }
    public bool IsRepeatActive => RepeatMode != RepeatMode.None;

    public ICommand ToggleShuffleCommand { get; }
    public ICommand CycleRepeatModeCommand { get; }

    public PlaybackModeViewModel(PlaybackViewModel parentPlaybackViewModel)
    {
        _parentPlaybackViewModel = parentPlaybackViewModel ?? throw new ArgumentNullException(nameof(parentPlaybackViewModel));
        _parentPlaybackViewModel.PropertyChanged += ParentPlaybackViewModel_PropertyChanged;

        ToggleShuffleCommand = new RelayCommand(
            _ => ShuffleEnabled = !ShuffleEnabled,
            _ => _parentPlaybackViewModel.HasCurrentSong
        );

        CycleRepeatModeCommand = new RelayCommand(
            _ => CycleRepeatModeInternal(),
            _ => _parentPlaybackViewModel.HasCurrentSong
        );
    }

    private void ParentPlaybackViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlaybackViewModel.HasCurrentSong))
        {
            return;
        }

        RaiseCommandCanExecuteChanged();
    }

    private void CycleRepeatModeInternal()
    {
        RepeatMode = RepeatMode switch
        {
            RepeatMode.None => RepeatMode.PlayOnce,
            RepeatMode.PlayOnce => RepeatMode.RepeatOne,
            RepeatMode.RepeatOne => RepeatMode.RepeatAll,
            RepeatMode.RepeatAll => RepeatMode.None,
            _ => RepeatMode.None // Should not happen
        };
    }

    public void RaiseCommandCanExecuteChanged()
    {
        (ToggleShuffleCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CycleRepeatModeCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_parentPlaybackViewModel == null)
        {
            return;
        }

        _parentPlaybackViewModel.PropertyChanged -= ParentPlaybackViewModel_PropertyChanged;
    }
}