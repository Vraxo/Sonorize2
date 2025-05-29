using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Sonorize.Models;
using TagLib;

namespace Sonorize.Services;

public class SongMetadataService
{
    public async Task<bool> SaveMetadataAsync(Song song)
    {
        if (song == null || string.IsNullOrEmpty(song.FilePath) || !System.IO.File.Exists(song.FilePath))
        {
            Debug.WriteLine("[SongMetadataService] SaveMetadataAsync: Invalid song or file path.");
            return false;
        }

        try
        {
            await Task.Run(() =>
            {
                using var tagFile = TagLib.File.Create(song.FilePath);

                tagFile.Tag.Title = song.Title;
                tagFile.Tag.Performers = new[] { song.Artist };
                tagFile.Tag.AlbumArtists = new[] { song.Artist }; // Often the same as performers
                tagFile.Tag.Album = song.Album;
                // Note: Duration is read-only from properties and not typically set manually.
                // Thumbnail saving is more complex and not handled here.

                tagFile.Save();
                Debug.WriteLine($"[SongMetadataService] Metadata saved successfully for: {Path.GetFileName(song.FilePath)}");
            });
            return true;
        }
        catch (CorruptFileException cfe)
        {
            Debug.WriteLine($"[SongMetadataService] Corrupt file when saving metadata for \"{Path.GetFileName(song.FilePath)}\": {cfe.Message}");
        }
        catch (UnsupportedFormatException ufe)
        {
            Debug.WriteLine($"[SongMetadataService] Unsupported format when saving metadata for \"{Path.GetFileName(song.FilePath)}\": {ufe.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SongMetadataService] Error saving metadata for \"{Path.GetFileName(song.FilePath)}\": {ex.Message}");
        }
        return false;
    }
}