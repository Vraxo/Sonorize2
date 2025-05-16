using System.Collections.Generic;

namespace Sonorize.Models;

public class AppSettings
{
    public List<string> MusicDirectories { get; set; } = new List<string>();
    public string? PreferredThemeFileName { get; set; } // Stores the name of the theme file
}