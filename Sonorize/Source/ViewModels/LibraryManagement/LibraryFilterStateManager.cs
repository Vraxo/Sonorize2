using System;
using Sonorize.Models; // For ArtistViewModel, AlbumViewModel

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryFilterStateManager : ViewModelBase
{
    private string _searchQuery = string.Empty;
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                FilterCriteriaChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private ArtistViewModel? _selectedArtist;
    public ArtistViewModel? SelectedArtist
    {
        get => _selectedArtist;
        set
        {
            if (SetProperty(ref _selectedArtist, value))
            {
                if (_selectedArtist is not null)
                {
                    // When an artist is selected, clear other selections.
                    // Use backing fields to prevent event storms from setters.
                    if (_selectedAlbum is not null)
                    {
                        _selectedAlbum = null;
                        OnPropertyChanged(nameof(SelectedAlbum));
                    }
                    if (_selectedPlaylist is not null)
                    {
                        _selectedPlaylist = null;
                        OnPropertyChanged(nameof(SelectedPlaylist));
                    }
                }
                FilterCriteriaChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private PlaylistViewModel? _selectedPlaylist;
    public PlaylistViewModel? SelectedPlaylist
    {
        get => _selectedPlaylist;
        set
        {
            if (SetProperty(ref _selectedPlaylist, value))
            {
                if (_selectedPlaylist is not null)
                {
                    if (_selectedArtist is not null)
                    {
                        _selectedArtist = null;
                        OnPropertyChanged(nameof(SelectedArtist));
                    }
                    if (_selectedAlbum is not null)
                    {
                        _selectedAlbum = null;
                        OnPropertyChanged(nameof(SelectedAlbum));
                    }
                }
                FilterCriteriaChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private AlbumViewModel? _selectedAlbum;
    public AlbumViewModel? SelectedAlbum
    {
        get => _selectedAlbum;
        set
        {
            if (SetProperty(ref _selectedAlbum, value))
            {
                if (_selectedAlbum is not null)
                {
                    if (_selectedArtist is not null)
                    {
                        _selectedArtist = null;
                        OnPropertyChanged(nameof(SelectedArtist));
                    }
                    if (_selectedPlaylist is not null)
                    {
                        _selectedPlaylist = null;
                        OnPropertyChanged(nameof(SelectedPlaylist));
                    }
                }
                FilterCriteriaChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? FilterCriteriaChanged;

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

        // If any state was changed, notify listeners that the filter criteria have changed.
        if (changed)
        {
            FilterCriteriaChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}