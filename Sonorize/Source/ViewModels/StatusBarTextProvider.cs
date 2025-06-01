using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels.Status;

public static class StatusBarTextProvider
{
    public static string GetCurrentStatusText(PlaybackViewModel playbackViewModel, LoopEditorViewModel loopEditorViewModel, LibraryViewModel libraryViewModel)
    {
        if (!playbackViewModel.HasCurrentSong)
        {
            return libraryViewModel.LibraryStatusText;
        }

        string playbackStateStr = GetPlaybackStateString(playbackViewModel.CurrentPlaybackStatus);
        string baseStatus = $"{playbackStateStr}: {playbackViewModel.CurrentSong?.Title ?? "Unknown Song"}";
        string loopStatus = GetLoopStatusString(loopEditorViewModel, playbackViewModel.CurrentSong);
        string modeStatus = GetPlaybackModeStatusString(playbackViewModel.ModeControls);

        return $"{baseStatus}{loopStatus}{modeStatus}";
    }

    private static string GetPlaybackStateString(PlaybackStateStatus status)
    {
        return status switch
        {
            PlaybackStateStatus.Playing => "Playing",
            PlaybackStateStatus.Paused => "Paused",
            PlaybackStateStatus.Stopped => "Stopped",
            _ => "Idle"
        };
    }

    private static string GetLoopStatusString(LoopEditorViewModel loopEditorViewModel, Song? currentSong)
    {
        if (loopEditorViewModel.ActiveLoop.IsLoopActive && currentSong?.SavedLoop != null)
        {
            return " (Loop Active)";
        }

        return string.Empty;
    }

    private static string GetPlaybackModeStatusString(PlaybackModeViewModel modeControls)
    {
        string modeStatus = "";
        if (modeControls.ShuffleEnabled)
        {
            modeStatus += " | Shuffle";
        }

        modeStatus += modeControls.RepeatMode switch
        {
            RepeatMode.None => " | Do Nothing",
            RepeatMode.PlayOnce => " | Play Once",
            RepeatMode.RepeatOne => " | Repeat Song",
            RepeatMode.RepeatAll => " | Repeat All",
            _ => ""
        };
        return modeStatus;
    }
}