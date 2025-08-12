using System.Collections.Generic;

namespace Sonorize.Models;

public class Playlist
{
public string Name { get; set; } = "Unknown Playlist";
    public string? FilePath { get; set; }
    public List<Song> Songs { get; set; } = new();
    public bool IsAutoPlaylist { get; set; } = false;
}
