using Sonorize.Services; // For PlaybackStateStatus

namespace Sonorize.ViewModels.Status;

public class StatusBarTextProvider
{
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly LoopEditorViewModel _loopEditorViewModel;
    private readonly LibraryViewModel _libraryViewModel;

    public StatusBarTextProvider(
        PlaybackViewModel playbackViewModel,
        LoopEditorViewModel loopEditorViewModel,
        LibraryViewModel libraryViewModel)
    {
        _playbackViewModel = playbackViewModel;
        _loopEditorViewModel = loopEditorViewModel;
        _libraryViewModel = libraryViewModel;
    }

    public string GetCurrentStatusText()
    {
        string status;
        if (_playbackViewModel.HasCurrentSong)
        {
            string stateStr = _playbackViewModel.CurrentPlaybackStatus switch
            {
                PlaybackStateStatus.Playing => "Playing",
                PlaybackStateStatus.Paused => "Paused",
                PlaybackStateStatus.Stopped => "Stopped",
                _ => "Idle"
            };
            status = $"{stateStr}: {_playbackViewModel.CurrentSong?.Title ?? "Unknown Song"}";

            // Playback.CurrentSong is the same instance as LoopEditor's internal current song reference
            if (_loopEditorViewModel.IsCurrentLoopActiveUiBinding && _playbackViewModel.CurrentSong?.SavedLoop != null)
            {
                status += $" (Loop Active)";
            }

            string modeStatus = "";
            if (_playbackViewModel.ModeControls.ShuffleEnabled) // Corrected access
            {
                modeStatus += " | Shuffle";
            }
            modeStatus += _playbackViewModel.ModeControls.RepeatMode switch // Corrected access
            {
                RepeatMode.None => " | Do Nothing",
                RepeatMode.PlayOnce => " | Play Once",
                RepeatMode.RepeatOne => " | Repeat Song",
                RepeatMode.RepeatAll => " | Repeat All",
                _ => ""
            };

            if (!string.IsNullOrEmpty(modeStatus))
            {
                status += modeStatus;
            }
        }
        else
        {
            status = _libraryViewModel.LibraryStatusText;
        }
        return status;
    }
}