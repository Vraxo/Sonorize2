using Avalonia.Media;

namespace Sonorize.Extensions;

public static class ColorExtensions
{
    public static Color ChangeLightness(this Color color, double factor)
    {
        HslColor hsl = color.ToHsl();
        double newL = System.Math.Clamp(hsl.L + factor, 0.0, 1.0);
        return HslColor.FromAhsl(hsl.A, hsl.H, hsl.S, newL).ToRgb();
    }
}