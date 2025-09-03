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
    public bool ShowAlbum
    {
        get;
        set => SetProperty(ref field, value);
    }
    public bool ShowDuration
    {
        get;
        set => SetProperty(ref field, value);
    }
    public bool ShowDateAdded
    {
        get;
        set => SetProperty(ref field, value);
    }
    public bool ShowPlayCount
    {
        get;
        set => SetProperty(ref field, value);
    }
    public double RowHeight
    {
        get;
        set => SetProperty(ref field, value);
    }
    public bool EnableAlternatingRowColors
    {
        get;
        set => SetProperty(ref field, value);
    }

    public void LoadFromSettings(AppSettings settings)
    {
        ShowArtist = settings.Appearance.ShowArtistInLibrary;
        ShowAlbum = settings.Appearance.ShowAlbumInLibrary;
        ShowDuration = settings.Appearance.ShowDurationInLibrary;
        ShowDateAdded = settings.Appearance.ShowDateAddedInLibrary;
        ShowPlayCount = settings.Appearance.ShowPlayCountInLibrary;
        RowHeight = settings.Appearance.LibraryRowHeight;
        EnableAlternatingRowColors = settings.Appearance.EnableAlternatingRowColors;
    }
}