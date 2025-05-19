using Avalonia.Media;
using System.Text.Json.Serialization;

namespace Sonorize.Models;

public class ThemeColors
{
    // --- Main UI Colors (Strings) ---
    public string BackgroundColor { get; set; } = "#FF1E1E1E";
    public string SlightlyLighterBackground { get; set; } = "#FF2D2D30";
    public string ControlBackgroundColor { get; set; } = "#FF3C3C3C";
    public string TextColor { get; set; } = "#FFF1F1F1";
    public string SecondaryTextColor { get; set; } = "#FFAAAAAA";
    public string AccentColor { get; set; } = "#FF007ACC";
    public string AccentForeground { get; set; } = "#FFFFFFFF";
    public string ListBoxBackground { get; set; } = "#FF2D2D30";

    // --- Brush Properties (IBrush) ---
    [JsonIgnore] public IBrush B_BackgroundColor => SolidColorBrush.Parse(BackgroundColor);
    [JsonIgnore] public IBrush B_SlightlyLighterBackground => SolidColorBrush.Parse(SlightlyLighterBackground);
    [JsonIgnore] public IBrush B_ControlBackgroundColor => SolidColorBrush.Parse(ControlBackgroundColor);
    [JsonIgnore] public IBrush B_TextColor => SolidColorBrush.Parse(TextColor);
    [JsonIgnore] public IBrush B_SecondaryTextColor => SolidColorBrush.Parse(SecondaryTextColor);
    [JsonIgnore] public IBrush B_AccentColor => SolidColorBrush.Parse(AccentColor);
    [JsonIgnore] public IBrush B_AccentForeground => SolidColorBrush.Parse(AccentForeground);
    [JsonIgnore] public IBrush B_ListBoxBackground => SolidColorBrush.Parse(ListBoxBackground);

    // --- Color Struct Properties (Color) ---
    [JsonIgnore] public Color C_BackgroundColor => Color.Parse(BackgroundColor);
    [JsonIgnore] public Color C_SlightlyLighterBackground => Color.Parse(SlightlyLighterBackground);
    [JsonIgnore] public Color C_ControlBackgroundColor => Color.Parse(ControlBackgroundColor);
    [JsonIgnore] public Color C_TextColor => Color.Parse(TextColor);
    [JsonIgnore] public Color C_SecondaryTextColor => Color.Parse(SecondaryTextColor);
    [JsonIgnore] public Color C_AccentColor => Color.Parse(AccentColor);
    [JsonIgnore] public Color C_AccentForeground => Color.Parse(AccentForeground);
    [JsonIgnore] public Color C_ListBoxBackground => Color.Parse(ListBoxBackground);

    public static ThemeColors CreateAmoledSpotifyTheme()
    {
        return new ThemeColors
        {
            BackgroundColor = "#FF000000",
            SlightlyLighterBackground = "#FF121212",
            ControlBackgroundColor = "#FF181818",
            TextColor = "#FFFFFFFF",
            SecondaryTextColor = "#FFB3B3B3",
            AccentColor = "#FF1DB954",      // Spotify Green
            AccentForeground = "#FF000000",  // Black text on green buttons
            ListBoxBackground = "#FF000000"
        };
    }
}