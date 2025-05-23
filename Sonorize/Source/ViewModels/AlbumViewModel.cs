using Avalonia.Media.Imaging;
using System.Collections.Generic; // For List
// Removed System.Collections.ObjectModel as List is sufficient here and ViewModelBase handles INPC

namespace Sonorize.ViewModels;

public class AlbumViewModel : ViewModelBase
{
    public string? Title { get; set; }
    public string? Artist { get; set; }

    private List<Bitmap?> _songThumbnailsForGrid = new List<Bitmap?>(new Bitmap?[4]); // Ensures 4 elements, can be null
    public List<Bitmap?> SongThumbnailsForGrid
    {
        get => _songThumbnailsForGrid;
        // Setter might be used by LibraryVM during initialization
        set => SetProperty(ref _songThumbnailsForGrid, value);
    }

    private Bitmap? _representativeThumbnail;
    public Bitmap? RepresentativeThumbnail
    {
        get => _representativeThumbnail;
        set => SetProperty(ref _representativeThumbnail, value);
    }

    public string DisplayText => $"{Title} - {Artist}";
}