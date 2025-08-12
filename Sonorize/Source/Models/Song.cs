using System;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Sonorize.ViewModels;

namespace Sonorize.Models;

public class Song : ViewModelBase
{
    public string DurationString => $"{Duration:mm\\:ss}";

    public string FilePath
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    public string Title
    {
        get;
        set => SetProperty(ref field, value);
    } = "Unknown Title";

    public string Artist
    {
        get;
        set => SetProperty(ref field, value);
    } = "Unknown Artist";

    public string Album
    {
        get;
        set => SetProperty(ref field, value);
    } = "Unknown Album";

    public TimeSpan Duration
    {
        get;
        set => SetProperty(ref field, value);
    }

    public Bitmap? Thumbnail
    {
        get;
        set => SetProperty(ref field, value);
    }

    public DateTime DateAdded { get; set; }

    public int PlayCount
    {
        get;
        set => SetProperty(ref field, value);
    }

    public LoopRegion? SavedLoop
    {
        get;
        set => SetProperty(ref field, value);
    }

    public bool IsLoopActive
    {
        get;

        set
        {
            if (!SetProperty(ref field, value))
            {
                return;
            }

            Debug.WriteLine($"[SongModel] {Title} - IsLoopActive set to: {value}");
        }
    }
}