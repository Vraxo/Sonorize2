using System;
using System.Diagnostics;
using Sonorize.Services; // Required for PlaybackService

namespace Sonorize.ViewModels;

public class PlaybackEffectsViewModel : ViewModelBase
{
    private readonly PlaybackService _playbackService;

    public double PlaybackSpeed
    {
        get;
        set
        {
            value = Math.Clamp(value, 0.5, 2.0);
            if (SetProperty(ref field, value))
            {
                _playbackService.PlaybackRate = (float)value;
                OnPropertyChanged(nameof(PlaybackSpeedDisplay));
                Debug.WriteLine($"[PlaybackEffectsVM] PlaybackSpeed set to: {value}");
            }
        }
    } = 1.0;

    public string PlaybackSpeedDisplay => $"{PlaybackSpeed:F2}x";

    public double PlaybackPitch
    {
        get;
        set
        {
            // Round to nearest 0.5
            double roundedValue = Math.Round(value * 2, MidpointRounding.AwayFromZero) / 2.0;
            roundedValue = Math.Clamp(roundedValue, -4.0, 4.0); // Clamp after rounding

            if (SetProperty(ref field, roundedValue))
            {
                _playbackService.PitchSemitones = (float)roundedValue;
                OnPropertyChanged(nameof(PlaybackPitchDisplay));
                Debug.WriteLine($"[PlaybackEffectsVM] PlaybackPitch set to: {roundedValue}");
            }
        }
    } = 0.0;
    
    public string PlaybackPitchDisplay => $"{PlaybackPitch:+0.0;-0.0;0} st";

    public PlaybackEffectsViewModel(PlaybackService playbackService)
    {
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        // Initialize with current service values if necessary, or assume service defaults are fine.
        // For now, assume ViewModel defaults (1.0 speed, 0.0 pitch) are fine and will set service on first change.
        // Or, sync from service:
        // _playbackSpeed = _playbackService.PlaybackRate;
        // _playbackPitch = _playbackService.PitchSemitones;
    }

    // If PlaybackService can change these values externally and this VM needs to reflect that,
    // then PlaybackEffectsViewModel would need to subscribe to PlaybackService.PropertyChanged.
    // For now, assuming changes are driven from this VM.
}