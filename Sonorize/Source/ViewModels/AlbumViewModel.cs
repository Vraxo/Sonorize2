using System.Collections.Generic; // For List
using Avalonia.Media.Imaging;
// Removed System.Collections.ObjectModel as List is sufficient here and ViewModelBase handles INPC

namespace Sonorize.ViewModels;

public class AlbumViewModel : ViewModelBase
{
    public string? Title { get; set; }
    public string? Artist { get; set; }
    public int SongCount { get; set; }
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
