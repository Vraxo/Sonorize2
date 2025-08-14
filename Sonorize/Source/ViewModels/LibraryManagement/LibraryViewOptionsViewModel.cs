using Sonorize.Models;
using Sonorize.ViewModels;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryViewOptionsViewModel : ViewModelBase
{
    private bool _showArtist;
    public bool ShowArtist
    {
        get => _showArtist;
        set => SetProperty(ref _showArtist, value);
    }

    private bool _showAlbum;
    public bool ShowAlbum
    {
        get => _showAlbum;
        set => SetProperty(ref _showAlbum, value);
    }

    private bool _showDuration;
    public bool ShowDuration
    {
        get => _showDuration;
        set => SetProperty(ref _showDuration, value);
    }

    private bool _showDateAdded;
    public bool ShowDateAdded
    {
        get => _showDateAdded;
        set => SetProperty(ref _showDateAdded, value);
    }

    private bool _showPlayCount;
    public bool ShowPlayCount
    {
        get => _showPlayCount;
        set => SetProperty(ref _showPlayCount, value);
    }

    private double _rowHeight;
    public double RowHeight
    {
        get => _rowHeight;
        set => SetProperty(ref _rowHeight, value);
    }

    public void LoadFromSettings(AppSettings settings)
    {
        ShowArtist = settings.ShowArtistInLibrary;
        ShowAlbum = settings.ShowAlbumInLibrary;
        ShowDuration = settings.ShowDurationInLibrary;
        ShowDateAdded = settings.ShowDateAddedInLibrary;
        ShowPlayCount = settings.ShowPlayCountInLibrary;
        RowHeight = settings.LibraryRowHeight;
    }
}
