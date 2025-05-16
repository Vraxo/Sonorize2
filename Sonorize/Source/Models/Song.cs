using Avalonia.Media.Imaging;
using Sonorize.ViewModels; // For ViewModelBase
using System;

namespace Sonorize.Models;

public class Song : ViewModelBase
{
    private string _filePath = string.Empty;
    public string FilePath { get => _filePath; set => SetProperty(ref _filePath, value); }

    private string _title = "Unknown Title";
    public string Title { get => _title; set => SetProperty(ref _title, value); }

    private string _artist = "Unknown Artist";
    public string Artist { get => _artist; set => SetProperty(ref _artist, value); }

    private TimeSpan _duration;
    public TimeSpan Duration { get => _duration; set => SetProperty(ref _duration, value); }
    public string DurationString => $"{Duration:mm\\:ss}";

    private Bitmap? _thumbnail;
    public Bitmap? Thumbnail { get => _thumbnail; set => SetProperty(ref _thumbnail, value); }
}