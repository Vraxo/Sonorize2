using System.Collections.Generic;

namespace Sonorize.Models;

public class Playlist
{
    public string Name { get; set; } = "Unknown Playlist";
    public string FilePath { get; set; } = string.Empty;
    public List<Song> Songs { get; set; } = new();
}
