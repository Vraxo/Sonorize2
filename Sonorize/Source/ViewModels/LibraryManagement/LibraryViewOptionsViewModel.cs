using Sonorize.ViewModels;

namespace Sonorize.ViewModels.LibraryManagement;

public class LibraryViewOptionsViewModel : ViewModelBase
{
    private bool _showArtist = true;
    public bool ShowArtist
    {
        get => _showArtist;
        set => SetProperty(ref _showArtist, value);
    }

    private bool _showAlbum = true;
    public bool ShowAlbum
    {
        get => _showAlbum;
        set => SetProperty(ref _showAlbum, value);
    }

    private bool _showDuration = true;
    public bool ShowDuration
    {
        get => _showDuration;
        set => SetProperty(ref _showDuration, value);
    }
}
