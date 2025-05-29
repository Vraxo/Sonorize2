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
    private readonly SongLoopService _songLoopService; // New service
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
            // Setter now calls SongLoopService to update model and persist
            if (SetProperty(ref _isCurrentLoopActiveUiBinding, value))
            {
                if (_currentSongInternal != null)
                {
                    _songLoopService.SetLoopActiveState(_currentSongInternal, value);
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


    public LoopEditorViewModel(PlaybackService playbackService, LoopDataService loopDataService, SongLoopService songLoopService) // Added SongLoopService
    {
        _playbackService = playbackService;
        _songLoopService = songLoopService; // Store new service

        CandidateLoop = new LoopCandidateViewModel(playbackService, () => _currentSongInternal);
        CandidateLoop.ParentLoopEditor = this;

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
                    CandidateLoop.RaiseCaptureCommandsCanExecuteChanged();
                    UpdateActiveLoopDisplayText();
                    break;
                case nameof(PlaybackService.CurrentPlaybackStatus):
                    CandidateLoop.RaiseCaptureCommandsCanExecuteChanged();
                    break;
            }
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
                        Debug.WriteLine($"[LoopEdVM] CurrentSong.SavedLoop changed for {song.Title}. Updating UI state.");
                        UpdateStateForCurrentSong(song); // Reflects model change to UI
                        break;
                    case nameof(Song.IsLoopActive):
                        Debug.WriteLine($"[LoopEdVM] CurrentSong.IsLoopActive changed to {song.IsLoopActive} for {song.Title}. Updating UI binding.");
                        // Update UI binding if it's out of sync with model (e.g., model changed externally)
                        if (_isCurrentLoopActiveUiBinding != song.IsLoopActive)
                        {
                            _isCurrentLoopActiveUiBinding = song.IsLoopActive;
                            OnPropertyChanged(nameof(IsCurrentLoopActiveUiBinding));
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
            // Sync IsCurrentLoopActiveUiBinding with the song's actual IsLoopActive state
            if (_isCurrentLoopActiveUiBinding != song.IsLoopActive)
            {
                _isCurrentLoopActiveUiBinding = song.IsLoopActive;
                OnPropertyChanged(nameof(IsCurrentLoopActiveUiBinding));
            }
        }
        else
        {
            CandidateLoop.ClearCandidates();
            if (_isCurrentLoopActiveUiBinding != false) // Ensure UI binding is false if no loop
            {
                _isCurrentLoopActiveUiBinding = false;
                OnPropertyChanged(nameof(IsCurrentLoopActiveUiBinding));
            }
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

        TimeSpan start = CandidateLoop.NewLoopStartCandidate.Value;
        TimeSpan end = CandidateLoop.NewLoopEndCandidate.Value;

        // Determine if the loop should be active after saving.
        // It should be active if a loop was already active, or if no loop existed before.
        bool shouldBeActive = (currentSong.SavedLoop != null && currentSong.IsLoopActive) || currentSong.SavedLoop == null;

        _songLoopService.SaveLoop(currentSong, start, end, shouldBeActive);
        // UpdateStateForCurrentSong will be called via PropertyChanged events from Song model
    }

    private void ClearSavedLoopAction(object? param)
    {
        var currentSong = _currentSongInternal;
        if (currentSong != null)
        {
            _songLoopService.ClearLoop(currentSong);
        }
        // CandidateLoop.ClearCandidates(); // SongLoopService.ClearLoop modifies song, which triggers UpdateStateForCurrentSong, which calls ClearCandidates.
        // UpdateStateForCurrentSong will be called via PropertyChanged events from Song model
    }

    private void ToggleCurrentSongLoopActive(object? parameter)
    {
        if (_currentSongInternal?.SavedLoop != null)
        {
            Debug.WriteLine($"[LoopEdVM] Toggling loop active state for {_currentSongInternal.Title} via command. Current UI binding: {_isCurrentLoopActiveUiBinding}");
            // This will trigger the IsCurrentLoopActiveUiBinding setter, which calls the service.
            IsCurrentLoopActiveUiBinding = !IsCurrentLoopActiveUiBinding;
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
        CandidateLoop.Dispose();
        CandidateLoop.ParentLoopEditor = null;
    }
}