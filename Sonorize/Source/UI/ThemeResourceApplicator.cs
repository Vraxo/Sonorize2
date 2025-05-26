using System.Diagnostics;
using Avalonia;
using Avalonia.Media;
using Sonorize.Models;
using Sonorize.Extensions; // For ChangeLightness and WithAlpha

namespace Sonorize.UI;

public static class ThemeResourceApplicator
{
    public static void ApplyCustomColorsToResources(Application app, ThemeColors themeColors)
    {
        System.ArgumentNullException.ThrowIfNull(app);
        System.ArgumentNullException.ThrowIfNull(themeColors);

        if (themeColors.B_AccentColor is ISolidColorBrush accentSolidBrush &&
            themeColors.B_AccentForeground is ISolidColorBrush accentForegroundSolidBrush)
        {
            Color accentColor = accentSolidBrush.Color;
            Color accentForegroundColor = accentForegroundSolidBrush.Color;
            Debug.WriteLine($"[ThemeResourceApplicator] Overriding FluentTheme accent resources. Accent: {accentColor}, AccentFG: {accentForegroundColor}");

            app.Resources["SystemAccentColor"] = accentColor;
            app.Resources["SystemAccentColorLight1"] = accentColor.ChangeLightness(0.15);
            app.Resources["SystemAccentColorLight2"] = accentColor.ChangeLightness(0.30);
            app.Resources["SystemAccentColorLight3"] = accentColor.ChangeLightness(0.45);
            app.Resources["SystemAccentColorDark1"] = accentColor.ChangeLightness(-0.15);
            app.Resources["SystemAccentColorDark2"] = accentColor.ChangeLightness(-0.30);
            app.Resources["SystemAccentColorDark3"] = accentColor.ChangeLightness(-0.45);
            app.Resources["AccentFillColorDefaultBrush"] = new SolidColorBrush(accentColor);
            app.Resources["AccentFillColorSecondaryBrush"] = new SolidColorBrush(accentColor.ChangeLightness(0.15).WithAlpha(204));
            app.Resources["AccentFillColorTertiaryBrush"] = new SolidColorBrush(accentColor.ChangeLightness(0.30).WithAlpha(153));
            app.Resources["AccentFillColorDisabledBrush"] = new SolidColorBrush(accentColor.WithAlpha(51));
            app.Resources["AccentFillColorSelectedTextBackgroundBrush"] = new SolidColorBrush(accentColor);
            app.Resources["TextOnAccentFillColorPrimaryBrush"] = new SolidColorBrush(accentForegroundColor);
            app.Resources["TextOnAccentFillColorSecondaryBrush"] = new SolidColorBrush(accentForegroundColor.WithAlpha(178));
            app.Resources["TextOnAccentFillColorDisabledBrush"] = new SolidColorBrush(accentForegroundColor.WithAlpha(127));
            app.Resources["AccentControlBackgroundBrush"] = new SolidColorBrush(accentColor);
        }
        else
        {
            Debug.WriteLine("[ThemeResourceApplicator] Warning: Custom theme AccentColor or AccentForeground is not a SolidColorBrush. Cannot fully override Fluent accent system.");
        }
    }
}