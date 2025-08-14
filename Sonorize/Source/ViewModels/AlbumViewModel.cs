using System.Collections.Generic; // For List
using Avalonia.Media.Imaging;
// Removed System.Collections.ObjectModel as List is sufficient here and ViewModelBase handles INPC

namespace Sonorize.ViewModels;

public class AlbumViewModel : ViewModelBase
{
    public string? Title { get; set; }
    public string? Artist { get; set; }

    private int _songCount;
    public int SongCount
    {
        get => _songCount;
        set => SetProperty(ref _songCount, value);
    }

    public List<Bitmap?> SongThumbnailsForGrid
    {
        get;
        // Setter might be used by LibraryVM during initialization
        set => SetProperty(ref field, value);
    } = new List<Bitmap?>(new Bitmap?[4]);

    public Bitmap? RepresentativeThumbnail
    {
        get;
        set => SetProperty(ref field, value);
    }

    public string DisplayText => $"{Title} - {Artist}";
}
