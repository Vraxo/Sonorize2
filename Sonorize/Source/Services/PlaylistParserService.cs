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
        Playlist playlist = new()
        {
            Name = Path.GetFileNameWithoutExtension(m3uFilePath),
            FilePath = m3uFilePath
        };

        try
        {
            string? m3uDirectory = Path.GetDirectoryName(m3uFilePath);
            IEnumerable<string> songEntries = GetSongEntriesFromM3u(m3uFilePath);

            foreach (string songEntry in songEntries)
            {
                if (!TryResolveAbsolutePath(songEntry, m3uDirectory, out var absolutePath))
                {
                    continue;
                }

                if (allSongsLookup.TryGetValue(absolutePath, out var song))
                {
                    playlist.Songs.Add(song);
                }
                else
                {
                    LogFailedLookup(playlist.Name, absolutePath);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PlaylistParser] Error parsing playlist file '{m3uFilePath}': {ex.Message}");
        }

        return playlist;
    }

    private static IEnumerable<string> GetSongEntriesFromM3u(string m3uFilePath)
    {
        return File.ReadAllLines(m3uFilePath)
                   .Select(line => line.Trim())
                   .Where(line => !string.IsNullOrEmpty(line) && !line.StartsWith('#'));
    }

    private bool TryResolveAbsolutePath(string songEntry, string? m3uDirectory, out string absolutePath)
    {
        absolutePath = string.Empty;

        if (Path.IsPathRooted(songEntry))
        {
            absolutePath = Path.GetFullPath(songEntry);
            return true;
        }

        if (m3uDirectory is null)
        {
            Debug.WriteLine($"[PlaylistParser] Cannot resolve relative path for '{songEntry}' as M3U directory is null.");
            return false;
        }

        absolutePath = DetermineAbsolutePathForRelativeEntry(songEntry, m3uDirectory);

        return true;
    }

    private string DetermineAbsolutePathForRelativeEntry(string relativeSongPath, string m3uFileDirectory)
    {
        string baseDirectory = GetBaseDirectoryForPathResolution(relativeSongPath, m3uFileDirectory);
        return Path.GetFullPath(Path.Combine(baseDirectory, relativeSongPath));
    }

    private static string GetBaseDirectoryForPathResolution(string relativeSongPath, string m3uFileDirectory)
    {
        string m3uDirectoryName = Path.GetFileName(m3uFileDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string? songPathFirstSegment = relativeSongPath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], 2).FirstOrDefault();

        bool isPathRelativeToLibraryRoot = !string.IsNullOrEmpty(m3uDirectoryName)
            && m3uDirectoryName.Equals(songPathFirstSegment, StringComparison.OrdinalIgnoreCase);

        if (!isPathRelativeToLibraryRoot)
        {
            return m3uFileDirectory;
        }

        string? libraryRoot = Path.GetDirectoryName(m3uFileDirectory);

        if (libraryRoot is not null)
        {
            return libraryRoot;
        }

        return m3uFileDirectory;
    }

    private static void LogFailedLookup(string playlistName, string failedPath)
    {
        Debug.WriteLine($"[PlaylistParser] FAILED LOOKUP: " +
            $"Song from playlist '{playlistName}' not found in library." +
            $" Path: '{failedPath}'");
    }
}