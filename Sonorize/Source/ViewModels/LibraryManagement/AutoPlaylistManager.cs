using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels.LibraryManagement;

public class AutoPlaylistManager
{
    private readonly AutoPlaylistGeneratorService _generator = new();
    private readonly PlaylistCollectionManager _playlistCollectionManager;
    private readonly SongListManager _songListManager;
    private readonly MusicLibraryService _musicLibraryService;

    public AutoPlaylistManager(
        PlaylistCollectionManager playlistCollectionManager,
        SongListManager songListManager,
        MusicLibraryService musicLibraryService)
    {
        _playlistCollectionManager = playlistCollectionManager ?? throw new ArgumentNullException(nameof(playlistCollectionManager));
        _songListManager = songListManager ?? throw new ArgumentNullException(nameof(songListManager));
        _musicLibraryService = musicLibraryService ?? throw new ArgumentNullException(nameof(musicLibraryService));
    }

    public List<Playlist> GenerateInitialAutoPlaylists(IEnumerable<Song> allSongs)
    {
        return _generator.GenerateAll(allSongs);
    }

    public void RefreshAutoPlaylists()
    {
        var allSongs = _songListManager.GetAllSongsReadOnly();
        var newAutoPlaylists = _generator.GenerateAll(allSongs);

        var newAutoPlaylistVMs = newAutoPlaylists
            .Select(p => new PlaylistViewModel(p, _musicLibraryService.GetDefaultThumbnail()))
            .ToList();

        _playlistCollectionManager.UpdateAutoPlaylists(newAutoPlaylistVMs);
    }
}