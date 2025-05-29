using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using Sonorize.Models;
using Sonorize.Services;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryGroupingsViewModel : ViewModelBase
{
    private readonly ArtistAlbumCollectionManager _artistAlbumManager;
    private readonly MusicLibraryService _musicLibraryService; // For ArtistAlbumCollectionManager

    internal ArtistAlbumCollectionManager ArtistAlbumManager => _artistAlbumManager; // Expose for LibraryDataOrchestrator

    public ObservableCollection<ArtistViewModel> Artists { get; } = [];
    public ObservableCollection<AlbumViewModel> Albums { get; } = [];

    public LibraryGroupingsViewModel(MusicLibraryService musicLibraryService)
    {
        _musicLibraryService = musicLibraryService ?? throw new ArgumentNullException(nameof(musicLibraryService));
        _artistAlbumManager = new ArtistAlbumCollectionManager(Artists, Albums, _musicLibraryService);
    }

    public void PopulateCollections(IEnumerable<Song> allSongs)
    {
        // This method will be called on the UI thread by LibraryViewModel
        _artistAlbumManager.PopulateCollections(allSongs);
        // ObservableCollections will notify changes automatically
    }

    public void HandleSongThumbnailUpdate(Song updatedSong, IEnumerable<Song> allSongs)
    {
        // This method will be called on the UI thread by LibraryViewModel
        _artistAlbumManager.UpdateCollectionsForSongThumbnail(updatedSong, allSongs);
        // ObservableCollections and their items will notify changes
    }
}