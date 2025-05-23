// Path: Source/Views/MainView.cs
using System;
using Avalonia.Media;

namespace Sonorize.Views;

public static class BrushExtensions
{
    public static IBrush Multiply(this IBrush brush, double factor)
    {
        if (brush is ISolidColorBrush solidBrush)
        {
            var c = solidBrush.Color;
            return new SolidColorBrush(Color.FromArgb(c.A, (byte)Math.Clamp(c.R * factor, 0, 255), (byte)Math.Clamp(c.G * factor, 0, 255), (byte)Math.Clamp(c.B * factor, 0, 255)));
        }
        return brush;
    }
}