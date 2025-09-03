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
                // Direct search query input might clear artist/album selections
                // or this can be handled by LibraryViewModel reacting to this change.
                // For now, assume direct search query changes don't auto-clear selected artist/album.
                // LibraryViewModel's ApplyFilter logic will decide precedence.
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
                    // When an artist is selected, update SearchQuery and clear SelectedAlbum
                    _searchQuery = _selectedArtist.Name ?? string.Empty; // Avoid raising SearchQuery's own event storm
                    OnPropertyChanged(nameof(SearchQuery)); // Manually notify SearchQuery changed
                    SelectedAlbum = null; // This will trigger its own PropertyChanged and subsequently FilterCriteriaChanged
                }
                else
                {
                    // When artist is deselected, clear the search query
                    SearchQuery = string.Empty;
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
                    SearchQuery = string.Empty;
                    OnPropertyChanged(nameof(SearchQuery));
                    SelectedArtist = null;
                    SelectedAlbum = null;
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
                    // When an album is selected, update SearchQuery and clear SelectedArtist
                    _searchQuery = _selectedAlbum.Title ?? string.Empty; // Avoid raising SearchQuery's own event storm
                    OnPropertyChanged(nameof(SearchQuery)); // Manually notify SearchQuery changed
                    SelectedArtist = null; // This will trigger its own PropertyChanged and subsequently FilterCriteriaChanged
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