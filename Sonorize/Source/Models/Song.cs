using System;
using System.Diagnostics;
using Avalonia.Media.Imaging;
using Sonorize.ViewModels;

namespace Sonorize.Models;

public class Song : ViewModelBase
{
    public string DurationString => $"{Duration:mm\\:ss}";

    private string _filePath = string.Empty;
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }
    public string Title
    {
        get;
        set => SetProperty(ref field, value);
    } = "Unknown Title";

    private string _artist = "Unknown Artist";
    public string Artist
    {
        get => _artist;
        set => SetProperty(ref _artist, value);
    }

    private string _album = "Unknown Album";
    public string Album
    {
        get => _album;
        set => SetProperty(ref _album, value);
    }

    private TimeSpan _duration;
    public TimeSpan Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    private Bitmap? _thumbnail;
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set => SetProperty(ref _thumbnail, value);
    }

    private LoopRegion? _savedLoop;
    public LoopRegion? SavedLoop
    {
        get => _savedLoop;
        set => SetProperty(ref _savedLoop, value);
    }

    private bool _isLoopActive;
    public bool IsLoopActive
    {
        get => _isLoopActive;
        set
        {
            if (!SetProperty(ref _isLoopActive, value))
            {
                return;
            }

            Debug.WriteLine($"[SongModel] {Title} - IsLoopActive set to: {value}");
        }
    }

    private string _genre = "Unknown Genre";
    public string Genre
    {
        get => _genre;
        set => SetProperty(ref _genre, value);
    }

    private uint _trackNumber;
    public uint TrackNumber
    {
        get => _trackNumber;
        set => SetProperty(ref _trackNumber, value);
    }

    private uint _year;
    public uint Year
    {
        get => _year;
        set => SetProperty(ref _year, value);
    }
}