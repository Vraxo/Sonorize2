using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading; // Required for DispatcherPriority if used, but not directly in this class

namespace Sonorize.Services;

public class DefaultIconGenerator
{
    public Bitmap? CreateMusicalNoteIcon()
    {
        Debug.WriteLine("[DefaultIconGenerator] CreateMusicalNoteIcon called.");
        try
        {
            var pixelSize = new PixelSize(96, 96); // Standard size for the default icon
            var dpi = new Vector(96, 96); // Standard DPI

            using var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
            using (DrawingContext context = renderTarget.CreateDrawingContext())
            {
                // Define background and foreground brushes
                var backgroundBrush = new SolidColorBrush(Colors.DimGray); // A neutral background
                var foregroundBrush = Brushes.WhiteSmoke; // A contrasting foreground for the icon
                var bounds = new Rect(new Size(pixelSize.Width, pixelSize.Height));

                // Fill background
                context.FillRectangle(backgroundBrush, bounds);

                // Create formatted text for the musical note icon
                var formattedText = new FormattedText(
                    "♫", // Musical note character
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    Typeface.Default, // Use default typeface
                    pixelSize.Width / 2, // Make font size relative to icon size
                    foregroundBrush);

                // Calculate text origin to center it
                var textOrigin = new Point(
                    (bounds.Width - formattedText.Width) / 2,
                    (bounds.Height - formattedText.Height) / 2);

                // Draw the text
                context.DrawText(formattedText, textOrigin);
            }

            // Save to a memory stream and create Bitmap
            using var memoryStream = new MemoryStream();
            renderTarget.Save(memoryStream);
            memoryStream.Seek(0, SeekOrigin.Begin); // Reset stream position

            // Return bitmap if stream is not empty
            return memoryStream.Length > 0 ? new Bitmap(memoryStream) : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DefaultIconGenerator] CRITICAL EXCEPTION creating default icon: {ex}");
            return null; // Return null on error
        }
    }
}