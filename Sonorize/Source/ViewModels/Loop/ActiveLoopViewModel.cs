using System;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels;

public class ActiveLoopViewModel : ViewModelBase, IDisposable
{
    private readonly PlaybackService _playbackService;
    private readonly SongLoopService _songLoopService;
    private Song? _currentSongInternal;

    public string ActiveLoopDisplayText
    {
        get;
        private set => SetProperty(ref field, value);
    } = "No loop defined.";

    private bool _isLoopActive;
    public bool IsLoopActive
    {
        get => _isLoopActive;
        set
        {
            if (!SetProperty(ref _isLoopActive, value) || _currentSongInternal == null)
            {
                return;
            }

            _songLoopService.SetLoopActiveState(_currentSongInternal, value);
        }
    }

    public ICommand ToggleLoopActiveCommand { get; }

    public ActiveLoopViewModel(PlaybackService playbackService, SongLoopService songLoopService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _songLoopService = songLoopService ?? throw new ArgumentNullException(nameof(songLoopService));

        ToggleLoopActiveCommand = new RelayCommand(ExecuteToggleLoopActive, CanExecuteToggleLoopActive);

        _playbackService.PropertyChanged += PlaybackService_PropertyChanged;
        UpdateInternalSongReference(_playbackService.CurrentSong);
    }

    private void PlaybackService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackService.CurrentSong))
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateInternalSongReference(_playbackService.CurrentSong);
            });
        }
    }

    private void UpdateInternalSongReference(Song? newSong)
    {
        if (_currentSongInternal == newSong) return;

        if (_currentSongInternal is not null)
        {
            _currentSongInternal.PropertyChanged -= CurrentSong_PropertyChanged;
        }

        _currentSongInternal = newSong;

        if (_currentSongInternal is not null)
        {
            _currentSongInternal.PropertyChanged += CurrentSong_PropertyChanged;
        }
        RefreshStateFromCurrentSong();
    }

    private void CurrentSong_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is Song song && song == _currentSongInternal)
        {
            if (e.PropertyName == nameof(Song.SavedLoop) || e.PropertyName == nameof(Song.IsLoopActive))
            {
                Dispatcher.UIThread.InvokeAsync(RefreshStateFromCurrentSong);
            }
        }
    }

    private void RefreshStateFromCurrentSong()
    {
        if (_currentSongInternal?.SavedLoop is not null)
        {
            var loop = _currentSongInternal.SavedLoop;
            string activeStatus = _currentSongInternal.IsLoopActive ? " (Active)" : " (Inactive)";
            ActiveLoopDisplayText = $"Loop: {loop.Start:mm\\:ss\\.f} - {loop.End:mm\\:ss\\.f}{activeStatus}";

            if (IsLoopActive != _currentSongInternal.IsLoopActive)
            {
                // Directly set backing field to avoid re-triggering service call if model initiated change
                SetProperty(ref _isLoopActive, _currentSongInternal.IsLoopActive, nameof(IsLoopActive));
            }
        }
        else
        {
            ActiveLoopDisplayText = "No loop defined.";
            if (IsLoopActive) // Ensure UI binding is false if no loop
            {
                SetProperty(ref _isLoopActive, false, nameof(IsLoopActive));
            }
        }
        (ToggleLoopActiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ExecuteToggleLoopActive(object? parameter)
    {
        if ((_currentSongInternal?.SavedLoop) == null)
        {
            return;
        }

        IsLoopActive = !IsLoopActive; // Setter will call SongLoopService
    }

    private bool CanExecuteToggleLoopActive(object? parameter)
    {
        return _currentSongInternal?.SavedLoop is not null;
    }

    public void Dispose()
    {
        _playbackService.PropertyChanged -= PlaybackService_PropertyChanged;
        if (_currentSongInternal is not null)
        {
            _currentSongInternal.PropertyChanged -= CurrentSong_PropertyChanged;
        }
        _currentSongInternal = null;
    }
}