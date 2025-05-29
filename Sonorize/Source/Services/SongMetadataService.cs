using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging; // Required for Bitmap
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
                tagFile.Tag.AlbumArtists = new[] { song.Artist };
                tagFile.Tag.Album = song.Album;

                // Handle Thumbnail
                if (song.Thumbnail != null)
                {
                    using var ms = new MemoryStream();
                    // Save the Avalonia Bitmap to a stream (defaults to PNG format)
                    song.Thumbnail.Save(ms);
                    ms.Position = 0;
                    var pictureData = ms.ToArray();

                    var newPicture = new TagLib.Picture(new TagLib.ByteVector(pictureData))
                    {
                        Type = TagLib.PictureType.FrontCover,
                        MimeType = "image/png" // Assuming PNG, as Avalonia's Bitmap.Save default
                        // Description = "Cover" // Optional
                    };
                    tagFile.Tag.Pictures = new IPicture[] { newPicture };
                    Debug.WriteLine($"[SongMetadataService] Thumbnail data prepared for saving for: {Path.GetFileName(song.FilePath)}");
                }
                else
                {
                    // If song.Thumbnail is null, clear existing pictures from the file
                    tagFile.Tag.Pictures = Array.Empty<IPicture>();
                    Debug.WriteLine($"[SongMetadataService] Thumbnail is null. Clearing existing pictures for: {Path.GetFileName(song.FilePath)}");
                }

                tagFile.Save();
                Debug.WriteLine($"[SongMetadataService] Metadata (and potentially thumbnail) saved successfully for: {Path.GetFileName(song.FilePath)}");
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
            Debug.WriteLine($"[SongMetadataService] Error saving metadata for \"{Path.GetFileName(song.FilePath)}\": {ex.Message} - Stack: {ex.StackTrace}");
        }
        return false;
    }
}