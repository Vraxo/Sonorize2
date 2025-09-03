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
                    RequestTabSwitchToLibrary?.Invoke(this, EventArgs.Empty);
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
                    RequestTabSwitchToLibrary?.Invoke(this, EventArgs.Empty);
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
                    RequestTabSwitchToLibrary?.Invoke(this, EventArgs.Empty);
                }
                FilterCriteriaChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? FilterCriteriaChanged;
    public event EventHandler? RequestTabSwitchToLibrary;

    public void ClearSelectionsAndSearch()
    {
        // Use the public setters to ensure all logic (including clearing other properties) is triggered.
        // The setters will ultimately trigger FilterCriteriaChanged.
        // We set them in an order that prevents unwanted side-effects, e.g., setting SearchQuery last.
        SelectedPlaylist = null;
        SelectedArtist = null;
        SelectedAlbum = null;
        SearchQuery = string.Empty;
    }
}
