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
        // Listen to IsWaveformLoading on the new WaveformDisplayViewModel
        _playbackViewModel.WaveformDisplay.PropertyChanged += WaveformDisplay_PropertyChanged;
    }

    private void WaveformDisplay_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WaveformDisplayViewModel.IsWaveformLoading))
        {
            // If any command in AdvancedPanelViewModel depends on IsWaveformLoading, update it here.
            // For now, ToggleVisibilityCommand might not directly, but if it did:
            (ToggleVisibilityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    private bool CanToggleVisibility(object? parameter)
    {
        // Visibility can be toggled if a song is playing and library is not loading.
        // Waveform loading state might also influence this if we want to prevent toggling during load.
        return _playbackViewModel.HasCurrentSong &&
               !_libraryViewModel.IsLoadingLibrary &&
               !_playbackViewModel.WaveformDisplay.IsWaveformLoading; // Prevent toggling if waveform is loading
    }

    private void OnVisibilityChanged()
    {
        (ToggleVisibilityCommand as RelayCommand)?.RaiseCanExecuteChanged();

        // Inform the WaveformDisplayViewModel about the panel's visibility
        _playbackViewModel.WaveformDisplay.SetPanelVisibility(IsVisible);
    }

    private void OnDependentViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackViewModel.HasCurrentSong) ||
            e.PropertyName == nameof(LibraryViewModel.IsLoadingLibrary))
        {
            (ToggleVisibilityCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public void Dispose()
    {
        _playbackViewModel.PropertyChanged -= OnDependentViewModelPropertyChanged;
        _libraryViewModel.PropertyChanged -= OnDependentViewModelPropertyChanged;
        if (_playbackViewModel?.WaveformDisplay is not null)
        {
            _playbackViewModel.WaveformDisplay.PropertyChanged -= WaveformDisplay_PropertyChanged;
        }
    }
}