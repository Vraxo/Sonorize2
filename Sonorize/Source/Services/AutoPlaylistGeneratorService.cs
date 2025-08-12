using System;
using System.Collections.Generic;
using System.Linq;
using Sonorize.Models;

namespace Sonorize.Services;

public class AutoPlaylistGeneratorService
{
    public List<Playlist> GenerateAll(IEnumerable<Song> allSongs)
    {
        var autoPlaylists = new List<Playlist>();

        // Generate "Recently Added"
        var recentlyAdded = GenerateRecentlyAddedPlaylist(allSongs, 50);
        if (recentlyAdded.Songs.Any())
        {
            autoPlaylists.Add(recentlyAdded);
        }

        // Generate "Most Played" and always add it, even if empty.
        var mostPlayed = GenerateMostPlayedPlaylist(allSongs, 25);
        autoPlaylists.Add(mostPlayed);


        return autoPlaylists;
    }

    private Playlist GenerateRecentlyAddedPlaylist(IEnumerable<Song> allSongs, int count)
    {
        var songs = allSongs.OrderByDescending(s => s.DateAdded).Take(count).ToList();
        return new Playlist
        {
            Name = "Recently Added",
            IsAutoPlaylist = true,
            Songs = songs
        };
    }

    private Playlist GenerateMostPlayedPlaylist(IEnumerable<Song> allSongs, int count)
    {
        // Now implemented to use the PlayCount property
        var songs = allSongs
            .Where(s => s.PlayCount > 0)
            .OrderByDescending(s => s.PlayCount)
            .ThenByDescending(s => s.DateAdded) // Secondary sort for ties
            .Take(count)
            .ToList();

        return new Playlist
        {
            Name = "Most Played",
            IsAutoPlaylist = true,
            Songs = songs
        };
    }
}