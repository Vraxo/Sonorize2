using Sonorize.Models;
using Sonorize.Services;
using System;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading; // Required for Dispatcher

namespace Sonorize.ViewModels;

public class LoopEditorViewModel : ViewModelBase
{
    private readonly PlaybackService _playbackService;
    private readonly LoopDataService _loopDataService;
    private Song? _currentSongInternal; // Holds the Song instance from PlaybackService.CurrentSong

    public TimeSpan? NewLoopStartCandidate
    {
        get;
        set { SetProperty(ref field, value); OnPropertyChanged(nameof(CanSaveLoopRegion)); OnPropertyChanged(nameof(NewLoopStartCandidateDisplay)); }
    }

    public TimeSpan? NewLoopEndCandidate
    {
        get;
        set { SetProperty(ref field, value); OnPropertyChanged(nameof(CanSaveLoopRegion)); OnPropertyChanged(nameof(NewLoopEndCandidateDisplay)); }
    }

    public string NewLoopStartCandidateDisplay => NewLoopStartCandidate.HasValue ? $"{NewLoopStartCandidate.Value:mm\\:ss\\.ff}" : "Not set";
    public string NewLoopEndCandidateDisplay => NewLoopEndCandidate.HasValue ? $"{NewLoopEndCandidate.Value:mm\\:ss\\.ff}" : "Not set";

    private string _activeLoopDisplayText = "No loop defined.";
    public string ActiveLoopDisplayText { get => _activeLoopDisplayText; set => SetProperty(ref _activeLoopDisplayText, value); }

    private bool _isCurrentLoopActiveUiBinding;
    public bool IsCurrentLoopActiveUiBinding
    {
        get => _isCurrentLoopActiveUiBinding;
        set
        {
            // Only set if different to avoid unnecessary property changed events and potential recursion
            if (SetProperty(ref _isCurrentLoopActiveUiBinding, value))
            {
                if (_currentSongInternal != null && _currentSongInternal.SavedLoop != null)
                {
                    // Update the underlying model property. The model's PropertyChanged handler
                    // will trigger persistence logic in the main VM or here if needed.
                    _currentSongInternal.IsLoopActive = value;
                    Debug.WriteLine($"[LoopEdVM] UI binding set IsLoopActive on Song '{_currentSongInternal.Title}' to: {value}");
                }
                else if (_currentSongInternal != null && _currentSongInternal.SavedLoop == null && value == true)
                {
                    // Prevent activating loop if none is defined, revert UI state
                    _isCurrentLoopActiveUiBinding = false;
                    OnPropertyChanged(nameof(IsCurrentLoopActiveUiBinding)); // Notify UI to revert
                    Debug.WriteLine($"[LoopEdVM] Attempted to activate loop via UI, but no loop is defined for {_currentSongInternal.Title}.");
                }
                // If _currentSongInternal is null, setting _isCurrentLoopActiveUiBinding to false is handled by UpdateStateForCurrentSong(null)
            }
        }
    }


    public bool CanSaveLoopRegion => _currentSongInternal != null
                                     && NewLoopStartCandidate.HasValue
                                     && NewLoopEndCandidate.HasValue
                                     && NewLoopEndCandidate.Value > NewLoopStartCandidate.Value
                                     && _currentSongInternal.Duration.TotalSeconds > 0 // Need duration for comparison
                                     && NewLoopEndCandidate.Value <= _currentSongInternal.Duration
                                     && NewLoopStartCandidate.Value >= TimeSpan.Zero;

    public ICommand CaptureLoopStartCandidateCommand { get; }
    public ICommand CaptureLoopEndCandidateCommand { get; }
    public ICommand SaveLoopCommand { get; }
    public ICommand ClearLoopCommand { get; }
    public ICommand ToggleLoopActiveCommand { get; }
    public ICommand WaveformSeekCommand { get; }


