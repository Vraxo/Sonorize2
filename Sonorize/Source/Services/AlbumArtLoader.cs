using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using TagLib;
using File = System.IO.File; // Required for IPicture, CorruptFileException, UnsupportedFormatException

namespace Sonorize.Services;

public class AlbumArtLoader
{
    public async Task<Bitmap?> LoadFromFileAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.WriteLine($"[AlbumArtLoader] File path is invalid or file does not exist: {filePath}");
            return null;
        }

        try
        {
            return await Task.Run(() =>
            {
                using var tagFile = TagLib.File.Create(filePath);
                if (tagFile.Tag.Pictures.Length > 0)
                {
                    IPicture pic = tagFile.Tag.Pictures[0];
                    using var ms = new MemoryStream(pic.Data.Data);
                    if (ms.Length > 0)
                    {
                        // Load the original bitmap
                        using var originalBitmap = new Bitmap(ms);
                        // Define the target size for the thumbnail
                        var targetSize = new PixelSize(128, 128); // Example size, adjust as needed
                        // Create a scaled version of the bitmap
                        return originalBitmap.CreateScaledBitmap(targetSize, BitmapInterpolationMode.HighQuality);
                    }
                }
                Debug.WriteLine($"[AlbumArtLoader] No pictures found in tags for: {Path.GetFileName(filePath)}");
                return null; // No pictures found or picture data is empty
            });
        }
        catch (CorruptFileException cfe)
        {
            Debug.WriteLine($"[AlbumArtLoader] Corrupt file when loading album art for \"{Path.GetFileName(filePath)}\": {cfe.Message}");
        }
        catch (UnsupportedFormatException ufe)
        {
            Debug.WriteLine($"[AlbumArtLoader] Unsupported format when loading album art for \"{Path.GetFileName(filePath)}\": {ufe.Message}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AlbumArtLoader] Generic error loading album art for \"{Path.GetFileName(filePath)}\": {ex.Message}");
        }
        return null; // Return null on any error
    }
}