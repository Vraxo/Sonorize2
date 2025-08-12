using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Sonorize.Models;

namespace Sonorize.Services;

public class PlaylistParserService
{
    public Playlist Parse(string m3uFilePath, IReadOnlyDictionary<string, Song> allSongsLookup)
    {
        var playlist = new Playlist
        {
            Name = Path.GetFileNameWithoutExtension(m3uFilePath),
            FilePath = m3uFilePath
        };

        try
        {
            var m3uDirectory = Path.GetDirectoryName(m3uFilePath);
            var songPaths = File.ReadAllLines(m3uFilePath)
                                .Select(line => line.Trim())
                                .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith('#'));

            foreach (var songPath in songPaths)
            {
                string absolutePath;
                if (Path.IsPathRooted(songPath))
                {
                    absolutePath = Path.GetFullPath(songPath);
                }
                else if (m3uDirectory is not null)
                {
                    absolutePath = Path.GetFullPath(Path.Combine(m3uDirectory, songPath));
                }
                else
                {
                    Debug.WriteLine($"[PlaylistParser] Cannot resolve relative path for '{songPath}' in playlist '{m3uFilePath}' as M3U directory is null.");
                    continue;
                }

                if (allSongsLookup.TryGetValue(absolutePath, out var song))
                {
                    playlist.Songs.Add(song);
                }
                else
                {
                    Debug.WriteLine($"[PlaylistParser] FAILED LOOKUP: Song from playlist '{playlist.Name}' not found in library. Path: '{absolutePath}'");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaylistParser] Error parsing playlist file '{m3uFilePath}': {ex.Message}");
        }

        return playlist;
    }
}
