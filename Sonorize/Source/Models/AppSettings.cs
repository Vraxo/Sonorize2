using System.Collections.Generic;
using Sonorize.ViewModels;

namespace Sonorize.Models;

public class AppSettings
{
    public List<string> MusicDirectories { get; set; } = [];
    public string? PreferredThemeFileName { get; set; }

    public string LibraryViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();
    public string ArtistViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();
    public string AlbumViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();
    public string PlaylistViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();

    // Grid View Image Preferences
    public string ArtistGridViewImageType { get; set; } = "Composite";
    public string AlbumGridViewImageType { get; set; } = "Composite";
    public string PlaylistGridViewImageType { get; set; } = "Composite";

    // Last.fm Settings
    public bool LastfmScrobblingEnabled { get; set; } = false;
    public string? LastfmUsername { get; set; }
    public string? LastfmPassword { get; set; } // Used for initial authentication to get a session key.
    public string? LastfmSessionKey { get; set; } // Stores the authenticated session key.

    public int ScrobbleThresholdPercentage { get; set; } = 50; // Default to 50%
    public int ScrobbleThresholdAbsoluteSeconds { get; set; } = 240; // Default to 240 seconds (4 minutes)
}
