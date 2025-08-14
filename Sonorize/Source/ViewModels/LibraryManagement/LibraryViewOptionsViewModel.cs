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

    public void LoadFromSettings(AppSettings settings)
    {
        ShowArtist = settings.ShowArtistInLibrary;
        ShowAlbum = settings.ShowAlbumInLibrary;
        ShowDuration = settings.ShowDurationInLibrary;
    }
}
