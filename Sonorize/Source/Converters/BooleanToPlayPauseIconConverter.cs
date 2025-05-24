using System;
using Avalonia.Data.Converters;
using System.Globalization;
using Avalonia.Media; // Required for IImage?

namespace Sonorize.Converters
{
    // Note: For SVG support, you'll need to install the Avalonia.Svg.Skia NuGet package.
    // Loading SVGs directly might be more complex than returning paths.
    // This converter now returns paths, and the UI must load the image from the path.
    // Alternatively, the converter could return Avalonia.Media.IImage or Avalonia.Media.Imaging.Bitmap/Avalonia.Svg.Skia.SvgImageSource
    // objects directly if performance is critical, but returning paths is simpler for binding.
    // Assuming the UI control binding Image.Source can handle "avares://..." paths.

    public class BooleanToPlayPauseIconConverter : IValueConverter
    {
        public static readonly BooleanToPlayPauseIconConverter Instance = new();

        // Changed accessibility to public so UI code can reference these paths directly
        public const string PlayIconPath = "avares://Sonorize/Assets/Icons/Play.svg";
        public const string PauseIconPath = "avares://Sonorize/Assets/Icons/Pause.svg";

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // We are now returning a string path to the SVG asset
            if (value is bool isPlaying)
            {
                // Ensure the target type is compatible with string paths or image sources
                // If binding directly to Image.Source, this should work with Avalonia's AssetLoader.
                return isPlaying ? PauseIconPath : PlayIconPath;
            }
            return PlayIconPath; // Default to Play icon path
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}