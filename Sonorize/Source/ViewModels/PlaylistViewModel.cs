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

    private List<Bitmap?> _songThumbnailsForGrid = new(new Bitmap?[4]);
    public List<Bitmap?> SongThumbnailsForGrid
    {
        get => _songThumbnailsForGrid;
        private set => SetProperty(ref _songThumbnailsForGrid, value);
    }

    private Bitmap? _representativeThumbnail;
    public Bitmap? RepresentativeThumbnail
    {
        get => _representativeThumbnail;
        private set => SetProperty(ref _representativeThumbnail, value);
    }

    private readonly Bitmap? _defaultIcon;

    public PlaylistViewModel(Playlist playlist, Bitmap? defaultIcon)
    {
        PlaylistModel = playlist;
        _defaultIcon = defaultIcon;
        RecalculateThumbnails();
    }

    public void RecalculateThumbnails()
    {
        var newGrid = new List<Bitmap?>(new Bitmap?[4]);

        var distinctSongThumbs = PlaylistModel.Songs
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

        var newRepresentativeThumbnail = newGrid.FirstOrDefault(t => t is not null) ?? _defaultIcon;
        RepresentativeThumbnail = newRepresentativeThumbnail;
    }
}