using Avalonia.Media;

namespace Sonorize.Extensions;

public static class ColorManipulationExtensions
{
    public static Color WithAlpha(this Color color, byte alpha)
    {
        return new(alpha, color.R, color.G, color.B);
    }
}
