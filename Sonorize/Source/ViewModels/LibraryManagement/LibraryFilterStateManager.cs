using System;
using Sonorize.Models; // For ArtistViewModel, AlbumViewModel

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryFilterStateManager : ViewModelBase
{
    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    private ArtistViewModel? _selectedArtist;
    public ArtistViewModel? SelectedArtist
    {
        get => _selectedArtist;
        set => SetProperty(ref _selectedArtist, value);
    }

    private PlaylistViewModel? _selectedPlaylist;
    public PlaylistViewModel? SelectedPlaylist
    {
        get => _selectedPlaylist;
        set => SetProperty(ref _selectedPlaylist, value);
    }

    private AlbumViewModel? _selectedAlbum;
    public AlbumViewModel? SelectedAlbum
    {
        get => _selectedAlbum;
        set => SetProperty(ref _selectedAlbum, value);
    }

    public void ClearSelectionsAndSearch()
    {
        // This method directly manipulates the backing fields and then fires the
        // change notification once, avoiding the chain reactions of the public setters.
        // This is useful when resetting the state completely, like before a library load.
        bool changed = false;

        if (_selectedArtist != null)
        {
            _selectedArtist = null;
            OnPropertyChanged(nameof(SelectedArtist));
            changed = true;
        }
        if (_selectedAlbum != null)
        {
            _selectedAlbum = null;
            OnPropertyChanged(nameof(SelectedAlbum));
            changed = true;
        }
        if (_selectedPlaylist != null)
        {
            _selectedPlaylist = null;
            OnPropertyChanged(nameof(SelectedPlaylist));
            changed = true;
        }
        if (!string.IsNullOrEmpty(_searchQuery))
        {
            _searchQuery = string.Empty;
            OnPropertyChanged(nameof(SearchQuery));
            changed = true;
        }
    }
}