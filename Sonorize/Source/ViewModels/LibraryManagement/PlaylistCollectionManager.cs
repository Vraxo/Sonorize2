using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media.Imaging;
using Sonorize.Models;
using Sonorize.Services;

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
        foreach (var playlist in allPlaylists.OrderBy(p => p.Name))
        {
            _playlistsCollection.Add(new PlaylistViewModel(playlist, defaultIcon));
        }
    }
}
