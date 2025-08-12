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

        // Generate "Most Played" (placeholder)
        autoPlaylists.Add(GenerateMostPlayedPlaylist(allSongs, 25));

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
        // Placeholder for now. This would require play count tracking.
        return new Playlist
        {
            Name = "Most Played",
            IsAutoPlaylist = true,
            Songs = new List<Song>() // Empty for now
        };
    }
}
