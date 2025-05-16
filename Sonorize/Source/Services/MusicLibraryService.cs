using Avalonia; // For Application.Current, PixelSize, Point, Vector, Size
using Avalonia.Media; // For Colors, Brushes, FormattedText, Typeface, FlowDirection, DrawingContext, SolidColorBrush
using Avalonia.Media.Imaging; // For Bitmap, RenderTargetBitmap
using Avalonia.Platform; // For IAssetLoader (though not directly used in thumbnail generation here)
using Sonorize.Models;
using System;
using System.Collections.Generic;
using System.Globalization; // For CultureInfo
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Sonorize.Services;

public class MusicLibraryService
{
    private Bitmap? _defaultThumbnail;

    public MusicLibraryService()
    {
        // Constructor
    }

    private Bitmap GetOrCreateDefaultThumbnail()
    {
        if (_defaultThumbnail == null)
        {
            var pixelSize = new Avalonia.PixelSize(64, 64);
            var dpi = new Avalonia.Vector(96, 96); // Standard DPI, 1 DIP = 1 pixel

            using var renderTarget = new RenderTargetBitmap(pixelSize, dpi);

            using (Avalonia.Media.DrawingContext context = renderTarget.CreateDrawingContext())
            {
                // --- MODIFIED LINE ---
                // DrawingContext.Clear() was removed in Avalonia 11.1+.
                // Use FillRectangle to "clear" the context with a color.
                var clearBrush = new SolidColorBrush(Avalonia.Media.Colors.DimGray);
                var bounds = new Rect(new Size(pixelSize.Width, pixelSize.Height));
                context.FillRectangle(clearBrush, bounds);
                // --- END MODIFIED LINE ---

                var formattedText = new FormattedText(
                    "♫",
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default,
                    32,
                    Avalonia.Media.Brushes.WhiteSmoke);

                var textOrigin = new Avalonia.Point(
                    (bounds.Width - formattedText.Width) / 2,
                    (bounds.Height - formattedText.Height) / 2);
                context.DrawText(formattedText, textOrigin);
            }

            using var memoryStream = new MemoryStream();
            renderTarget.Save(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin);
            _defaultThumbnail = new Bitmap(memoryStream);
        }
        return _defaultThumbnail;
    }


    public async Task<List<Song>> LoadMusicFromDirectoriesAsync(IEnumerable<string> directories)
    {
        var songs = new List<Song>();
        var supportedExtensions = new[] { ".mp3", ".wav", ".flac", ".m4a", ".ogg" };

        Bitmap defaultThumbnail = GetOrCreateDefaultThumbnail();

        await Task.Run(() =>
        {
            foreach (var dir in directories)
            {
                if (!Directory.Exists(dir))
                {
                    Console.WriteLine($"Directory not found: {dir}");
                    continue;
                }

                try
                {
                    var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                                         .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

                    foreach (var file in files)
                    {
                        var song = new Song
                        {
                            FilePath = file,
                            Title = Path.GetFileNameWithoutExtension(file),
                            Artist = "Unknown Artist",
                            Duration = TimeSpan.FromMinutes(3) + TimeSpan.FromSeconds(Random.Shared.Next(0, 180)),
                            Thumbnail = defaultThumbnail
                        };
                        songs.Add(song);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error scanning directory {dir}: {ex.Message}");
                }
            }
        });
        return songs;
    }
}