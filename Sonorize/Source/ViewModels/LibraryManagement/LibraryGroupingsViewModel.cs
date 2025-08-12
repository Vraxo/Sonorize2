using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryGroupingsViewModel : ViewModelBase
{
    private readonly ArtistAlbumCollectionManager _artistAlbumManager;
    private readonly MusicLibraryService _musicLibraryService;
    private readonly PlaylistCollectionManager _playlistManager;

    internal ArtistAlbumCollectionManager ArtistAlbumManager => _artistAlbumManager;
    internal PlaylistCollectionManager PlaylistManager => _playlistManager;

    public ObservableCollection<ArtistViewModel> Artists { get; } = [];
    public ObservableCollection<AlbumViewModel> Albums { get; } = [];
    public ObservableCollection<PlaylistViewModel> Playlists { get; } = [];

    public LibraryGroupingsViewModel(MusicLibraryService musicLibraryService)
    {
        _musicLibraryService = musicLibraryService ?? throw new ArgumentNullException(nameof(musicLibraryService));
        _artistAlbumManager = new ArtistAlbumCollectionManager(Artists, Albums, _musicLibraryService);
        _playlistManager = new PlaylistCollectionManager(Playlists, _musicLibraryService);
    }

    public void PopulateCollections(IEnumerable<Song> allSongs)
    {
        _artistAlbumManager.PopulateCollections(allSongs);
    }

    public void PopulatePlaylistCollection(IEnumerable<Playlist> allPlaylists)
    {
        _playlistManager.PopulateCollection(allPlaylists);
    }

    public void HandleSongThumbnailUpdate(Song updatedSong, IEnumerable<Song> allSongs)
    {
        // This method will be called on the UI thread by LibraryViewModel
        _artistAlbumManager.UpdateCollectionsForSongThumbnail(updatedSong, allSongs);
        _playlistManager.HandleSongThumbnailUpdate(updatedSong);
    }
}