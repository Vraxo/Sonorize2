using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using Sonorize.Models;

namespace Sonorize.ViewModels;

public class PlaylistViewModel : ViewModelBase
{
    public string Name => PlaylistModel.Name;
    public int SongCount => PlaylistModel.Songs.Count;
    public Playlist PlaylistModel { get; }
    public List<Bitmap?> SongThumbnailsForGrid { get; } = new(new Bitmap?[4]);
    public Bitmap? RepresentativeThumbnail { get; }

    public PlaylistViewModel(Playlist playlist, Bitmap? defaultIcon)
    {
        PlaylistModel = playlist;

        List<Bitmap?> distinctSongThumbs = playlist.Songs
            .Where(s => s.Thumbnail is not null)
            .Select(s => s.Thumbnail)
            .Distinct()
            .Take(4)
            .ToList();

        for (int i = 0; i < distinctSongThumbs.Count; i++)
        {
            SongThumbnailsForGrid[i] = distinctSongThumbs[i];
        }

        RepresentativeThumbnail = SongThumbnailsForGrid.FirstOrDefault(t => t is not null) ?? defaultIcon;
    }
}
