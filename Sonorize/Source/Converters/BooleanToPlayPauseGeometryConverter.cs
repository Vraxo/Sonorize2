using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Platform;
using System;
using System.Globalization;
using System.Diagnostics;

namespace Sonorize.Converters;

public class BooleanToPlayPauseGeometryConverter : IValueConverter
{
    public static readonly BooleanToPlayPauseGeometryConverter Instance = new();

    // Store the loaded geometries statically
    private static readonly StreamGeometry? PlayGeometry;
    private static readonly StreamGeometry? PauseGeometry;

    static BooleanToPlayPauseGeometryConverter()
    {
        PlayGeometry = LoadSvgGeometry("avares://Sonorize/Assets/Play.svg");
        PauseGeometry = LoadSvgGeometry("avares://Sonorize/Assets/Pause.svg");

        if (PlayGeometry == null || PauseGeometry == null)
        {
            Debug.WriteLine("[Converter] ERROR: Failed to load Play/Pause SVG geometries.");
        }
    }

    private static StreamGeometry? LoadSvgGeometry(string uriString)
    {
        try
        {
            // Ensure the URI is absolute
            if (!Uri.TryCreate(uriString, UriKind.Absolute, out var uri))
            {
                Debug.WriteLine($"[Converter] Invalid URI format: {uriString}");
                return null;
            }

            // Check if it's an Avalonia resource URI
            if (uri.Scheme == "avares")
            {
                using (var stream = AssetLoader.Open(uri))
                {
                    // Use StreamGeometry.Parse to convert SVG path data
                    // However, directly parsing an SVG *file* as StreamGeometry.Parse
                    // expects a path *string*, not the entire file content.
                    // For loading the SVG *file* as geometry, we need a different approach
                    // or library. Assuming the SVGs contain only a single path or can be
                    // simplified for direct path extraction if necessary.

                    // A more direct approach for simple SVGs might be to manually extract the 'd' attribute from the <path> tag
                    // or use a library that parses SVG files into Avalonia Geometry.
                    // For now, let's assume a simple case where we can perhaps use hardcoded paths IF extraction is complex,
                    // or use a helper if available that parses the SVG stream into a Geometry.

                    // Let's try a simple approach assuming the SVG file contains *only* the path data string
                    // or can be read and parsed from the stream. This is not a robust SVG parser.
                    // A better way would be to use a library or XAML-based SVG loading.
                    // For this example, let's fallback to hardcoded path strings if stream parsing is difficult.

                    // *** Reverting to hardcoded paths if direct stream geometry parsing isn't straightforward for files ***
                    // A proper solution would involve a more complex SVG loading mechanism.
                    // Given the constraint to only modify/add files, and no explicit SVG parsing library is available,
                    // let's use hardcoded path data for demonstration, assuming simple SVG structures.

                    // Example SVG path data (replace with actual data if possible, or use a real SVG parser)
                    // Play: <path d="M8 5v14l11-7z"/>
                    // Pause: <path d="M6 19h4V5H6v14zm8-14v14h4V5h-4z"/>
                    // These are Material Design Icons standard paths (approximate)

                    string pathData = "";
                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        string svgContent = reader.ReadToEnd();
                        // Simple, non-robust attempt to find path data
                        // This requires the SVG structure to be very simple: <svg>...<path d="..."/>...</svg>
                        int pathStartIndex = svgContent.IndexOf(" d=");
                        if (pathStartIndex != -1)
                        {
                            int dValueStart = svgContent.IndexOf('"', pathStartIndex + 3);
                            if (dValueStart != -1)
                            {
                                int dValueEnd = svgContent.IndexOf('"', dValueStart + 1);
                                if (dValueEnd != -1)
                                {
                                    pathData = svgContent.Substring(dValueStart + 1, dValueEnd - dValueStart - 1);
                                    // Debug.WriteLine($"[Converter] Extracted path data for {uriString}: {pathData}");
                                }
                            }
                        }
                    }


                    if (!string.IsNullOrEmpty(pathData))
                    {
                        try
                        {
                            return StreamGeometry.Parse(pathData);
                        }
                        catch (Exception parseEx)
                        {
                            Debug.WriteLine($"[Converter] Error parsing path data '{pathData.Substring(0, Math.Min(pathData.Length, 100))}...' for {uriString}: {parseEx.Message}");
                            return null; // Failed to parse
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[Converter] No path data found in SVG stream for {uriString}.");
                        return null;
                    }
                }
            }
            else
            {
                Debug.WriteLine($"[Converter] Unsupported URI scheme for loading geometry: {uri.Scheme}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Converter] Error loading SVG geometry from {uriString}: {ex.Message}");
            return null;
        }
    }


    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isPlaying)
        {
            return isPlaying ? PauseGeometry : PlayGeometry;
        }
        // Default to Play icon if value is not a bool or if loading failed
        return PlayGeometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}