using Avalonia.Media.Imaging;

namespace Sonorize.ViewModels;

public class ArtistViewModel
{
    public string? Name { get; set; }
    public Bitmap? Thumbnail { get; set; }
    // You could add more properties later, like SongCount or AlbumCount
}