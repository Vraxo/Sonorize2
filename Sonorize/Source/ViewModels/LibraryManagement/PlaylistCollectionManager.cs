using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using Sonorize.Models;
using Sonorize.Services;
using System;

namespace Sonorize.ViewModels.LibraryManagement;

public class PlaylistCollectionManager
{
    private readonly ObservableCollection<PlaylistViewModel> _playlistsCollection;
    private readonly MusicLibraryService _musicLibraryService;

    public PlaylistCollectionManager(ObservableCollection<PlaylistViewModel> playlistsCollection, MusicLibraryService musicLibraryService)
    {
        _playlistsCollection = playlistsCollection;
        _musicLibraryService = musicLibraryService;
    }

    public void PopulateCollection(IEnumerable<Playlist> allPlaylists)
    {
        _playlistsCollection.Clear();
        var defaultIcon = _musicLibraryService.GetDefaultThumbnail();

        // Sort auto-playlists first, then sort alphabetically within each group
        var sortedPlaylists = allPlaylists
            .OrderByDescending(p => p.IsAutoPlaylist)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var playlist in sortedPlaylists)
        {
            _playlistsCollection.Add(new PlaylistViewModel(playlist, defaultIcon));
        }
    }

    public void HandleSongThumbnailUpdate(Song updatedSong)
    {
        // Find all playlists containing this song
        var affectedPlaylists = _playlistsCollection
            .Where(pvm => pvm.PlaylistModel.Songs.Contains(updatedSong));

        foreach (var playlistVM in affectedPlaylists)
        {
            playlistVM.RecalculateThumbnails();
        }
    }
}