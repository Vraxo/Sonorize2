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
                    // Heuristic to handle M3U paths that are relative to a "library root"
                    // which is one level above the M3U file's directory.
                    // This happens if the path inside the M3U is like "Music/SomeFolder/song.mp3"
                    // when the M3U itself is inside the "Music" folder.
                    var m3uDirName = Path.GetFileName(m3uDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    var songPathFirstSegment = songPath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, 2).FirstOrDefault();

                    string basePath = m3uDirectory;
                    if (!string.IsNullOrEmpty(m3uDirName) && m3uDirName.Equals(songPathFirstSegment, StringComparison.OrdinalIgnoreCase))
                    {
                        var m3uParentDir = Path.GetDirectoryName(m3uDirectory);
                        if (m3uParentDir is not null)
                        {
                            basePath = m3uParentDir;
                        }
                    }
                    absolutePath = Path.GetFullPath(Path.Combine(basePath, songPath));
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