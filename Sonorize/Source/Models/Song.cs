using Avalonia.Media.Imaging;
using Sonorize.ViewModels; // For ViewModelBase
using System;
using System.ComponentModel;
using System.Diagnostics; // For INotifyPropertyChanged

namespace Sonorize.Models;

public class Song : ViewModelBase
{
    private string _filePath = string.Empty;
    public string FilePath { get => _filePath; set => SetProperty(ref _filePath, value); }

    private string _title = "Unknown Title";
    public string Title { get => _title; set => SetProperty(ref _title, value); }

    private string _artist = "Unknown Artist";
    public string Artist { get => _artist; set => SetProperty(ref _artist, value); }

    private string _album = "Unknown Album";
    public string Album { get => _album; set => SetProperty(ref _album, value); }

    private TimeSpan _duration;
    public TimeSpan Duration { get => _duration; set => SetProperty(ref _duration, value); }
    public string DurationString => $"{Duration:mm\\:ss}";

    private Bitmap? _thumbnail;
    public Bitmap? Thumbnail { get => _thumbnail; set => SetProperty(ref _thumbnail, value); }

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
            // SetProperty handles OnPropertyChanged
            if (SetProperty(ref _isLoopActive, value))
            {
                // The ViewModel will observe this change and trigger persistence
                Debug.WriteLine($"[SongModel] {Title} - IsLoopActive set to: {value}");
            }
        }
    }
}