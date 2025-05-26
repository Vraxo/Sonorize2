using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Sonorize.Services; // Required for PlaybackService if directly interacting, or PlaybackViewModel

namespace Sonorize.ViewModels;

public class AdvancedPanelViewModel : ViewModelBase
{
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly LibraryViewModel _libraryViewModel;

    private bool _isVisible;
    public bool IsVisible
    {
        get => _isVisible;
        set
        {
            if (SetProperty(ref _isVisible, value))
            {
                OnVisibilityChanged();
            }
        }
    }

    public ICommand ToggleVisibilityCommand { get; }

    public AdvancedPanelViewModel(PlaybackViewModel playbackViewModel, LibraryViewModel libraryViewModel)
    {
        _playbackViewModel = playbackViewModel ?? throw new System.ArgumentNullException(nameof(playbackViewModel));
        _libraryViewModel = libraryViewModel ?? throw new System.ArgumentNullException(nameof(libraryViewModel));

        ToggleVisibilityCommand = new RelayCommand(
            _ => IsVisible = !IsVisible,
            CanToggleVisibility
        );

        _playbackViewModel.PropertyChanged += OnDependentViewModelPropertyChanged;
        _libraryViewModel.PropertyChanged += OnDependentViewModelPropertyChanged;
    }

    private bool CanToggleVisibility(object? parameter)
    {
        return _playbackViewModel.HasCurrentSong && !_libraryViewModel.IsLoadingLibrary;
    }

    private void OnVisibilityChanged()
    {
        (ToggleVisibilityCommand as RelayCommand)?.RaiseCanExecuteChanged(); // Visibility itself doesn't affect CanExecute, but good practice
        if (IsVisible && _playbackViewModel.HasCurrentSong && !_playbackViewModel.WaveformRenderData.Any() && !_playbackViewModel.IsWaveformLoading)
        {
            // This logic was previously in MainWindowViewModel.OnAdvancedPanelVisibleChanged
            System.Diagnostics.Debug.WriteLine("[AdvancedPanelVM] Panel visible, song playing, waveform not loaded/loading. Requesting waveform load.");
            _ = _playbackViewModel.LoadWaveformForCurrentSongAsync();
        }
    }

    private void OnDependentViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackViewModel.HasCurrentSong) ||
            e.PropertyName == nameof(LibraryViewModel.IsLoadingLibrary))
        {
            (ToggleVisibilityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    // Consider adding a Dispose method if event subscriptions need to be cleaned up,
    // though for ViewModels often tied to the main window lifetime, it might not be strictly necessary
    // if the main window's closure disposes everything.
    public void Dispose()
    {
        _playbackViewModel.PropertyChanged -= OnDependentViewModelPropertyChanged;
        _libraryViewModel.PropertyChanged -= OnDependentViewModelPropertyChanged;
    }
}