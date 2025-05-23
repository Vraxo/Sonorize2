using System.Collections.Generic;
using Sonorize.ViewModels; // Required for SongDisplayMode

namespace Sonorize.Models;

public class AppSettings
{
    public List<string> MusicDirectories { get; set; } = new List<string>();
    public string? PreferredThemeFileName { get; set; } // Stores the name of the theme file

    // Preferences for view modes, stored as strings for easy serialization
    public string LibraryViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();
    public string ArtistViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();
    public string AlbumViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();
}