using System;
using System.ComponentModel;
using System.Windows.Input;
using Sonorize.Models;
using Sonorize.Services; // For PlaybackService (to get CurrentPosition)

namespace Sonorize.ViewModels;

public class LoopCandidateViewModel : ViewModelBase
{
    private readonly PlaybackService _playbackService;
    private readonly Func<Song?> _getCurrentSongCallback; // To check if a song is loaded

    private TimeSpan? _newLoopStartCandidate;
    public TimeSpan? NewLoopStartCandidate
    {
        get => _newLoopStartCandidate;
        set
        {
            if (SetProperty(ref _newLoopStartCandidate, value))
            {
                OnPropertyChanged(nameof(NewLoopStartCandidateDisplay));
                // Notify parent VM or raise an event if CanSave in parent depends on this directly
                ParentLoopEditor?.RaiseCanSaveLoopRegionChanged();
            }
        }
    }

    private TimeSpan? _newLoopEndCandidate;
    public TimeSpan? NewLoopEndCandidate
    {
        get => _newLoopEndCandidate;
        set
        {
            if (SetProperty(ref _newLoopEndCandidate, value))
            {
                OnPropertyChanged(nameof(NewLoopEndCandidateDisplay));
                ParentLoopEditor?.RaiseCanSaveLoopRegionChanged();
            }
        }
    }

    public string NewLoopStartCandidateDisplay => NewLoopStartCandidate.HasValue ? $"{NewLoopStartCandidate.Value:mm\\:ss\\.ff}" : "Not set";
    public string NewLoopEndCandidateDisplay => NewLoopEndCandidate.HasValue ? $"{NewLoopEndCandidate.Value:mm\\:ss\\.ff}" : "Not set";

    public ICommand CaptureLoopStartCandidateCommand { get; }
    public ICommand CaptureLoopEndCandidateCommand { get; }

    // Reference to parent to notify about changes affecting CanSaveLoopRegion
    // This is a simple way; alternatively, events could be used.
    internal LoopEditorViewModel? ParentLoopEditor { get; set; }


    public LoopCandidateViewModel(PlaybackService playbackService, Func<Song?> getCurrentSongCallback)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _getCurrentSongCallback = getCurrentSongCallback ?? throw new ArgumentNullException(nameof(getCurrentSongCallback));

        CaptureLoopStartCandidateCommand = new RelayCommand(
            _ => NewLoopStartCandidate = _playbackService.CurrentPosition,
            CanCaptureLoopPoint);

        CaptureLoopEndCandidateCommand = new RelayCommand(
            _ => NewLoopEndCandidate = _playbackService.CurrentPosition,
            CanCaptureLoopPoint);

        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;
    }

    private void PlaybackService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackService.CurrentPlaybackStatus) || e.PropertyName == nameof(PlaybackService.CurrentSong))
        {
            RaiseCaptureCommandsCanExecuteChanged();
        }
    }

    private bool CanCaptureLoopPoint(object? _) =>
        _getCurrentSongCallback() is not null &&
        _playbackService.CurrentPlaybackStatus != PlaybackStateStatus.Stopped;

    public void RaiseCaptureCommandsCanExecuteChanged()
    {
        (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void ClearCandidates()
    {
        NewLoopStartCandidate = null;
        NewLoopEndCandidate = null;
    }

    public void Dispose()
    {
        _playbackService.PropertyChanged -= PlaybackService_PropertyChanged;
        ParentLoopEditor = null; // Clear reference
    }
}