using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Sonorize.Services;

public class DefaultIconGenerator
{
    private const int IconDimension = 96;
    private const double DpiValue = 96.0;
    private const string MusicalNoteCharacter = "♫";
    private const double FontSizeFactor = 0.5;

    public static Bitmap? CreateMusicalNoteIcon()
    {
        Debug.WriteLine("[DefaultIconGenerator] CreateMusicalNoteIcon called.");
        try
        {
            var pixelSize = new PixelSize(IconDimension, IconDimension);
            var dpi = new Vector(DpiValue, DpiValue);

            using var renderTarget = new RenderTargetBitmap(pixelSize, dpi);
            using (DrawingContext context = renderTarget.CreateDrawingContext())
            {
                DrawMusicalNote(context, pixelSize);
            }

            return SaveToBitmap(renderTarget);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DefaultIconGenerator] CRITICAL EXCEPTION creating default icon: {ex}");
            return null;
        }
    }

    private static void DrawMusicalNote(DrawingContext context, PixelSize pixelSize)
    {
        var backgroundBrush = new SolidColorBrush(Colors.DimGray);
        var foregroundBrush = Brushes.WhiteSmoke;
        var bounds = new Rect(new Size(pixelSize.Width, pixelSize.Height));

        // Fill background
        context.FillRectangle(backgroundBrush, bounds);

        // Create formatted text for the musical note icon
        var formattedText = new FormattedText(
            MusicalNoteCharacter,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            Typeface.Default,
            pixelSize.Width * FontSizeFactor, // Font size relative to icon size
            foregroundBrush);

        // Calculate text origin to center it
        var textOrigin = new Point(
            (bounds.Width - formattedText.Width) / 2,
            (bounds.Height - formattedText.Height) / 2);

        // Draw the text
        context.DrawText(formattedText, textOrigin);
    }

    private static Bitmap? SaveToBitmap(RenderTargetBitmap renderTarget)
    {
        using var memoryStream = new MemoryStream();
        renderTarget.Save(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream.Length > 0 ? new Bitmap(memoryStream) : null;
    }
}