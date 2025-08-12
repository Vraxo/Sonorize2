using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using Sonorize.Models;
using Sonorize.Services;
using System;
using System.Diagnostics;

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

    public void UpdateAutoPlaylists(IEnumerable<PlaylistViewModel> newAutoPlaylists)
    {
        Debug.WriteLine("[PlaylistCollectionManager] Updating auto-playlists.");
        // Remove existing auto-playlists
        var existingAutoPlaylists = _playlistsCollection.Where(p => p.IsAutoPlaylist).ToList();
        foreach (var p in existingAutoPlaylists)
        {
            _playlistsCollection.Remove(p);
        }

        // Insert new ones at the top, respecting their own order
        var sortedNewPlaylists = newAutoPlaylists
            .OrderByDescending(p => p.PlaylistModel.IsAutoPlaylist) // Should all be true, but for safety
            .ThenBy(p => p.PlaylistModel.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (int i = 0; i < sortedNewPlaylists.Count; i++)
        {
            _playlistsCollection.Insert(i, sortedNewPlaylists[i]);
        }
        Debug.WriteLine($"[PlaylistCollectionManager] Finished updating auto-playlists. Total playlists: {_playlistsCollection.Count}");
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