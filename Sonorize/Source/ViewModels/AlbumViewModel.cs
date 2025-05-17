using Avalonia.Media.Imaging;

namespace Sonorize.ViewModels;

public class AlbumViewModel
{
    public string? Title { get; set; }       // Album Title
    public string? Artist { get; set; }      // Primary Artist for this album
    public Bitmap? Thumbnail { get; set; }
    public string DisplayText => $"{Title} - {Artist}"; // For simpler display if needed
}