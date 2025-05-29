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
    private readonly SongLoopService _songLoopService;
    private Song? _currentSongInternal; // Holds the Song instance from PlaybackService.CurrentSong

    public LoopCandidateViewModel CandidateLoop { get; }
    public ActiveLoopViewModel ActiveLoop { get; } // New child ViewModel

    public bool CanSaveLoopRegion => _currentSongInternal != null
                                     && CandidateLoop.NewLoopStartCandidate.HasValue
                                     && CandidateLoop.NewLoopEndCandidate.HasValue
                                     && CandidateLoop.NewLoopEndCandidate.Value > CandidateLoop.NewLoopStartCandidate.Value
                                     && _currentSongInternal.Duration.TotalSeconds > 0
                                     && CandidateLoop.NewLoopEndCandidate.Value <= _currentSongInternal.Duration
                                     && CandidateLoop.NewLoopStartCandidate.Value >= TimeSpan.Zero;

    public ICommand CaptureLoopStartCandidateCommand => CandidateLoop.CaptureLoopStartCandidateCommand;
    public ICommand CaptureLoopEndCandidateCommand => CandidateLoop.CaptureLoopEndCandidateCommand;

    public ICommand SaveLoopCommand { get; }
    public ICommand ClearLoopCommand { get; }
    public ICommand WaveformSeekCommand { get; }


    public LoopEditorViewModel(PlaybackService playbackService, LoopDataService loopDataService, SongLoopService songLoopService)
    {
        _playbackService = playbackService;
        _songLoopService = songLoopService;

        CandidateLoop = new LoopCandidateViewModel(playbackService, () => _currentSongInternal);
        CandidateLoop.ParentLoopEditor = this; // For CanSaveLoopRegion updates

        ActiveLoop = new ActiveLoopViewModel(playbackService, songLoopService); // Instantiate new child

        SaveLoopCommand = new RelayCommand(SaveLoopAction, _ => CanSaveLoopRegion);
        ClearLoopCommand = new RelayCommand(ClearSavedLoopAction, _ => _currentSongInternal?.SavedLoop != null);
        WaveformSeekCommand = new RelayCommand(
            timeSpanObj => { if (timeSpanObj is TimeSpan ts && _currentSongInternal != null) _playbackService.Seek(ts); },
            _ => _currentSongInternal != null);

        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;
        UpdateStateForCurrentSong(_playbackService.CurrentSong); // Initial state update
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
                    Debug.WriteLine($"[LoopEdVM] PlaybackService.CurrentSong changed to: {_playbackService.CurrentSong?.Title ?? "null"}. Updating loop editor state.");
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
                    // ActiveLoopViewModel handles its own display text update
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
                if (e.PropertyName == nameof(Song.SavedLoop))
                {
                    Debug.WriteLine($"[LoopEdVM] CurrentSong.SavedLoop changed for {song.Title}. Updating loop editor state.");
                    UpdateStateForCurrentSong(song);
                }
                // IsLoopActive changes are handled by ActiveLoopViewModel directly
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
        }
        else
        {
            CandidateLoop.ClearCandidates();
        }
        // ActiveLoopViewModel refreshes its state independently based on PlaybackService.CurrentSong and model changes.
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

        bool shouldBeActive = (currentSong.SavedLoop != null && currentSong.IsLoopActive) || currentSong.SavedLoop == null;

        _songLoopService.SaveLoop(currentSong, start, end, shouldBeActive);
        // ActiveLoopViewModel and LoopEditorViewModel will react to Song model PropertyChanged events
    }

    private void ClearSavedLoopAction(object? param)
    {
        var currentSong = _currentSongInternal;
        if (currentSong != null)
        {
            _songLoopService.ClearLoop(currentSong);
        }
        // ActiveLoopViewModel and LoopEditorViewModel will react to Song model PropertyChanged events
    }

    public void RaiseMainLoopCommandsCanExecuteChanged()
    {
        (SaveLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ClearLoopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (WaveformSeekCommand as RelayCommand)?.RaiseCanExecuteChanged();
        // ToggleLoopActiveCommand is now in ActiveLoopViewModel
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
        ActiveLoop.Dispose();
    }
}