using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.Services;

public class PlaybackFlowManagerService
{
    private readonly LibraryViewModel _libraryViewModel;
    private readonly PlaybackViewModel _playbackViewModel;
    private readonly PlaybackService _playbackService;
    private readonly NextTrackSelectorService _nextTrackSelectorService;

    public PlaybackFlowManagerService(
        LibraryViewModel libraryViewModel,
        PlaybackViewModel playbackViewModel,
        PlaybackService playbackService,
        NextTrackSelectorService nextTrackSelectorService)
    {
        _libraryViewModel = libraryViewModel ?? throw new System.ArgumentNullException(nameof(libraryViewModel));
        _playbackViewModel = playbackViewModel ?? throw new System.ArgumentNullException(nameof(playbackViewModel));
        _playbackService = playbackService ?? throw new System.ArgumentNullException(nameof(playbackService));
        _nextTrackSelectorService = nextTrackSelectorService ?? throw new System.ArgumentNullException(nameof(nextTrackSelectorService));
    }

    public void HandlePlaybackEndedNaturally()
    {
        Debug.WriteLine("[PlaybackFlowManager] HandlePlaybackEndedNaturally called.");

        Song? currentSong = _libraryViewModel.SelectedSong;
        List<Song> currentList = [.. _libraryViewModel.FilteredSongs];
        RepeatMode repeatMode = _playbackViewModel.RepeatMode;
        bool shuffleEnabled = _playbackViewModel.ShuffleEnabled;

        Song? nextSong = _nextTrackSelectorService.GetNextSong(currentSong, currentList, repeatMode, shuffleEnabled);

        if (nextSong is not null)
        {
            Debug.WriteLine($"[PlaybackFlowManager] Next song determined: {nextSong.Title}. Setting Library.SelectedSong.");
            _libraryViewModel.SelectedSong = nextSong;
        }
        else
        {
            Debug.WriteLine("[PlaybackFlowManager] No next song determined. Calling PlaybackService.Stop().");
            _playbackService.Stop();
        }

        Debug.WriteLine("[PlaybackFlowManager] HandlePlaybackEndedNaturally completed.");
    }
}