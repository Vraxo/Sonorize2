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
}