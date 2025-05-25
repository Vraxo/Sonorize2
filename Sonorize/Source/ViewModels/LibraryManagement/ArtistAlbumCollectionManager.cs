using Avalonia.Media.Imaging;
using Sonorize.Models;
using Sonorize.Services;
using Sonorize.Utils; // For AlbumArtistTupleComparer
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Sonorize.ViewModels.LibraryManagement;

public class ArtistAlbumCollectionManager
{
    private readonly ObservableCollection<ArtistViewModel> _artistsCollection;
    private readonly ObservableCollection<AlbumViewModel> _albumsCollection;
    private readonly MusicLibraryService _musicLibraryService; // For default thumbnail

    public ArtistAlbumCollectionManager(
        ObservableCollection<ArtistViewModel> artistsCollection,
        ObservableCollection<AlbumViewModel> albumsCollection,
        MusicLibraryService musicLibraryService)
    {
        _artistsCollection = artistsCollection ?? throw new ArgumentNullException(nameof(artistsCollection));
        _albumsCollection = albumsCollection ?? throw new ArgumentNullException(nameof(albumsCollection));
        _musicLibraryService = musicLibraryService ?? throw new ArgumentNullException(nameof(musicLibraryService));
    }

    public void PopulateCollections(IEnumerable<Song> allSongs)
    {
        _artistsCollection.Clear();
        var uniqueArtistNames = allSongs
            .Where(s => !string.IsNullOrWhiteSpace(s.Artist))
            .Select(s => s.Artist!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(a => a, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Bitmap? defaultSongThumbnail = _musicLibraryService.GetDefaultThumbnail();
        foreach (string? artistName in uniqueArtistNames)
        {
            Bitmap? repThumb = allSongs.FirstOrDefault(s =>
                                   (s.Artist?.Equals(artistName, StringComparison.OrdinalIgnoreCase) ?? false) &&
                                   s.Thumbnail != null && s.Thumbnail != defaultSongThumbnail)?.Thumbnail
                               ?? defaultSongThumbnail;
            _artistsCollection.Add(new ArtistViewModel { Name = artistName, Thumbnail = repThumb });
        }

        _albumsCollection.Clear();
        Func<Song, (string Album, string Artist)> keySelector = s => (s.Album?.Trim() ?? string.Empty, s.Artist?.Trim() ?? string.Empty);
        var uniqueAlbumsData = allSongs
            .Where(s => !string.IsNullOrWhiteSpace(s.Album) && !string.IsNullOrWhiteSpace(s.Artist))
            .GroupBy(keySelector, AlbumArtistTupleComparer.Instance)
            .Select(g => new
            {
                AlbumTitle = g.Key.Item1,
                ArtistName = g.Key.Item2,
                SongsInAlbum = g.ToList()
            })
            .OrderBy(a => a.ArtistName, StringComparer.OrdinalIgnoreCase).ThenBy(a => a.AlbumTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var albumData in uniqueAlbumsData)
        {
            AlbumViewModel albumVM = new()
            {
                Title = albumData.AlbumTitle,
                Artist = albumData.ArtistName
            };

            List<Bitmap?> songThumbnailsForGrid = new(new Bitmap?[4]);
            List<Bitmap?> distinctSongThumbs = albumData.SongsInAlbum
                                                 .Select(s => s.Thumbnail ?? defaultSongThumbnail)
                                                 .Distinct()
                                                 .Take(4)
                                                 .ToList();

            for (int i = 0; i < distinctSongThumbs.Count; i++)
            {
                songThumbnailsForGrid[i] = distinctSongThumbs[i];
            }

            albumVM.SongThumbnailsForGrid = songThumbnailsForGrid;
            albumVM.RepresentativeThumbnail = songThumbnailsForGrid.FirstOrDefault(t => t != null) ?? defaultSongThumbnail;

            _albumsCollection.Add(albumVM);
        }
    }

    public void UpdateCollectionsForSongThumbnail(Song updatedSong, IEnumerable<Song> allSongs)
    {
        // Update ArtistViewModel
        var artistVM = _artistsCollection.FirstOrDefault(a => a.Name == updatedSong.Artist);
        if (artistVM != null)
        {
            var firstSongOfArtistWithThumbnail = allSongs.FirstOrDefault(s =>
                (s.Artist?.Equals(artistVM.Name, StringComparison.OrdinalIgnoreCase) ?? false) && s.Thumbnail != _musicLibraryService.GetDefaultThumbnail());

            Bitmap? newArtistThumbnail = (firstSongOfArtistWithThumbnail?.Thumbnail ?? _musicLibraryService.GetDefaultThumbnail());
            if (artistVM.Thumbnail != newArtistThumbnail) // Only update if changed to avoid needless notifications
            {
                artistVM.Thumbnail = newArtistThumbnail;
            }
        }

        // Update AlbumViewModel
        var albumVM = _albumsCollection.FirstOrDefault(al => al.Title == updatedSong.Album && al.Artist == updatedSong.Artist);
        if (albumVM != null)
        {
            var songsInAlbum = allSongs.Where(s => (s.Album?.Equals(albumVM.Title, StringComparison.OrdinalIgnoreCase) ?? false) &&
                                                    (s.Artist?.Equals(albumVM.Artist, StringComparison.OrdinalIgnoreCase) ?? false))
                                         .ToList();

            Bitmap? defaultSongThumbnail = _musicLibraryService.GetDefaultThumbnail();
            List<Bitmap?> newSongThumbnailsForGrid = new List<Bitmap?>(new Bitmap?[4]);
            List<Bitmap?> distinctSongThumbs = songsInAlbum
                                                 .Select(s => s.Thumbnail ?? defaultSongThumbnail)
                                                 .Distinct()
                                                 .Take(4)
                                                 .ToList();

            for (int i = 0; i < distinctSongThumbs.Count; i++)
            {
                newSongThumbnailsForGrid[i] = distinctSongThumbs[i];
            }

            // Check if the grid thumbnails actually changed before assigning to avoid unnecessary UI updates.
            if (!albumVM.SongThumbnailsForGrid.SequenceEqual(newSongThumbnailsForGrid))
            {
                albumVM.SongThumbnailsForGrid = newSongThumbnailsForGrid;
            }

            Bitmap? newRepresentativeThumbnail = newSongThumbnailsForGrid.FirstOrDefault(t => t != null) ?? defaultSongThumbnail;
            if (albumVM.RepresentativeThumbnail != newRepresentativeThumbnail)
            {
                albumVM.RepresentativeThumbnail = newRepresentativeThumbnail;
            }
        }
    }
}