    public LoopEditorViewModel(PlaybackService playbackService, LoopDataService loopDataService)
    {
        _playbackService = playbackService;
        _loopDataService = loopDataService;

        CaptureLoopStartCandidateCommand = new RelayCommand(
            _ => NewLoopStartCandidate = _playbackService.CurrentPosition,
            _ => _currentSongInternal != null && _playbackService.CurrentPlaybackStatus != PlaybackStateStatus.Stopped);

        CaptureLoopEndCandidateCommand = new RelayCommand(
            _ => NewLoopEndCandidate = _playbackService.CurrentPosition,
            _ => _currentSongInternal != null && _playbackService.CurrentPlaybackStatus != PlaybackStateStatus.Stopped);

        SaveLoopCommand = new RelayCommand(SaveLoopAction, _ => CanSaveLoopRegion);

        ClearLoopCommand = new RelayCommand(ClearSavedLoopAction, _ => _currentSongInternal?.SavedLoop != null);

        ToggleLoopActiveCommand = new RelayCommand(ToggleCurrentSongLoopActive, _ => _currentSongInternal?.SavedLoop != null);

        // WaveformSeekCommand is needed here as it interacts directly with the playback position based on a UI event
        WaveformSeekCommand = new RelayCommand(
            timeSpanObj => { if (timeSpanObj is TimeSpan ts && _currentSongInternal != null) _playbackService.Seek(ts); },
            _ => _currentSongInternal != null);


        // Listen to PlaybackService property changes relevant to loop editing state
        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;

        // Initial state update
        UpdateStateForCurrentSong(_playbackService.CurrentSong);
    }

