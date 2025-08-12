using System.Collections.Generic;
using System.Linq;
using Avalonia.Media.Imaging;
using Sonorize.Models;

namespace Sonorize.ViewModels;

public class PlaylistViewModel : ViewModelBase
{
    public Playlist PlaylistModel { get; }

    private readonly Bitmap? _defaultIcon;

    public string Name => PlaylistModel.Name;
    public int SongCount => PlaylistModel.Songs.Count;

    public List<Bitmap?> SongThumbnailsForGrid
    {
        get;
        private set
        {
            SetProperty(ref field, value);
        }

    } = new(new Bitmap?[4]);

    public Bitmap? RepresentativeThumbnail
    {
        get;

        private set
        {
            SetProperty(ref field, value);
        }
    }


    public PlaylistViewModel(Playlist playlist, Bitmap? defaultIcon)
    {
        PlaylistModel = playlist;
        _defaultIcon = defaultIcon;
        RecalculateThumbnails();
    }

    public void RecalculateThumbnails()
    {
        List<Bitmap?> newGrid = new(new Bitmap?[4]);

        List<Bitmap?> distinctSongThumbs = PlaylistModel.Songs
            .Select(s => s.Thumbnail)
            .Where(t => t is not null)
            .Distinct()
            .Take(4)
            .ToList();

        for (int i = 0; i < distinctSongThumbs.Count; i++)
        {
            newGrid[i] = distinctSongThumbs[i];
        }

        if (!SongThumbnailsForGrid.SequenceEqual(newGrid))
        {
            SongThumbnailsForGrid = newGrid;
        }

        Bitmap? newRepresentativeThumbnail = newGrid.FirstOrDefault(t => t is not null) ?? _defaultIcon;
        RepresentativeThumbnail = newRepresentativeThumbnail;
    }
}