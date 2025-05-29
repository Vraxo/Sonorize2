using System;
using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Threading; // Required for Dispatcher
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels;

public class LoopEditorViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackService _playbackService;
    private readonly LoopDataService _loopDataService;
    private Song? _currentSongInternal; // Holds the Song instance from PlaybackService.CurrentSong

    public LoopCandidateViewModel CandidateLoop { get; }

    private string _activeLoopDisplayText = "No loop defined.";
    public string ActiveLoopDisplayText { get => _activeLoopDisplayText; set => SetProperty(ref _activeLoopDisplayText, value); }

    private bool _isCurrentLoopActiveUiBinding;
    public bool IsCurrentLoopActiveUiBinding
    {
        get => _isCurrentLoopActiveUiBinding;
        set
        {
            if (SetProperty(ref _isCurrentLoopActiveUiBinding, value))
            {
                if (_currentSongInternal != null && _currentSongInternal.SavedLoop != null)
                {
                    _currentSongInternal.IsLoopActive = value;
                    Debug.WriteLine($"[LoopEdVM] UI binding set IsLoopActive on Song '{_currentSongInternal.Title}' to: {value}");
                }
                else if (_currentSongInternal != null && _currentSongInternal.SavedLoop == null && value == true)
                {
                    _isCurrentLoopActiveUiBinding = false;
                    OnPropertyChanged(nameof(IsCurrentLoopActiveUiBinding));
                    Debug.WriteLine($"[LoopEdVM] Attempted to activate loop via UI, but no loop is defined for {_currentSongInternal.Title}.");
                }
            }
        }
    }

    public bool CanSaveLoopRegion => _currentSongInternal != null
                                     && CandidateLoop.NewLoopStartCandidate.HasValue
                                     && CandidateLoop.NewLoopEndCandidate.HasValue
                                     && CandidateLoop.NewLoopEndCandidate.Value > CandidateLoop.NewLoopStartCandidate.Value
                                     && _currentSongInternal.Duration.TotalSeconds > 0
                                     && CandidateLoop.NewLoopEndCandidate.Value <= _currentSongInternal.Duration
                                     && CandidateLoop.NewLoopStartCandidate.Value >= TimeSpan.Zero;

    // Delegate capture commands to LoopCandidateViewModel
    public ICommand CaptureLoopStartCandidateCommand => CandidateLoop.CaptureLoopStartCandidateCommand;
    public ICommand CaptureLoopEndCandidateCommand => CandidateLoop.CaptureLoopEndCandidateCommand;

    public ICommand SaveLoopCommand { get; }
    public ICommand ClearLoopCommand { get; }
    public ICommand ToggleLoopActiveCommand { get; }
    public ICommand WaveformSeekCommand { get; }


    public LoopEditorViewModel(PlaybackService playbackService, LoopDataService loopDataService)
    {
        _playbackService = playbackService;
        _loopDataService = loopDataService;

        CandidateLoop = new LoopCandidateViewModel(playbackService, () => _currentSongInternal);
        CandidateLoop.ParentLoopEditor = this; // Allow child to notify parent

        SaveLoopCommand = new RelayCommand(SaveLoopAction, _ => CanSaveLoopRegion);
        ClearLoopCommand = new RelayCommand(ClearSavedLoopAction, _ => _currentSongInternal?.SavedLoop != null);
        ToggleLoopActiveCommand = new RelayCommand(ToggleCurrentSongLoopActive, _ => _currentSongInternal?.SavedLoop != null);
        WaveformSeekCommand = new RelayCommand(
            timeSpanObj => { if (timeSpanObj is TimeSpan ts && _currentSongInternal != null) _playbackService.Seek(ts); },
            _ => _currentSongInternal != null);

        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;
        UpdateStateForCurrentSong(_playbackService.CurrentSong);
    }

    internal void RaiseCanSaveLoopRegionChanged()
    {
        OnPropertyChanged(nameof(CanSaveLoopRegion));
        (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void PlaybackService_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(PlaybackService.CurrentSong):
                    Debug.WriteLine($"[LoopEdVM] PlaybackService.CurrentSong changed to: {_playbackService.CurrentSong?.Title ?? "null"}. Updating loop state.");
                    if (_currentSongInternal != null)
                    {
                        _currentSongInternal.PropertyChanged -= CurrentSong_PropertyChanged;
                    }
                    _currentSongInternal = _playbackService.CurrentSong;
                    if (_currentSongInternal != null)
                    {
                        _currentSongInternal.PropertyChanged += CurrentSong_PropertyChanged;
                    }
                    UpdateStateForCurrentSong(_currentSongInternal);
                    break;
                case nameof(PlaybackService.CurrentPosition):
                case nameof(PlaybackService.CurrentSongDuration):
                    RaiseCanSaveLoopRegionChanged();
                    CandidateLoop.RaiseCaptureCommandsCanExecuteChanged(); // Capture commands depend on playback status/song
                    UpdateActiveLoopDisplayText();
                    break;
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    CandidateLoop.RaiseCaptureCommandsCanExecuteChanged();
                    break;
            }
            // All commands might be affected by song or playback state changes
            RaiseMainLoopCommandsCanExecuteChanged();
        });
    }

    private void CurrentSong_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (sender is Song song && song == _currentSongInternal)
            {
                switch (e.PropertyName)
                {
                    case nameof(Song.SavedLoop):
                        Debug.WriteLine($"[LoopEdVM] CurrentSong.SavedLoop changed for {song.Title}. Updating loop state.");
                        UpdateStateForCurrentSong(song);
                        break;
                    case nameof(Song.IsLoopActive):
                        Debug.WriteLine($"[LoopEdVM] CurrentSong.IsLoopActive changed to {song.IsLoopActive} for {song.Title}. Updating UI binding and persisting.");
                        if (_isCurrentLoopActiveUiBinding != song.IsLoopActive)
                        {
                            _isCurrentLoopActiveUiBinding = song.IsLoopActive;
                            OnPropertyChanged(nameof(IsCurrentLoopActiveUiBinding));
                        }
                        if (song.SavedLoop != null)
                        {
                            _loopDataService.UpdateLoopActiveState(song.FilePath, song.IsLoopActive);
                        }
                        UpdateActiveLoopDisplayText();
                        break;
                }
            }
        });
    }

    private void UpdateStateForCurrentSong(Song? song)
    {
        Debug.WriteLine($"[LoopEdVM] UpdateStateForCurrentSong called for: {song?.Title ?? "null"}");
        if (song?.SavedLoop != null)
        {
            CandidateLoop.NewLoopStartCandidate = song.SavedLoop.Start;
            CandidateLoop.NewLoopEndCandidate = song.SavedLoop.End;
            IsCurrentLoopActiveUiBinding = song.IsLoopActive;
        }
        else
        {
            CandidateLoop.ClearCandidates();
            IsCurrentLoopActiveUiBinding = false;
        }
        UpdateActiveLoopDisplayText();
        RaiseCanSaveLoopRegionChanged();
        RaiseMainLoopCommandsCanExecuteChanged();
        CandidateLoop.RaiseCaptureCommandsCanExecuteChanged();
    }

    private void SaveLoopAction(object? param)
    {
        var currentSong = _currentSongInternal;
        if (!CanSaveLoopRegion || currentSong == null || !CandidateLoop.NewLoopStartCandidate.HasValue || !CandidateLoop.NewLoopEndCandidate.HasValue)
        {
            Debug.WriteLine("[LoopEdVM] SaveLoopAction skipped: conditions not met.");
            return;
        }

        var newLoop = new LoopRegion(CandidateLoop.NewLoopStartCandidate.Value, CandidateLoop.NewLoopEndCandidate.Value, "User Loop");
        bool shouldBeActive = (currentSong.SavedLoop != null && currentSong.IsLoopActive) || currentSong.SavedLoop == null;

        currentSong.SavedLoop = newLoop;
        if (currentSong.IsLoopActive != shouldBeActive)
        {
            currentSong.IsLoopActive = shouldBeActive;
        }
        else
        {
            _loopDataService.SetLoop(currentSong.FilePath, newLoop.Start, newLoop.End, currentSong.IsLoopActive);
        }
        Debug.WriteLine($"[LoopEdVM] Loop saved for {currentSong.Title}. Start: {newLoop.Start}, End: {newLoop.End}, Active: {currentSong.IsLoopActive}");
        UpdateStateForCurrentSong(currentSong);
    }

    private void ClearSavedLoopAction(object? param)
    {
        var currentSong = _currentSongInternal;
        if (currentSong != null)
        {
            var filePath = currentSong.FilePath;
            Debug.WriteLine($"[LoopEdVM] Clearing loop for {currentSong.Title}.");
            currentSong.SavedLoop = null;
            currentSong.IsLoopActive = false;
            if (!string.IsNullOrEmpty(filePath))
            {
                _loopDataService.ClearLoop(filePath);
            }
        }
        CandidateLoop.ClearCandidates();
        UpdateStateForCurrentSong(currentSong);
    }

    private void ToggleCurrentSongLoopActive(object? parameter)
    {
        if (_currentSongInternal != null && _currentSongInternal.SavedLoop != null)
        {
            Debug.WriteLine($"[LoopEdVM] Toggling loop active state for {_currentSongInternal.Title}. Current: {_currentSongInternal.IsLoopActive}");
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

    public void RaiseMainLoopCommandsCanExecuteChanged()
    {
        (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ToggleLoopActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (WaveformSeekCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (_currentSongInternal != null)
        {
            _currentSongInternal.PropertyChanged -= CurrentSong_PropertyChanged;
        }
        _playbackService.PropertyChanged -= PlaybackService_PropertyChanged;
        CandidateLoop.Dispose(); // Dispose the child ViewModel
        CandidateLoop.ParentLoopEditor = null; // Break cycle if any strict GC needs it
    }
}