    private void PlaybackService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    Debug.WriteLine($"[LoopEdVM] PlaybackService.CurrentSong changed to: {_playbackService.CurrentSong?.Title ?? "null"}. Updating loop state.");
                    // Remove handler from old song
                    if (_currentSongInternal != null)
                    {
                        _currentSongInternal.PropertyChanged -= CurrentSong_PropertyChanged;
                    }
                    // Update internal song reference
                    _currentSongInternal = _playbackService.CurrentSong;
                    // Add handler to new song if not null
                    if (_currentSongInternal != null)
                    {
                        _currentSongInternal.PropertyChanged += CurrentSong_PropertyChanged;
                    }
                    UpdateStateForCurrentSong(_currentSongInternal);
                    break;
                case nameof(PlaybackService.CurrentPosition):
                case nameof(PlaybackService.CurrentSongDuration):
                    // These affect CanSaveLoopRegion and command CanExecute states
                    OnPropertyChanged(nameof(CanSaveLoopRegion));
                    RaiseLoopCommandCanExecuteChanged();
                    // Also need to update loop display text if CurrentSong or its Loop property isn't triggering it
                    UpdateActiveLoopDisplayText(); // Ensure text reflects active loop status
                    break;
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    // Affects Capture command CanExecute
                    RaiseLoopCommandCanExecuteChanged();
                    break;
            }
        });
    }

    private void CurrentSong_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Listen for changes on the *Song model itself* relevant to loop state
            if (sender is Song song && song == _currentSongInternal)
            {
                switch (e.PropertyName)
                {
                    case nameof(Song.SavedLoop):
                        Debug.WriteLine($"[LoopEdVM] CurrentSong.SavedLoop changed for {song.Title}. Updating loop state.");
                        UpdateStateForCurrentSong(song); // Fully refresh state based on the new loop
                        break;
                    case nameof(Song.IsLoopActive):
                        Debug.WriteLine($"[LoopEdVM] CurrentSong.IsLoopActive changed to {song.IsLoopActive} for {song.Title}. Updating UI binding and persisting.");
                        // Update the UI binding property if it's out of sync
                        if (_isCurrentLoopActiveUiBinding != song.IsLoopActive)
                        {
                            _isCurrentLoopActiveUiBinding = song.IsLoopActive;
                            OnPropertyChanged(nameof(IsCurrentLoopActiveUiBinding));
                        }
                        // Persist the active state change
                        if (song.SavedLoop != null)
                        {
                            _loopDataService.UpdateLoopActiveState(song.FilePath, song.IsLoopActive);
                        }
                        UpdateActiveLoopDisplayText(); // Update the display text
                        break;
                        // Add other relevant song properties if they affect loop state UI
                }
            }
        });
    }

    private void UpdateStateForCurrentSong(Song? song)
    {
        // This method syncs the LoopEditorViewModel's state with the current song's state
        Debug.WriteLine($"[LoopEdVM] UpdateStateForCurrentSong called for: {song?.Title ?? "null"}");
        if (song?.SavedLoop != null)
        {
            NewLoopStartCandidate = song.SavedLoop.Start;
            NewLoopEndCandidate = song.SavedLoop.End;
            IsCurrentLoopActiveUiBinding = song.IsLoopActive; // Sync UI binding
        }
        else
        {
            NewLoopStartCandidate = null;
            NewLoopEndCandidate = null;
            IsCurrentLoopActiveUiBinding = false; // Ensure binding is false when no loop
        }
        UpdateActiveLoopDisplayText();
        OnPropertyChanged(nameof(CanSaveLoopRegion));
        RaiseLoopCommandCanExecuteChanged();
    }

    private void ClearLoopCandidateInputs()
    {
        NewLoopStartCandidate = null;
        NewLoopEndCandidate = null;
        Debug.WriteLine("[LoopEdVM] Loop candidates cleared.");
    }

    private void SaveLoopAction(object? param)
    {
        var currentSong = _currentSongInternal;
        if (!CanSaveLoopRegion || currentSong == null || !NewLoopStartCandidate.HasValue || !NewLoopEndCandidate.HasValue)
        {
            Debug.WriteLine("[LoopEdVM] SaveLoopAction skipped: conditions not met.");
            return;
        }

        var newLoop = new LoopRegion(NewLoopStartCandidate.Value, NewLoopEndCandidate.Value, "User Loop");

        // Determine the desired active state for the new loop:
        // - If there was a loop before and it was active, the new one should also be active.
        // - If there was no loop before, setting a new one implies it should be active.
        // - If there was a loop before and it was *in*active, keep the new one inactive initially.
        bool shouldBeActive = (currentSong.SavedLoop != null && currentSong.IsLoopActive) || currentSong.SavedLoop == null;

        currentSong.SavedLoop = newLoop; // This might trigger Song's PropertyChanged -> LoopEditorViewModel.CurrentSong_PropertyChanged -> UpdateStateForCurrentSong

        // Set IsLoopActive *after* setting SavedLoop to potentially trigger the right logic flow
        // If the desired state is different from the current Song.IsLoopActive, set it.
        // If it's the same, explicitly trigger the persistence call as the Song.IsLoopActive setter might not fire PropertyChanged
        if (currentSong.IsLoopActive != shouldBeActive)
        {
            currentSong.IsLoopActive = shouldBeActive; // This should trigger persistence via CurrentSong_PropertyChanged
        }
        else
        {
            // State is the same, manually trigger persistence
            _loopDataService.SetLoop(currentSong.FilePath, newLoop.Start, newLoop.End, currentSong.IsLoopActive);
        }

        Debug.WriteLine($"[LoopEdVM] Loop saved for {currentSong.Title}. Start: {newLoop.Start}, End: {newLoop.End}, Active: {currentSong.IsLoopActive}");

        // Update UI state might be redundant if triggered by CurrentSong_PropertyChanged, but safe
        UpdateStateForCurrentSong(currentSong);
    }


    private void ClearSavedLoopAction(object? param)
    {
        var currentSong = _currentSongInternal;
        if (currentSong != null)
        {
            var filePath = currentSong.FilePath;
            Debug.WriteLine($"[LoopEdVM] Clearing loop for {currentSong.Title}.");
            currentSong.SavedLoop = null; // This might trigger Song's PropertyChanged
            currentSong.IsLoopActive = false; // This might trigger Song's PropertyChanged
            if (!string.IsNullOrEmpty(filePath))
            {
                _loopDataService.ClearLoop(filePath);
            }
        }
        ClearLoopCandidateInputs();
        UpdateStateForCurrentSong(currentSong); // Ensure UI syncs after clearing
    }

    private void ToggleCurrentSongLoopActive(object? parameter)
    {
        if (_currentSongInternal != null && _currentSongInternal.SavedLoop != null)
        {
            Debug.WriteLine($"[LoopEdVM] Toggling loop active state for {_currentSongInternal.Title}. Current: {_currentSongInternal.IsLoopActive}");
            // Toggling IsCurrentLoopActiveUiBinding will flow back through its setter
            IsCurrentLoopActiveUiBinding = !_isCurrentLoopActiveUiBinding;
        }
    }

    private void UpdateActiveLoopDisplayText()
    {
        var currentSong = _currentSongInternal;
        if (currentSong?.SavedLoop != null)
        {
            var loop = currentSong.SavedLoop;
            string activeStatus = currentSong.IsLoopActive ? " (Active)" : " (Inactive)";
            ActiveLoopDisplayText = $"Loop: {loop.Start:mm\\:ss\\.f} - {loop.End:mm\\:ss\\.f}{activeStatus}";
        }
        else
        {
            ActiveLoopDisplayText = "No loop defined.";
        }
    }

    public void RaiseLoopCommandCanExecuteChanged()
    {
        (CaptureLoopStartCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CaptureLoopEndCandidateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleLoopActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (WaveformSeekCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}