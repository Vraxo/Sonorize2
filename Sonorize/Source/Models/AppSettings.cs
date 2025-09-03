using System.Collections.Generic;
using Sonorize.ViewModels;

namespace Sonorize.Models;

public class AppSettings
{
    public List<string> MusicDirectories { get; set; } = [];
    public string? PreferredThemeFileName { get; set; }

    public string ArtistViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();
    public string AlbumViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();
    public string PlaylistViewModePreference { get; set; } = SongDisplayMode.Detailed.ToString();

    // Grid View Image Preferences
    public string ArtistGridViewImageType { get; set; } = "Composite";
    public string AlbumGridViewImageType { get; set; } = "Composite";
    public string PlaylistGridViewImageType { get; set; } = "Composite";
    public string PlaybackAreaBackgroundStyle { get; set; } = "Solid";

    // Library List Column Preferences
    public bool ShowArtistInLibrary { get; set; } = true;
    public bool ShowAlbumInLibrary { get; set; } = true;
    public bool ShowDurationInLibrary { get; set; } = true;
    public bool ShowDateAddedInLibrary { get; set; } = false;
    public bool ShowPlayCountInLibrary { get; set; } = false;
    public double LibraryRowHeight { get; set; } = 44.0;
    public bool EnableAlternatingRowColors { get; set; } = true;
    public bool UseCompactPlaybackControls { get; set; } = false;
    public bool ShowStatusBar { get; set; } = true;

    // Last.fm Settings
    public bool LastfmScrobblingEnabled { get; set; } = false;
    public string? LastfmUsername { get; set; }
    public string? LastfmPassword { get; set; } // Used for initial authentication to get a session key.
    public string? LastfmSessionKey { get; set; } // Stores the authenticated session key.

    public int ScrobbleThresholdPercentage { get; set; } = 50; // Default to 50%
    public int ScrobbleThresholdAbsoluteSeconds { get; set; } = 240; // Default to 240 seconds (4 minutes)
}