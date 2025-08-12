using System;
using System.Diagnostics;
using System.IO;
using Avalonia.Media.Imaging;
using Sonorize.Models;
using TagLib; // Required for TagLib.File, CorruptFileException, UnsupportedFormatException

namespace Sonorize.Services;

public class SongFactory
{
    private readonly LoopDataService _loopDataService;
    private readonly PlayCountDataService _playCountDataService;

    public SongFactory(LoopDataService loopDataService, PlayCountDataService playCountDataService)
    {
        _loopDataService = loopDataService ?? throw new ArgumentNullException(nameof(loopDataService));
        _playCountDataService = playCountDataService ?? throw new ArgumentNullException(nameof(playCountDataService));
        Debug.WriteLine("[SongFactory] Initialized.");
    }

    public Song CreateSongFromFile(string filePath, Bitmap? defaultThumbnail)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.WriteLine("[SongFactory] FilePath is null or empty. Returning a default Song object.");
            return new Song { FilePath = string.Empty, Thumbnail = defaultThumbnail };
        }

        var song = new Song
        {
            FilePath = filePath,
            Title = Path.GetFileNameWithoutExtension(filePath), // Default title
            Artist = "Unknown Artist",                         // Default artist
            Album = "Unknown Album",                           // Default album
            Duration = TimeSpan.Zero,                          // Default duration
            Thumbnail = defaultThumbnail                       // Initial default thumbnail
        };

        try
        {
            using var tagFile = TagLib.File.Create(filePath);
            if (!string.IsNullOrWhiteSpace(tagFile.Tag.Title)) song.Title = tagFile.Tag.Title;

            if (tagFile.Tag.Performers.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.Performers[0]))
                song.Artist = tagFile.Tag.Performers[0];
            else if (tagFile.Tag.AlbumArtists.Length > 0 && !string.IsNullOrWhiteSpace(tagFile.Tag.AlbumArtists[0]))
                song.Artist = tagFile.Tag.AlbumArtists[0];

            if (!string.IsNullOrWhiteSpace(tagFile.Tag.Album)) song.Album = tagFile.Tag.Album;
            if (tagFile.Properties.Duration > TimeSpan.Zero) song.Duration = tagFile.Properties.Duration;
        }
        catch (CorruptFileException cfe)
        {
            Debug.WriteLine($"[SongFactory] Corrupt file when reading tags for \"{Path.GetFileName(filePath)}\": {cfe.Message}. Song will use defaults.");
        }
        catch (UnsupportedFormatException ufe)
        {
            Debug.WriteLine($"[SongFactory] Unsupported format when reading tags for \"{Path.GetFileName(filePath)}\": {ufe.Message}. Song will use defaults.");
        }
        catch (Exception ex)
        {
            // Catching generic Exception for any other TagLib# related issues
            Debug.WriteLine($"[SongFactory] Error reading tags for \"{Path.GetFileName(filePath)}\": {ex.Message}. Song will use defaults.");
        }

        try
        {
            song.DateAdded = new FileInfo(filePath).CreationTimeUtc;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SongFactory] Could not read file creation time for {Path.GetFileName(filePath)}: {ex.Message}. Using current time as fallback.");
            song.DateAdded = DateTime.UtcNow;
        }

        var storedLoopData = _loopDataService.GetLoop(song.FilePath);
        if (storedLoopData is not null)
        {
            song.SavedLoop = new LoopRegion(storedLoopData.Start, storedLoopData.End);
            song.IsLoopActive = storedLoopData.IsActive;
        }

        song.PlayCount = _playCountDataService.GetPlayCount(song.FilePath);

        return song;
    }
}